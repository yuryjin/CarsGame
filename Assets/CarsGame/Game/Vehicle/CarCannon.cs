using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Roof-mounted cannon. Builds its own turret/barrel visuals at start. The turret swings and the
    /// barrel tilts toward the mouse point for the player, or a rival in front for AI (with lead and
    /// shell drop), so shells leave along the barrel. Firing intent comes
    /// from the car's <see cref="Core.Sim.DriveCommand"/> (or an AI gunner via <see cref="TryFire"/>);
    /// the cooldown is counted in simulation ticks and shells are spawned through the
    /// <see cref="RaceDirector"/> pool.
    /// </summary>
    public class CarCannon : MonoBehaviour
    {
        [Header("Firing")]
        public float fireInterval = 0.4f;
        public float shellSpeed = 70f;
        public float shellDamage = 25f;
        public float recoilForce = 1500f;
        [Tooltip("Shots per magazine. 0 = unlimited.")]
        public int magazineSize = 5;
        [Tooltip("Seconds to refill an empty magazine.")]
        public float reloadSeconds = 4f;

        [Header("Mount")]
        public Vector3 turretLocalPosition = new Vector3(0f, 1.12f, -0.45f);
        public float barrelLength = 1.3f;
        [Tooltip("Event rule (mines only): the cannon never fires this race. Separate from enabled, which the wreck toggles.")]
        public bool locked;

        [Header("Turret")]
        [Tooltip("The turret turns toward a rival up to this many degrees off the car's nose. 0 = fixed forward.")]
        public float turretYawLimit = 30f;
        [Tooltip("Barrel tilt limit (deg, up and down): the muzzle sits above rivals' roofs, so a level barrel overshoots them.")]
        public float barrelPitchLimit = 12f;
        [Tooltip("Turret turn and barrel tilt speed (deg/s).")]
        public float turretTurnSpeed = 180f;
        public float turretMinRange = 4f, turretMaxRange = 45f;

        [Header("References (auto-created if empty)")]
        [Tooltip("Yaw pivot; defaults to the barrel's parent.")]
        public Transform turret;
        public Transform barrel;
        public Transform muzzle;

        private Rigidbody rb;
        private CarController car;
        private CarStatus status;
        private CarWeapons weapons;
        private int ammo;
        private PrimaryWeaponType profile;
        private readonly int[] parkedAmmo = new int[PrimaryWeaponCatalog.Count];
        private readonly int[] parkedReloadDoneTick = new int[PrimaryWeaponCatalog.Count];
        private int reloadDoneTick = -1;
        private int nextFireTick;
        private bool wantsFire;
        private float recoilOffset;
        private Vector3 barrelRestPosition;
        private Quaternion turretRestRotation, barrelRestRotation;
        private float turretYaw, barrelPitch;
        private CarController turretTarget;
        private MissionUnit turretUnit;
        private SpaceTurret turretHazard;
        private bool aimCommitted, aimAllowsHoming;
        private Vector3 aimCommitPoint;
        private float aimCommitYawLimit;
        private Material shellMaterial;
        private SpecialWeaponAttack specialAttack;

        public int FireIntervalTicks => SimulationDriver.SecondsToTicks(fireInterval);
        public bool IsReady => SimulationDriver.Instance.Tick >= nextFireTick && (magazineSize <= 0 || ammo > 0);
        public int Ammo => magazineSize <= 0 ? int.MaxValue : ammo;
        public int AmmoMax => magazineSize;
        public bool IsReloading => reloadDoneTick >= 0;
        /// <summary>0..1 while reloading, 1 when ready.</summary>
        public float ReloadProgress(int tick) => reloadDoneTick < 0 ? 1f : 1f - Mathf.Clamp01((reloadDoneTick - tick) / (float)SimulationDriver.SecondsToTicks(reloadSeconds));
        public event System.Action<int, int> AmmoChanged;   // ammo, max
        /// <summary>Turret yaw off the car's nose (deg, + = right).</summary>
        public float TurretYaw => turretYaw;
        /// <summary>Barrel tilt (deg, + = down).</summary>
        public float BarrelPitch => barrelPitch;
        /// <summary>The rival the turret is tracking, else null.</summary>
        public CarController TurretTarget => turretTarget;
        /// <summary>The mission unit the turret is tracking when no car is in its sector, else null.</summary>
        public MissionUnit TurretUnit => turretUnit;
        /// <summary>True while an AI gunner or mouse holds the barrel on a world point.</summary>
        public bool HasAimCommit => aimCommitted;
        /// <summary>World point the barrel tracks while <see cref="HasAimCommit"/> is set.</summary>
        public Vector3 AimCommitPoint => aimCommitPoint;

        /// <summary>
        /// Where to aim so a shell of <see cref="shellSpeed"/> meets <paramref name="target"/>.
        /// Gravity drop is applied later, when the barrel tilts. <paramref name="stripLateral"/>
        /// drops only the sideways part of the lead (the lane the target is already leaving).
        /// </summary>
        public Vector3 SolveAimPoint(CarController target, bool stripLateral)
        {
            Vector3 origin = barrel != null ? barrel.position : transform.position;
            Vector3 aim = target.transform.position + Vector3.up * 0.5f;
            float speed = Mathf.Max(1f, shellSpeed);
            Vector3 relative = (target.Body != null ? target.Body.linearVelocity : Vector3.zero) - (rb != null ? rb.linearVelocity : Vector3.zero);
            if (stripLateral)
            {
                Vector3 right = transform.right;
                right.y = 0f;
                if (right.sqrMagnitude > 1e-6f)
                {
                    right.Normalize();
                    relative -= right * Vector3.Dot(relative, right);
                }
            }
            return aim + relative * (Vector3.Distance(aim, origin) / speed);
        }

        /// <summary>Lead for a mission unit (drone, trooper, a point on the boss's hull nearest the barrel).</summary>
        public Vector3 SolveAimPoint(MissionUnit target)
        {
            Vector3 origin = barrel != null ? barrel.position : transform.position;
            Vector3 aim = target.AimPointFrom(origin);
            Vector3 relative = target.Velocity - (rb != null ? rb.linearVelocity : Vector3.zero);
            return aim + relative * (Vector3.Distance(aim, origin) / Mathf.Max(1f, shellSpeed));
        }

        /// <summary>
        /// Hold the barrel on a world point until <see cref="ClearAimCommit"/>. <paramref name="yawLimit"/>
        /// widens the turret sector while the commit lasts (player mouse aim); it never narrows it.
        /// </summary>
        public void CommitAim(Vector3 worldPoint, bool allowHoming = false, float yawLimit = 0f)
        {
            aimCommitted = true;
            aimAllowsHoming = allowHoming;
            aimCommitPoint = worldPoint;
            aimCommitYawLimit = yawLimit;
        }

        /// <summary>Turret sector right now: the body's arc, widened by an aim commit that asks for more.</summary>
        public float CurrentYawLimit => aimCommitted ? Mathf.Max(turretYawLimit, aimCommitYawLimit) : turretYawLimit;

        public void ClearAimCommit() { aimCommitted = false; }

        public bool TryGetSpecialAim(out Vector3 point)
        {
            point = transform.position;
            return specialAttack != null && specialAttack.TryGetAimPoint(PrimaryWeaponCatalog.Get(profile), out point);
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            car = GetComponent<CarController>();
            status = GetComponent<CarStatus>();
            if (status == null) status = gameObject.AddComponent<CarStatus>();
            weapons = GetComponent<CarWeapons>();
            if (weapons == null) weapons = gameObject.AddComponent<CarWeapons>();
            if (barrel == null || muzzle == null) shellMaterial = WeaponArt.Material(new Color(1f, 0.8f, 0.25f));
            EnsureTurretVisuals();
            barrelRestPosition = barrel.localPosition;
            barrelRestRotation = barrel.localRotation;
            if (turret == null && barrel.parent != transform) turret = barrel.parent;
            if (turret != null) turretRestRotation = turret.localRotation;
            ApplyPrimaryProfile(weapons != null ? weapons.primaryType : PrimaryWeaponType.Cannon);
        }

        /// <summary>Initial loadout setup; resets all magazines before upgrades are applied.</summary>
        public void ApplyPrimaryProfile(PrimaryWeaponType type)
        {
            for (int i = 0; i < PrimaryWeaponCatalog.Count; i++)
            {
                parkedAmmo[i] = PrimaryWeaponCatalog.Get((PrimaryWeaponType)i).Magazine;
                parkedReloadDoneTick[i] = -1;
            }
            ConfigurePrimary(type);
            ammo = magazineSize;
            reloadDoneTick = -1;
            AmmoChanged?.Invoke(Ammo, AmmoMax);
        }

        private void ConfigurePrimary(PrimaryWeaponType type)
        {
            profile = PrimaryWeaponCatalog.IsValid(type) ? type : PrimaryWeaponType.Cannon;
            var stats = PrimaryWeaponCatalog.Get(profile);
            fireInterval = stats.Interval;
            shellSpeed = stats.Speed;
            shellDamage = stats.Damage;
            recoilForce = stats.Recoil;
            magazineSize = stats.Magazine;
            reloadSeconds = stats.Reload;
            if (specialAttack == null) specialAttack = new SpecialWeaponAttack(gameObject);
            specialAttack.Configure(profile);
            if (turret != null) turret.gameObject.SetActive(stats.Mode == WeaponMode.Projectile);
        }

        // Banana is a skin: changing its appearance must not grant another magazine.
        private static int MagazineSlot(PrimaryWeaponType type) => (int)(type == PrimaryWeaponType.GiantBanana ? PrimaryWeaponType.MineDispenser : type);

        /// <summary>Preserves upgrades and each weapon's magazine and reload deadline.</summary>
        public void SwitchPrimaryProfile(PrimaryWeaponType type, int readyTick)
        {
            if (!PrimaryWeaponCatalog.IsValid(type) || type == profile) return;
            var previous = PrimaryWeaponCatalog.Get(profile);
            float damageScale = shellDamage / previous.Damage;
            float intervalScale = fireInterval / previous.Interval;
            parkedAmmo[MagazineSlot(profile)] = ammo;
            parkedReloadDoneTick[MagazineSlot(profile)] = reloadDoneTick;
            ConfigurePrimary(type);
            shellDamage *= damageScale;
            fireInterval *= intervalScale;
            ammo = parkedAmmo[MagazineSlot(profile)];
            reloadDoneTick = parkedReloadDoneTick[MagazineSlot(profile)];
            nextFireTick = Mathf.Max(nextFireTick, readyTick);
            AmmoChanged?.Invoke(Ammo, AmmoMax);
        }

        public void RefillAmmo()
        {
            for (int i = 0; i < PrimaryWeaponCatalog.Count; i++)
            {
                parkedAmmo[i] = PrimaryWeaponCatalog.Get((PrimaryWeaponType)i).Magazine;
                parkedReloadDoneTick[i] = -1;
            }
            ReloadCurrentMagazine();
        }

        private void ReloadCurrentMagazine()
        {
            ammo = magazineSize;
            reloadDoneTick = -1;
            AmmoChanged?.Invoke(Ammo, AmmoMax);
        }

        private void Update()
        {
            // Barrel recoil spring-back (visual only)
            recoilOffset = Mathf.MoveTowards(recoilOffset, 0f, Time.deltaTime * 1.2f);
            if (barrel != null) barrel.localPosition = barrelRestPosition - barrel.localRotation * Vector3.forward * recoilOffset;
            specialAttack?.UpdateVisual();
        }

        private void OnDisable() { wantsFire = false; specialAttack?.Cancel(); }

        /// <summary>Level input: true while the fire button is held. Set by the car every tick.</summary>
        public void SetWantsFire(bool value)
        {
            wantsFire = value;
            if (weapons == null) weapons = GetComponent<CarWeapons>();
            if (weapons != null) weapons.SetWantsPrimary(value);
        }

        /// <summary>Called by the owning <see cref="CarController"/> from its simulation step.</summary>
        public void SimulationStep(int tick)
        {
            if (reloadDoneTick >= 0 && tick >= reloadDoneTick) ReloadCurrentMagazine();
            specialAttack?.Step(tick);
            if (PrimaryWeaponCatalog.Get(profile).Mode == WeaponMode.Projectile) StepTurret(tick);
            if (wantsFire) TryFire(tick);
        }

        // Runs in the simulation step before firing, so the shell leaves along the turned barrel.
        private void StepTurret(int tick)
        {
            if (turret == null || CurrentYawLimit <= 0f) return;
            Vector3 aim = default;
            bool haveAim = false;
            if (aimCommitted)
            {
                aim = aimCommitPoint;
                haveAim = true;
            }
            else
            {
                if (!enabled) { turretTarget = null; turretUnit = null; turretHazard = null; }
                else if ((tick + (car != null ? car.CarId : 0)) % 5 == 0)   // re-acquire 10×/s, staggered per car
                {
                    // From the muzzle, not the roof pivot: the pivot sits inside the hull, so a linecast from it
                    // hits the car or the road and the turret never acquires.
                    Vector3 origin = muzzle != null ? muzzle.position : turret.position;
                    turretTarget = TargetFinder.Find(car, origin, transform.forward, turretYawLimit, turretMinRange, turretMaxRange, shellSpeed, closestToAxis: true);
                    turretUnit = turretTarget == null ? TargetFinder.FindUnit(car, origin, transform.forward, turretYawLimit, turretMinRange, turretMaxRange, closestToAxis: true) : null;
                    turretHazard = turretTarget == null && turretUnit == null
                        ? TargetFinder.FindTurret(origin, transform.forward, turretYawLimit, turretMinRange, turretMaxRange) : null;
                }
                if (turretTarget != null && turretTarget.isActiveAndEnabled)
                {
                    aim = SolveAimPoint(turretTarget, false);
                    haveAim = true;
                }
                else if (turretUnit != null && turretUnit.Targetable)
                {
                    aim = SolveAimPoint(turretUnit);
                    haveAim = true;
                }
                else if (turretHazard != null && turretHazard.IsAlive)
                {
                    aim = turretHazard.AimPoint;
                    haveAim = true;
                }
            }

            float desiredYaw = 0f, desiredPitch = 0f;
            if (haveAim) DesiredAngles(aim, out desiredYaw, out desiredPitch);
            float step = turretTurnSpeed * SimulationDriver.FixedStep;
            turretYaw = Mathf.MoveTowards(turretYaw, desiredYaw, step);
            barrelPitch = Mathf.MoveTowards(barrelPitch, desiredPitch, step);
            turret.localRotation = turretRestRotation * Quaternion.Euler(0f, turretYaw, 0f);
            barrel.localRotation = barrelRestRotation * Quaternion.Euler(barrelPitch, 0f, 0f);
        }

        public bool IsAlignedWith(Vector3 aimPoint)
        {
            if (turret == null || barrel == null) return false;
            DesiredAngles(aimPoint, out float yaw, out float pitch);
            return Mathf.Abs(turretYaw - yaw) <= 1.5f && Mathf.Abs(barrelPitch - pitch) <= 1.5f;
        }

        private void DesiredAngles(Vector3 aimPoint, out float desiredYaw, out float desiredPitch)
        {
            Vector3 local = turret.parent.InverseTransformDirection(aimPoint - barrel.position);
            float flat = Mathf.Sqrt(local.x * local.x + local.z * local.z);
            float speed = Mathf.Max(1f, shellSpeed);
            float flight = flat / speed;
            float drop = -0.5f * Physics.gravity.y * flight * flight;   // aim above the point by what the shell falls
            float yawLimit = CurrentYawLimit;
            desiredYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -yawLimit, yawLimit);
            desiredPitch = Mathf.Clamp(-Mathf.Atan2(local.y + drop, flat) * Mathf.Rad2Deg, -barrelPitchLimit, barrelPitchLimit);
        }

        public bool TryFire() => TryFire(SimulationDriver.Instance.Tick);

        public bool TryFire(int tick)
        {
            if (!enabled || locked || tick < nextFireTick) return false;
            if (status == null) status = GetComponent<CarStatus>();
            if (status != null && status.IsJammed) return false;
            if (weapons == null) weapons = GetComponent<CarWeapons>();
            if (weapons != null && weapons.IsOverheated) return false;
            if (magazineSize > 0 && ammo <= 0) return false;
            nextFireTick = tick + FireIntervalTicks;
            if (magazineSize > 0)
            {
                ammo--;
                if (ammo <= 0) reloadDoneTick = tick + SimulationDriver.SecondsToTicks(reloadSeconds);
                AmmoChanged?.Invoke(Ammo, AmmoMax);
            }

            if (weapons != null) weapons.AddHeat(weapons.heatPerShot);

            var shot = PrimaryWeaponCatalog.Get(profile);
            if (shot.Mode != WeaponMode.Projectile)
            {
                if (specialAttack == null) { specialAttack = new SpecialWeaponAttack(gameObject); specialAttack.Configure(profile); }
                WeaponSounds.Fire(profile, transform.position, tick);
                specialAttack.Fire(shot, shellDamage, tick);
                return true;
            }

            Vector3 direction = muzzle != null ? muzzle.forward : transform.forward;
            Vector3 inherited = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 muzzlePos = muzzle != null ? muzzle.position : transform.position;

            var director = RaceDirector.Current ?? RaceDirector.Instance;
            if (director != null)
            {
                var homingTarget = shot.Homing && (!aimCommitted || aimAllowsHoming) ? TargetFinder.Find(car, muzzlePos, direction, aimCommitted ? 5f : 35f, 1f, 80f) : null;
                for (int pellet = 0; pellet < shot.Pellets; pellet++)
                {
                    float angle = shot.Pellets == 1 ? 0f : Mathf.Lerp(-shot.Spread * 0.5f, shot.Spread * 0.5f, pellet / (float)(shot.Pellets - 1));
                    Vector3 shotDirection = Quaternion.AngleAxis(angle, muzzle != null ? muzzle.up : transform.up) * direction;
                    director.RequestProjectile(new ProjectileSpawn
                    {
                        Owner = gameObject,
                        OwnerId = car != null ? car.CarId : 0,
                        Position = muzzlePos,
                        Velocity = shotDirection * shellSpeed + inherited,
                        Damage = shellDamage,
                        Tick = tick,
                        Kind = shot.Kind,
                        Weapon = profile,
                        Impact = shot.Impact,
                        Ricochets = shot.Ricochets,
                        HomingTarget = homingTarget != null ? homingTarget.transform : null,
                        Status = shot.Status,
                        StatusDuration = shot.StatusSeconds,
                        Material = shellMaterial
                    });
                }
            }

            recoilOffset = 0.18f;
            var pool = EffectPool.Current ?? EffectPool.Instance;
            if (pool != null) pool.PlayMuzzle(muzzlePos);
            WeaponSounds.Fire(profile, muzzlePos, tick);
            if (rb != null) rb.AddForceAtPosition(-direction * recoilForce, muzzlePos, ForceMode.Impulse);

            return true;
        }

        /// <summary>Also used by the inactive garage template; creates visuals without registering a live car.</summary>
        public void EnsureTurretVisuals()
        {
            if (barrel == null || muzzle == null) BuildTurret();
            if (turret == null && barrel != null && barrel.parent != transform) turret = barrel.parent;
        }

        private void BuildTurret()
        {
            Material dark = FindMaterialOnCar("carbon", "dark") ?? WeaponArt.Material(new Color(0.12f, 0.12f, 0.13f));

            var turret = new GameObject("Cannon_Turret").transform;
            turret.gameObject.layer = gameObject.layer;
            turret.SetParent(transform, false);
            turret.localPosition = turretLocalPosition;
            turret.localRotation = Quaternion.identity;

            // Base ring on the roof
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Cannon_Base";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(turret, false);
            ring.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            ring.transform.localScale = new Vector3(0.55f, 0.05f, 0.55f);
            ring.GetComponent<Renderer>().sharedMaterial = dark;

            // Turret housing
            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "Cannon_Housing";
            Destroy(housing.GetComponent<Collider>());
            housing.transform.SetParent(turret, false);
            housing.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            housing.transform.localScale = new Vector3(0.34f, 0.2f, 0.46f);
            housing.GetComponent<Renderer>().sharedMaterial = dark;

            // Barrel (pivot at the rear so recoil slides it back)
            var barrelPivot = new GameObject("Cannon_Barrel").transform;
            barrelPivot.SetParent(turret, false);
            barrelPivot.localPosition = new Vector3(0f, 0.1f, 0.1f);
            barrelPivot.localRotation = Quaternion.identity;

            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "Barrel_Tube";
            Destroy(tube.GetComponent<Collider>());
            tube.transform.SetParent(barrelPivot, false);
            tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tube.transform.localPosition = new Vector3(0f, 0f, barrelLength * 0.5f);
            tube.transform.localScale = new Vector3(0.11f, barrelLength * 0.5f, 0.11f);
            tube.GetComponent<Renderer>().sharedMaterial = dark;

            var brake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            brake.name = "Muzzle_Brake";
            Destroy(brake.GetComponent<Collider>());
            brake.transform.SetParent(barrelPivot, false);
            brake.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            brake.transform.localPosition = new Vector3(0f, 0f, barrelLength - 0.08f);
            brake.transform.localScale = new Vector3(0.15f, 0.08f, 0.15f);
            brake.GetComponent<Renderer>().sharedMaterial = dark;

            var muzzlePoint = new GameObject("Muzzle").transform;
            muzzlePoint.SetParent(barrelPivot, false);
            muzzlePoint.localPosition = new Vector3(0f, 0f, barrelLength + 0.25f);
            muzzlePoint.localRotation = Quaternion.identity;

            foreach (var t in turret.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = gameObject.layer;

            barrel = barrelPivot;
            muzzle = muzzlePoint;
        }

        private Material FindMaterialOnCar(params string[] nameParts)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var m = r.sharedMaterial;
                if (m == null) continue;
                string n = m.name.ToLowerInvariant();
                foreach (var part in nameParts)
                {
                    if (n.Contains(part)) return m;
                }
            }
            return null;
        }

    }
}
