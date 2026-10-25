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
        private MissionUnit pendingUnit;
        private SpaceTurret pendingTurret;
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

        public void Cancel() { impactTick = -1; pendingTarget = null; pendingUnit = null; pendingTurret = null; rig?.Stop(); }
        public void UpdateVisual() => rig?.Update();

        public void Step(int tick)
        {
            if (impactTick < 0 || tick < impactTick) return;
            impactTick = -1;
            var status = owner.GetComponent<CarStatus>();
            var health = owner.GetComponent<CarHealth>();
            if ((status != null && status.IsJammed) || (health != null && health.IsDead)) return;
            if (CanHit(pendingTarget, pendingShot)) Hit(pendingTarget, pendingShot, pendingDamage);
            else if (CanHit(pendingUnit, pendingShot)) HitUnit(pendingUnit, pendingDamage);
            else if (CanHit(pendingTurret, pendingShot)) HitTurret(pendingTurret, pendingDamage);
            pendingTarget = null;
            pendingUnit = null;
            pendingTurret = null;
        }

        // The aiming marker and the attack use the same reach, arc and occlusion rules.
        public bool TryGetAimPoint(PrimaryWeaponCatalog.Profile shot, out Vector3 end)
        {
            FindTargets(shot, out var target, out var unit, out var turret, out end);
            return CanHit(target, shot) || CanHit(unit, shot) || CanHit(turret, shot);
        }

        private void FindTargets(PrimaryWeaponCatalog.Profile shot, out CarController target, out MissionUnit unit, out SpaceTurret turret, out Vector3 end)
        {
            Vector3 origin = owner.transform.position + Vector3.up;
            float arc = shot.Mode == WeaponMode.Pulse ? 180f : shot.Arc;
            target = TargetFinder.Find(car, origin, owner.transform.forward, arc, 0.1f, shot.Range);
            unit = target == null ? TargetFinder.FindUnit(car, origin, owner.transform.forward, arc, 0.1f, shot.Range) : null;
            turret = target == null && unit == null ? TargetFinder.FindTurret(origin, owner.transform.forward, arc, 0.1f, shot.Range) : null;
            end = target != null ? target.transform.position + Vector3.up * 0.5f
                : unit != null ? unit.AimPointFrom(origin) : turret != null ? turret.AimPoint : origin + owner.transform.forward * shot.Range;
            if (target == null && Physics.Linecast(origin, end, out var obstacle, TargetFinder.ObstacleMask, QueryTriggerInteraction.Ignore)) end = obstacle.point;
        }

        public void Fire(PrimaryWeaponCatalog.Profile shot, float damage, int tick)
        {
            var director = RaceDirector.Current ?? RaceDirector.Instance;
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

            FindTargets(shot, out var target, out var unit, out var turret, out var end);
            rig?.Play(end, shot);

            if (shot.Mode == WeaponMode.Melee)
            {
                pendingTarget = target;
                pendingUnit = unit;
                pendingTurret = turret;
                pendingShot = shot;
                pendingDamage = damage;
                impactTick = tick + SimulationDriver.SecondsToTicks(Mathf.Min(0.18f, shot.Interval * 0.45f));
            }
            else if (shot.Mode == WeaponMode.Beam)
            {
                if (CanHit(target, shot)) Hit(target, shot, damage);
                else if (CanHit(unit, shot)) HitUnit(unit, damage);
                else if (CanHit(turret, shot)) HitTurret(turret, damage);
            }
            else
            {
                var cars = director.Cars;
                for (int i = 0; i < cars.Count; i++)
                    if (CanHit(cars[i], shot)) Hit(cars[i], shot, damage);
                var units = MissionUnit.All;
                for (int i = units.Count - 1; i >= 0; i--)
                    if (i < units.Count && CanHit(units[i], shot)) HitUnit(units[i], damage);
                // Destroying a turret removes it from All, so iterate backwards.
                var turrets = SpaceTurret.All;
                for (int i = turrets.Count - 1; i >= 0; i--)
                    if (i < turrets.Count && CanHit(turrets[i], shot)) HitTurret(turrets[i], damage);
            }
        }

        private bool CanHit(CarController target, PrimaryWeaponCatalog.Profile shot)
        {
            if (target == null || target.gameObject == owner || !target.isActiveAndEnabled) return false;
            if (!Teams.Hostile(owner, target.gameObject)) return false;
            var health = target.GetComponent<CarHealth>();
            if (health == null || health.IsDead || health.IsInvulnerable) return false;
            Vector3 origin = owner.transform.position + Vector3.up;
            Vector3 end = target.transform.position + Vector3.up * 0.5f;
            Vector3 offset = end - origin;
            if (offset.sqrMagnitude > shot.Range * shot.Range || offset.sqrMagnitude < 0.001f) return false;
            if (shot.Mode != WeaponMode.Pulse && Vector3.Angle(owner.transform.forward, offset) > shot.Arc) return false;
            return !Physics.Linecast(origin, end, TargetFinder.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        private bool CanHit(MissionUnit unit, PrimaryWeaponCatalog.Profile shot)
        {
            if (unit == null || !unit.Targetable || !Teams.Hostile(owner, unit.gameObject)) return false;
            Vector3 origin = owner.transform.position + Vector3.up;
            Vector3 end = unit.AimPointFrom(origin);
            Vector3 offset = end - origin;
            float reach = shot.Range + unit.Radius;
            if (offset.sqrMagnitude > reach * reach || offset.sqrMagnitude < 0.001f) return false;
            if (shot.Mode != WeaponMode.Pulse && Vector3.Angle(owner.transform.forward, offset) > shot.Arc + 10f) return false;
            return !Physics.Linecast(origin, end, TargetFinder.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        private void HitUnit(MissionUnit unit, float damage)
        {
            Vector3 point = unit.AimPointFrom(owner.transform.position + Vector3.up);
            if (!unit.TakeHit(owner, damage, point)) return;
            RaceDirector.Current?.CountHit(owner, unit.gameObject);
            EffectPool.Current?.PlayShellImpact(point);
        }

        private bool CanHit(SpaceTurret turret, PrimaryWeaponCatalog.Profile shot)
        {
            if (turret == null || !turret.IsAlive || !turret.isActiveAndEnabled) return false;
            Vector3 origin = owner.transform.position + Vector3.up;
            Vector3 offset = turret.AimPoint - origin;
            if (offset.sqrMagnitude > shot.Range * shot.Range || offset.sqrMagnitude < 0.001f) return false;
            if (shot.Mode != WeaponMode.Pulse && Vector3.Angle(owner.transform.forward, offset) > shot.Arc) return false;
            return !Physics.Linecast(origin, turret.AimPoint, TargetFinder.ObstacleMask, QueryTriggerInteraction.Ignore);
        }

        private void HitTurret(SpaceTurret turret, float damage)
        {
            Vector3 point = turret.AimPoint;
            if (!turret.TakeHit(owner, damage)) return;
            RaceDirector.Current?.CountHit(owner, turret.gameObject);
            EffectPool.Current?.PlayShellImpact(point);
            if (PrimaryWeaponCatalog.Get(type).Mode == WeaponMode.Melee) WeaponSounds.MeleeHit(type, point);
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
