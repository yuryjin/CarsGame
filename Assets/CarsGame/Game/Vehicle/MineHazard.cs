using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    /// <summary>
    /// Pooled mine, simulated in ticks. Three flavours share the pool:
    /// <list type="bullet">
    /// <item>Dropped contact mine: arms after 0.5s (25 ticks), spares its owner for 1.0s (50 ticks),
    /// goes off when a car drives within <see cref="triggerRadius"/>, fizzles after 15s (750 ticks).</item>
    /// <item>Field mine (<see cref="MineField"/>): a contact mine the map lays. No owner, no expiry, a
    /// tighter trigger so a row of them leaves a lane to slalom through, and a yellow warning disc.</item>
    /// <item>Charge: ignores proximity. Its owner sets it off with a second press
    /// (<see cref="TryDetonateCharge"/>); otherwise the fuse fires it after <see cref="chargeFuseSeconds"/>.</item>
    /// </list>
    /// Every blast deals radial damage with linear falloff, knocks cars back and punctures them.
    /// Returns to the <see cref="RaceDirector"/> pool when it explodes or expires.
    /// </summary>
    public class MineHazard : MonoBehaviour, ISimulated
    {
        [Header("Damage & Radius")]
        public float damage = 25f;
        public float minDamage = 10f;
        public float triggerRadius = 2.5f;
        public float explosionRadius = 4f;
        public float explosionImpulse = 3000f;

        [Header("Timings")]
        public float armDelaySeconds = 0.5f;
        public float lifetimeSeconds = 15f;
        public float ownerImmunitySeconds = 1.0f;

        [Header("Field mine")]
        [Tooltip("Field mines trigger only when a car is nearly on top of them, so a row leaves a lane free.")]
        public float fieldTriggerRadius = 1.6f;
        public float fieldExplosionRadius = 3.5f;

        [Header("Charge")]
        public float chargeMinDamage = 15f;
        public float chargeExplosionRadius = 7f;
        public float chargeExplosionImpulse = 4500f;
        public float chargeArmSeconds = 0.6f;
        [Tooltip("An untouched charge goes off by itself this long after it was planted.")]
        public float chargeFuseSeconds = 8f;

        [Header("Status Effect")]
        public StatusKind statusApplied = StatusKind.Punctured;
        public float statusDuration = 2.5f;

        private RaceDirector director;
        private GameObject owner;
        private int ownerId;
        private bool live;
        private int spawnTick;
        private int armTick;
        private int deathTick;
        private int ownerImmunityTick;

        private MineVisual visual;
        private float customBlastRadius, customImpulse, activeStatusDuration;
        private StatusKind activeStatus;
        public MineSkin Skin { get; private set; }

        public GameObject Owner => owner;
        public int OwnerId => ownerId;
        public bool IsLive => live;
        public MineKind Kind { get; private set; }
        public bool IsField { get; private set; }
        /// <summary>Bumped on every activation, so a holder of a pooled reference can tell its mine was reused.</summary>
        public int Generation { get; private set; }
        public int SpawnTick => spawnTick;
        public int ArmTick => armTick;
        /// <summary>Tick the mine expires (contact) or its fuse fires (charge); -1 for field mines.</summary>
        public int DeathTick => deathTick;
        public int SimulationOrder => 1100;

        /// <summary>Distance at which a car sets this mine off (0 for charges, which ignore proximity).</summary>
        public float TriggerRadius => Kind == MineKind.Charge ? 0f : IsField ? fieldTriggerRadius : triggerRadius;
        public float BlastRadius => customBlastRadius > 0f ? customBlastRadius : Kind == MineKind.Charge ? chargeExplosionRadius : IsField ? fieldExplosionRadius : explosionRadius;

        /// <summary>AI cache: where the mine sits on the track spline (filled lazily by <see cref="AiDriver"/>).</summary>
        [System.NonSerialized] public bool HasTrackPose;
        [System.NonSerialized] public float TrackDistance, TrackLateral;

        public bool IsArmed(int tick) => live && tick >= armTick;
        public bool IsOwnerImmune(int tick) => live && tick < ownerImmunityTick;

        public static MineHazard CreatePooled(Transform parent)
        {
            // Detection is deterministic in the tick simulation: the mine has no collider at all.
            var go = new GameObject("Mine_Hazard");
            go.transform.SetParent(parent, false);
            var mine = go.AddComponent<MineHazard>();
            mine.visual = MineVisual.Build(go.transform);
            go.SetActive(false);
            return mine;
        }

        public void Activate(RaceDirector owningDirector, in MineSpawn spawn)
        {
            director = owningDirector;
            owner = spawn.Owner;
            ownerId = spawn.OwnerId;
            Kind = spawn.Kind;
            Skin = spawn.Skin;
            customBlastRadius = Mathf.Max(0f, spawn.BlastRadius);
            customImpulse = spawn.Impulse;
            activeStatus = spawn.OverrideStatus ? spawn.Status : statusApplied;
            activeStatusDuration = spawn.OverrideStatus ? spawn.StatusDuration : statusDuration;
            IsField = spawn.Field && spawn.Kind == MineKind.Contact;
            damage = spawn.Damage > 0f ? spawn.Damage : Kind == MineKind.Charge ? 45f : 25f;
            spawnTick = spawn.Tick;
            live = true;
            Generation++;
            HasTrackPose = false;

            float armSeconds = Kind == MineKind.Charge ? chargeArmSeconds : armDelaySeconds;
            armTick = spawn.Tick + SimulationDriver.SecondsToTicks(armSeconds);
            ownerImmunityTick = spawn.Tick + SimulationDriver.SecondsToTicks(ownerImmunitySeconds);
            if (IsField) deathTick = -1;
            else deathTick = spawn.Tick + SimulationDriver.SecondsToTicks(Kind == MineKind.Charge ? chargeFuseSeconds : lifetimeSeconds);

            transform.position = spawn.Position;
            transform.rotation = spawn.Rotation;

            if (visual != null) visual.Show(Kind, IsField, spawn.Material, Skin);

            gameObject.SetActive(true);
        }

        public void SimulationStep(int tick)
        {
            if (!live) return;

            if (deathTick >= 0 && tick >= deathTick)
            {
                // A charge's fuse ends in a bang; an old contact mine just fizzles out.
                if (Kind == MineKind.Charge) Detonate(tick);
                else Deactivate();
                return;
            }

            if (tick < armTick || Kind == MineKind.Charge) return;

            // Check proximity to all registered cars
            if (director != null && director.Cars != null)
            {
                float radius = TriggerRadius;
                var cars = director.Cars;
                for (int i = 0; i < cars.Count; i++)
                {
                    var car = cars[i];
                    if (car == null || !car.gameObject.activeInHierarchy) continue;

                    var health = car.GetComponent<CarHealth>();
                    if (health != null && health.IsDead) continue;

                    bool isOwner = owner != null && car.gameObject == owner;
                    if (isOwner && tick < ownerImmunityTick) continue;
                    if (owner != null && !isOwner && !Teams.Hostile(owner, car.gameObject)) continue;   // mission team-mates roll over it

                    float dist = Vector3.Distance(transform.position, car.transform.position);
                    if (dist <= radius)
                    {
                        Detonate(tick);
                        return;
                    }
                }
            }
        }

        /// <summary>The owner's second press. Only a charge that has finished arming goes off.</summary>
        public bool TryDetonateCharge(int tick)
        {
            if (!live || Kind != MineKind.Charge || tick < armTick) return false;
            Detonate(tick);
            return true;
        }

        public void Detonate(int tick)
        {
            if (!live) return;

            Vector3 blastCenter = transform.position;
            float radius = BlastRadius;
            float floor = Mathf.Min(damage, Kind == MineKind.Charge ? chargeMinDamage : minDamage);
            float impulse = customImpulse != 0f ? customImpulse : Kind == MineKind.Charge ? chargeExplosionImpulse : explosionImpulse;

            if (director != null && director.Cars != null)
            {
                var cars = director.Cars;
                for (int i = 0; i < cars.Count; i++)
                {
                    var car = cars[i];
                    if (car == null || !car.gameObject.activeInHierarchy) continue;

                    var health = car.GetComponent<CarHealth>();
                    if (health == null || health.IsDead) continue;
                    if (owner != null && car.gameObject != owner && !Teams.Hostile(owner, car.gameObject)) continue;

                    float dist = Vector3.Distance(blastCenter, car.transform.position);
                    if (dist <= radius)
                    {
                        float t = Mathf.Clamp01(dist / radius);
                        float dealDamage = Mathf.Lerp(damage, floor, t);

                        Vector3 diff = car.transform.position - blastCenter;
                        Vector3 dir = diff.sqrMagnitude > 0.01f ? diff.normalized : Vector3.up;

                        var hit = new HitInfo
                        {
                            Attacker = owner,
                            Damage = dealDamage,
                            Point = blastCenter,
                            Normal = Vector3.up,
                            HitType = HitType.Hazard,
                            Impulse = dir * impulse,
                            Status = activeStatus,
                            StatusDuration = activeStatusDuration
                        };

                        var hitResult = health.ApplyHit(in hit);
                        if (hitResult.DamageApplied && !hitResult.ShieldAbsorbed && director != null)
                        {
                            director.CountMineHit(owner, health.gameObject);
                        }

                        var carRb = car.GetComponent<Rigidbody>();
                        if (carRb != null && !carRb.isKinematic && hitResult.DamageApplied)
                        {
                            Vector3 liftImpulse = (dir + Vector3.up * 0.35f).normalized * impulse;
                            carRb.AddForceAtPosition(liftImpulse, blastCenter, ForceMode.Impulse);
                        }
                    }
                }
            }

            // Mission units in the blast (troopers, a low drone, the boss's hull).
            var units = MissionUnit.All;
            for (int i = units.Count - 1; i >= 0; i--)
            {
                if (i >= units.Count) continue;
                var u = units[i];
                if (u == null || !u.Targetable) continue;
                float d = Vector3.Distance(blastCenter, u.AimPointFrom(blastCenter));
                if (d > radius) continue;
                if (u.TakeHit(owner, Mathf.Lerp(damage, floor, Mathf.Clamp01(d / radius)), blastCenter) && director != null) director.CountMineHit(owner, u.gameObject);
            }

            if (EffectPool.Instance != null)
            {
                EffectPool.Instance.PlayWreck(blastCenter);
            }

            if (SfxPlayer.Instance != null)
            {
                SfxPlayer.Instance.Play(SfxSynth.Wreck, blastCenter, Kind == MineKind.Charge ? 1f : 0.9f, Kind == MineKind.Charge ? 0.8f : 1.1f);
            }

            Deactivate();
        }

        public void Deactivate()
        {
            if (!live) return;
            live = false;
            owner = null;
            gameObject.SetActive(false);
            if (director != null) director.ReturnToPool(this);
        }

        // Presentation only: the light blinks from the tick the simulation already reached.
        private void LateUpdate()
        {
            if (!live || visual == null) return;
            var driver = SimulationDriver.Current;
            int tick = driver != null ? driver.Tick : spawnTick;
            visual.Blink(tick, armTick, deathTick, Kind, Time.time);
        }
    }
}
