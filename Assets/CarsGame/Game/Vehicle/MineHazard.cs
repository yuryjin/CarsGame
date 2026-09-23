using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    /// <summary>
    /// Pooled contact mine. Spawned behind vehicles with delayed arming (0.5s / 25 ticks)
    /// and owner immunity window (1.0s / 50 ticks). Detonates on proximity of rivals,
    /// deals radial blast damage with linear falloff, knocks victims back and applies Punctured status.
    /// Returns to <see cref="RaceDirector"/> pool on detonation or lifetime expiry (15s / 750 ticks).
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

        private Renderer mineRenderer;

        public GameObject Owner => owner;
        public int OwnerId => ownerId;
        public bool IsLive => live;
        public int SimulationOrder => 1100;

        public bool IsArmed(int tick) => live && tick >= armTick;
        public bool IsOwnerImmune(int tick) => live && tick < ownerImmunityTick;

        public static MineHazard CreatePooled(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Mine_Hazard";
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(1.1f, 0.18f, 1.1f);
            int layer = LayerMask.NameToLayer("Track");
            go.layer = layer >= 0 ? layer : 0;

            // Remove physics collider since detection is done deterministically in tick simulation
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }

            var mine = go.AddComponent<MineHazard>();
            mine.mineRenderer = go.GetComponent<Renderer>();
            go.SetActive(false);
            return mine;
        }

        private void Awake()
        {
            if (mineRenderer == null) mineRenderer = GetComponent<Renderer>();
        }

        public void Activate(RaceDirector owningDirector, in MineSpawn spawn)
        {
            director = owningDirector;
            owner = spawn.Owner;
            ownerId = spawn.OwnerId;
            damage = spawn.Damage > 0f ? spawn.Damage : 25f;
            spawnTick = spawn.Tick;
            live = true;

            int armDelayTicks = SimulationDriver.SecondsToTicks(armDelaySeconds);
            int lifetimeTicks = SimulationDriver.SecondsToTicks(lifetimeSeconds);
            int ownerImmunityTicks = SimulationDriver.SecondsToTicks(ownerImmunitySeconds);

            armTick = spawn.Tick + armDelayTicks;
            deathTick = spawn.Tick + lifetimeTicks;
            ownerImmunityTick = spawn.Tick + ownerImmunityTicks;

            transform.position = spawn.Position;
            transform.rotation = spawn.Rotation;

            if (spawn.Material != null && mineRenderer != null)
            {
                mineRenderer.sharedMaterial = spawn.Material;
            }

            gameObject.SetActive(true);
        }

        public void SimulationStep(int tick)
        {
            if (!live) return;

            if (tick >= deathTick)
            {
                Deactivate();
                return;
            }

            if (tick < armTick) return;

            // Check proximity to all registered cars
            if (director != null && director.Cars != null)
            {
                var cars = director.Cars;
                for (int i = 0; i < cars.Count; i++)
                {
                    var car = cars[i];
                    if (car == null || !car.gameObject.activeInHierarchy) continue;

                    var health = car.GetComponent<CarHealth>();
                    if (health != null && health.IsDead) continue;

                    bool isOwner = car.gameObject == owner;
                    if (isOwner && tick < ownerImmunityTick) continue;

                    float dist = Vector3.Distance(transform.position, car.transform.position);
                    if (dist <= triggerRadius)
                    {
                        Detonate(tick);
                        return;
                    }
                }
            }
        }

        public void Detonate(int tick)
        {
            if (!live) return;

            Vector3 blastCenter = transform.position;

            if (director != null && director.Cars != null)
            {
                var cars = director.Cars;
                for (int i = 0; i < cars.Count; i++)
                {
                    var car = cars[i];
                    if (car == null || !car.gameObject.activeInHierarchy) continue;

                    var health = car.GetComponent<CarHealth>();
                    if (health != null && health.IsDead) continue;

                    float dist = Vector3.Distance(blastCenter, car.transform.position);
                    if (dist <= explosionRadius)
                    {
                        float t = Mathf.Clamp01(dist / explosionRadius);
                        float dealDamage = Mathf.Lerp(damage, minDamage, t);

                        Vector3 diff = car.transform.position - blastCenter;
                        Vector3 dir = diff.sqrMagnitude > 0.01f ? diff.normalized : Vector3.up;

                        var hit = new HitInfo
                        {
                            Attacker = owner,
                            Damage = dealDamage,
                            Point = blastCenter,
                            Normal = Vector3.up,
                            HitType = HitType.Hazard,
                            Impulse = dir * explosionImpulse,
                            Status = statusApplied,
                            StatusDuration = statusDuration
                        };

                        var hitResult = health.ApplyHit(in hit);
                        if (hitResult.DamageApplied && !hitResult.ShieldAbsorbed && director != null)
                        {
                            director.CountMineHit(owner, health.gameObject);
                        }

                        var carRb = car.GetComponent<Rigidbody>();
                        if (carRb != null && !carRb.isKinematic && hitResult.DamageApplied)
                        {
                            Vector3 liftImpulse = (dir + Vector3.up * 0.35f).normalized * explosionImpulse;
                            carRb.AddForceAtPosition(liftImpulse, blastCenter, ForceMode.Impulse);
                        }
                    }
                }
            }

            if (EffectPool.Instance != null)
            {
                EffectPool.Instance.PlayWreck(blastCenter);
            }

            if (SfxPlayer.Instance != null)
            {
                SfxPlayer.Instance.Play(SfxSynth.Wreck, blastCenter, 0.9f, 1.1f);
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
    }
}
