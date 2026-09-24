using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    /// <summary>Tick-owned contact, beam, area and trap attacks; animation never applies damage.</summary>
    public sealed class SpecialWeaponAttack
    {
        private readonly GameObject owner;
        private readonly CarController car;
        private CarController pendingTarget;
        private PrimaryWeaponCatalog.Profile pendingShot;
        private float pendingDamage;
        private int impactTick = -1;
        private WeaponRig rig;
        private PrimaryWeaponType type;

        public SpecialWeaponAttack(GameObject owner)
        {
            this.owner = owner;
            car = owner.GetComponent<CarController>();
        }

        public void Configure(PrimaryWeaponType weapon)
        {
            Cancel();
            type = weapon;
            if (rig != null) rig.Dispose();
            rig = Application.isPlaying && PrimaryWeaponCatalog.Get(type).Mode != WeaponMode.Projectile
                && PrimaryWeaponCatalog.Get(type).Mode != WeaponMode.Trap ? new WeaponRig(owner.transform, type) : null;
        }

        public void Cancel() { impactTick = -1; pendingTarget = null; rig?.Stop(); }
        public void UpdateVisual() => rig?.Update();

        public void Step(int tick)
        {
            if (impactTick < 0 || tick < impactTick) return;
            impactTick = -1;
            var status = owner.GetComponent<CarStatus>();
            var health = owner.GetComponent<CarHealth>();
            if ((status != null && status.IsJammed) || (health != null && health.IsDead)) return;
            if (CanHit(pendingTarget, pendingShot)) Hit(pendingTarget, pendingShot, pendingDamage);
            pendingTarget = null;
        }

        public void Fire(PrimaryWeaponCatalog.Profile shot, float damage, int tick)
        {
            var director = RaceDirector.Current ?? RaceDirector.Instance;
            Vector3 origin = owner.transform.position + Vector3.up;
            if (shot.Mode == WeaponMode.Trap)
            {
                Vector3 position = owner.transform.position - owner.transform.forward * 3f;
                if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out var ground, 8f, TargetFinder.ObstacleMask, QueryTriggerInteraction.Ignore))
                    position = ground.point + Vector3.up * 0.05f;
                director.RequestMine(new MineSpawn
                {
                    Owner = owner, OwnerId = car != null ? car.CarId : 0, Position = position,
                    Rotation = Quaternion.Euler(0f, owner.transform.eulerAngles.y, 0f), Damage = damage, Tick = tick,
                    Skin = shot.Skin, OverrideStatus = shot.Status != StatusKind.None,
                    Status = shot.Status, StatusDuration = shot.StatusSeconds,
                    BlastRadius = shot.Range, Impulse = shot.Impact
                });
                return;
            }

            var target = TargetFinder.Find(car, origin, owner.transform.forward, shot.Mode == WeaponMode.Pulse ? 180f : shot.Arc, 0.1f, shot.Range);
            Vector3 end = target != null ? target.transform.position + Vector3.up * 0.5f : origin + owner.transform.forward * shot.Range;
            if (target == null && Physics.Linecast(origin, end, out var obstacle, TargetFinder.ObstacleMask, QueryTriggerInteraction.Ignore)) end = obstacle.point;
            rig?.Play(end, shot);

            if (shot.Mode == WeaponMode.Melee)
            {
                pendingTarget = target;
                pendingShot = shot;
                pendingDamage = damage;
                impactTick = tick + SimulationDriver.SecondsToTicks(Mathf.Min(0.18f, shot.Interval * 0.45f));
            }
            else if (shot.Mode == WeaponMode.Beam)
            {
                if (CanHit(target, shot)) Hit(target, shot, damage);
            }
            else
            {
                var cars = director.Cars;
                for (int i = 0; i < cars.Count; i++)
                    if (CanHit(cars[i], shot)) Hit(cars[i], shot, damage);
            }
        }

        private bool CanHit(CarController target, PrimaryWeaponCatalog.Profile shot)
        {
            if (target == null || target.gameObject == owner || !target.isActiveAndEnabled) return false;
            var health = target.GetComponent<CarHealth>();
            if (health == null || health.IsDead || health.IsInvulnerable) return false;
            Vector3 origin = owner.transform.position + Vector3.up;
            Vector3 end = target.transform.position + Vector3.up * 0.5f;
            Vector3 offset = end - origin;
            if (offset.sqrMagnitude > shot.Range * shot.Range || offset.sqrMagnitude < 0.001f) return false;
            if (shot.Mode != WeaponMode.Pulse && Vector3.Angle(owner.transform.forward, offset) > shot.Arc) return false;
            return !Physics.Linecast(origin, end, TargetFinder.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        private void Hit(CarController target, PrimaryWeaponCatalog.Profile shot, float damage)
        {
            Vector3 direction = (target.transform.position - owner.transform.position).normalized;
            var hit = new HitInfo
            {
                Attacker = owner, Damage = damage, HitType = HitType.Direct,
                Point = target.transform.position + Vector3.up * 0.5f, Normal = -direction,
                Status = shot.Status, StatusDuration = shot.StatusSeconds, Impulse = direction * shot.Impact
            };
            var result = target.GetComponent<CarHealth>().ApplyHit(in hit);
            if (!result.DamageApplied || result.ShieldAbsorbed) return;
            if (target.Body != null && !target.Body.isKinematic)
                target.Body.AddForce(direction * shot.Impact, ForceMode.Impulse);
            RaceDirector.Current?.CountHit(owner, target.gameObject);
            EffectPool.Current?.PlayShellImpact(hit.Point);
            if (shot.Mode == WeaponMode.Melee)
                WeaponSounds.MeleeHit(type, hit.Point);
        }
    }
}
