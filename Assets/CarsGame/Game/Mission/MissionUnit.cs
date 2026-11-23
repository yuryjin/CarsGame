using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public enum UnitKind { Drone, Infantry, RocketTrooper, Turret, Plane, BossPart, BossCore, BossHull }

    /// <summary>
    /// A mission enemy that is not a car (drone, cyborg trooper, turret, plane, a part of the boss). Owns its hit
    /// points, takes hits from <see cref="Projectile"/> and special weapons, and steps its behaviour in the simulation
    /// tick while the mission runs. Every live unit is in <see cref="All"/>, which the target finder and the HUD read.
    /// </summary>
    public abstract class MissionUnit : MonoBehaviour, ISimulated
    {
        /// <summary>Live units (alive or dying) in registration order.</summary>
        public static readonly List<MissionUnit> All = new List<MissionUnit>(64);
        /// <summary>Set by the mission while the fight is on; units hold still in the countdown and after the end.</summary>
        public static bool Running;
        /// <summary>(unit, attacker) — the unit died. Attacker may be null.</summary>
        public static event Action<MissionUnit, GameObject> Killed;
        /// <summary>(unit, attacker, damage) — HP actually lost.</summary>
        public static event Action<MissionUnit, GameObject, float> Damaged;

        private static int nextSerial;

        public UnitKind Kind { get; protected set; }
        public float MaxHealth { get; protected set; } = 50f;
        public float Health { get; protected set; } = 50f;
        public bool Alive { get; private set; } = true;
        /// <summary>Damage taken per point dealt (armour below 1).</summary>
        public float DamageTaken = 1f;
        /// <summary>Unique per unit, above any car id: the kill tally counts each unit as its own victim.</summary>
        public int KillId { get; private set; }
        /// <summary>Radius the aim assist and the HUD bar use (m).</summary>
        public float Radius = 1f;
        /// <summary>Hidden units (a plane still off the map) cannot be locked.</summary>
        public bool Hidden;
        /// <summary>Screen bar above the unit once hurt.</summary>
        public bool ShowBar = true;
        public virtual bool Targetable => Alive && !Hidden && isActiveAndEnabled;
        public virtual Vector3 AimPoint => transform.position;
        public virtual Vector3 Velocity => Vector3.zero;
        /// <summary>Where a shooter at <paramref name="origin"/> should aim (big units: the nearest part of the hull).</summary>
        public virtual Vector3 AimPointFrom(Vector3 origin) => AimPoint;
        /// <summary>The owner written on this unit's shells (boss parts use the boss, so the shells skip its hull).</summary>
        protected virtual GameObject ShotOwner => gameObject;
        public int SimulationOrder => int.MaxValue - 5000 + (KillId % 3000);

        protected int DespawnTick = int.MaxValue;
        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private float flashUntil;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        protected virtual void Awake()
        {
            KillId = 1000000 + (++nextSerial);
            Teams.Set(gameObject, Team.Enemy);
        }

        protected void SetHealth(float hp)
        {
            MaxHealth = Health = Mathf.Max(1f, hp);
        }

        /// <summary>Call after the visuals exist: they flash white on a hit.</summary>
        protected void CollectRenderers() => renderers = GetComponentsInChildren<Renderer>(true);

        protected virtual void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            SimulationDriver.Instance.Register(this);
        }

        protected virtual void OnDisable()
        {
            All.Remove(this);
            var driver = SimulationDriver.Current;
            if (driver != null) driver.Unregister(this);
        }

        public void SimulationStep(int tick)
        {
            if (!Alive)
            {
                StepDying(tick);
                if (tick >= DespawnTick) Destroy(gameObject);
                return;
            }
            if (Running) Step(tick);
        }

        protected abstract void Step(int tick);
        protected virtual void StepDying(int tick) { }

        /// <summary>A hit from <paramref name="attacker"/> (null = the world). Friendly fire is ignored. True when HP was lost.</summary>
        public virtual bool TakeHit(GameObject attacker, float damage, Vector3 point)
        {
            if (!Alive || damage <= 0f || !Teams.Hostile(attacker, gameObject)) return false;
            float dealt = Mathf.Min(Health, damage * DamageTaken);
            if (dealt <= 0f) return false;
            Health = Mathf.Max(0f, Health - dealt);
            flashUntil = Time.time + 0.07f;
            SetFlash(true);
            Damaged?.Invoke(this, attacker, dealt);
            OnDamaged(attacker, dealt, point);
            if (Health <= 0f) Die(attacker);
            return true;
        }

        protected virtual void OnDamaged(GameObject attacker, float dealt, Vector3 point) { }

        /// <summary>Remove at once (the director's cleanup): no kill credit, no explosion.</summary>
        public void Remove() { Alive = false; DespawnTick = 0; }

        protected void Die(GameObject attacker)
        {
            if (!Alive) return;
            Alive = false;
            SetFlash(false);
            int tick = SimulationDriver.Instance.Tick;
            DespawnTick = tick + SimulationDriver.SecondsToTicks(DespawnSeconds);
            OnDied(attacker);
            Killed?.Invoke(this, attacker);
        }

        protected virtual float DespawnSeconds => 3f;

        /// <summary>Default wreck: a blast and a dark tint; subclasses drop, topple or hide themselves.</summary>
        protected virtual void OnDied(GameObject attacker)
        {
            Explode(AimPoint, 1f);
            Tint(new Color(0.16f, 0.13f, 0.1f));
        }

        public static void Explode(Vector3 point, float size)
        {
            var pool = EffectPool.Current ?? EffectPool.Instance;
            if (pool != null)
            {
                pool.PlayWreck(point);
                if (size > 1.5f) pool.PlayFlash(point + Vector3.up * size, new Color(1f, 0.5f, 0.1f), 0.9f, 3.5f * size, 12f * size, 8f);
            }
            if (Application.isPlaying) SfxPlayer.Instance.Play(SfxSynth.Wreck, point, Mathf.Clamp(0.55f + size * 0.2f, 0.5f, 1f), size > 2f ? 0.7f : 1f);
        }

        protected void Tint(Color color)
        {
            if (renderers == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            block.Clear();
            block.SetColor(BaseColorId, color);
            foreach (var r in renderers) if (r != null) r.SetPropertyBlock(block);
        }

        private void SetFlash(bool on)
        {
            if (renderers == null) return;
            if (on) Tint(new Color(2.2f, 2f, 1.8f));
            else foreach (var r in renderers) if (r != null) r.SetPropertyBlock(null);
        }

        protected virtual void LateUpdate()
        {
            if (flashUntil > 0f && Time.time >= flashUntil) { flashUntil = 0f; if (Alive) SetFlash(false); }
        }

        // ---------- shared combat helpers ----------

        /// <summary>A shell from this unit through the race director's pool; the unit is its owner (friendly fire off).</summary>
        protected void FireShell(Vector3 muzzle, Vector3 velocity, float damage, int tick, ProjectileKind kind = ProjectileKind.Shell,
            PrimaryWeaponType weapon = PrimaryWeaponType.Cannon, float impact = 2500f, Material material = null)
        {
            var director = RaceDirector.Current ?? RaceDirector.Instance;
            if (director == null) return;
            director.RequestProjectile(new ProjectileSpawn
            {
                Owner = ShotOwner, OwnerId = 0, Position = muzzle, Velocity = velocity, Damage = damage, Tick = tick,
                Kind = kind, Weapon = weapon, Impact = impact, Material = material != null ? material : MissionArt.EnemyShell
            });
            var pool = EffectPool.Current;
            if (pool != null) pool.PlayMuzzle(muzzle);
        }

        /// <summary>Where to point a shell of <paramref name="speed"/> so it meets a target holding its velocity (gravity included).</summary>
        public static Vector3 Lead(Vector3 from, Vector3 target, Vector3 targetVelocity, float speed, bool gravity = true)
        {
            speed = Mathf.Max(1f, speed);
            float t = Vector3.Distance(from, target) / speed;
            Vector3 point = target + targetVelocity * t;
            t = Vector3.Distance(from, point) / speed;
            point = target + targetVelocity * t;
            return gravity ? point - Physics.gravity * (0.5f * t * t) : point;
        }

        /// <summary>Nearest live, vulnerable car of the other side within range (optionally in clear sight), else null.</summary>
        public CarController FindHostileCar(Vector3 from, float range, bool lineOfSight, int avoidId = 0)
        {
            var director = RaceDirector.Current;
            if (director == null) return null;
            CarController best = null;
            float bestScore = float.MaxValue;
            var cars = director.Cars;
            for (int i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                if (car == null || !car.isActiveAndEnabled || !Teams.Hostile(gameObject, car.gameObject)) continue;
                var health = car.GetComponent<CarHealth>();
                if (health != null && (health.IsDead || health.IsInvulnerable)) continue;
                Vector3 centre = car.transform.position + Vector3.up * 0.6f;
                float d2 = (centre - from).sqrMagnitude;
                if (d2 > range * range) continue;
                float score = d2 + (car.CarId == avoidId ? range * range * 0.5f : 0f);
                if (score >= bestScore) continue;
                if (lineOfSight && Physics.Linecast(from, centre, TargetFinder.ObstacleMask, QueryTriggerInteraction.Ignore)) continue;
                best = car; bestScore = score;
            }
            return best;
        }

        /// <summary>Damage every hostile car inside the radius, falling off to a third at the edge, and shove it outwards.</summary>
        public static void Blast(GameObject source, Vector3 centre, float radius, float damage, float shove = 5f)
        {
            var director = RaceDirector.Current;
            if (director == null) return;
            var cars = director.Cars;
            for (int i = 0; i < cars.Count; i++)
            {
                var car = cars[i];
                if (car == null || !car.isActiveAndEnabled || !Teams.Hostile(source, car.gameObject)) continue;
                Vector3 offset = car.transform.position - centre;
                float distance = offset.magnitude;
                if (distance > radius) continue;
                var health = car.GetComponent<CarHealth>();
                if (health == null) continue;
                float k = Mathf.Lerp(1f, 0.35f, distance / radius);
                var hit = new Core.Combat.HitInfo(source, damage * k, Core.Combat.HitType.Explosion) { Point = car.transform.position, Normal = Vector3.up };
                var result = health.ApplyHit(in hit);
                if (result.DamageApplied && car.Body != null && !car.Body.isKinematic)
                {
                    Vector3 away = distance > 0.1f ? offset / distance : Vector3.forward;
                    car.Body.AddForce((away + Vector3.up * 0.6f).normalized * shove * k, ForceMode.VelocityChange);
                }
            }
        }
    }
}
