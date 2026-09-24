using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Roof-mounted cannon. Builds its own turret/barrel visuals at start. The turret swings and the
    /// barrel tilts toward the rival closest to the car's nose inside a sector ahead (with lead and
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
        private int parkedAmmo, parkedReloadDoneTick = -1;   // the cannon magazine while the machine gun is out
        private int reloadDoneTick = -1;
        private int nextFireTick;
        private bool wantsFire;
        private float recoilOffset;
        private Vector3 barrelRestPosition;
        private Quaternion turretRestRotation, barrelRestRotation;
        private float turretYaw, barrelPitch;
        private CarController turretTarget;
        private Material shellMaterial;

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

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            car = GetComponent<CarController>();
            status = GetComponent<CarStatus>();
            if (status == null) status = gameObject.AddComponent<CarStatus>();
            weapons = GetComponent<CarWeapons>();
            if (weapons == null) weapons = gameObject.AddComponent<CarWeapons>();
            if (barrel == null || muzzle == null) BuildTurret();
            barrelRestPosition = barrel.localPosition;
            barrelRestRotation = barrel.localRotation;
            if (turret == null && barrel.parent != transform) turret = barrel.parent;
            if (turret != null) turretRestRotation = turret.localRotation;
            ApplyPrimaryProfile(weapons != null ? weapons.primaryType : PrimaryWeaponType.Cannon);
        }

        private const float CannonInterval = 0.4f, CannonDamage = 25f, MachineGunInterval = 0.125f, MachineGunDamage = 8f;
        private const int CannonMagazine = 5;

        /// <summary>Resets the stats to the base of the primary (before upgrades). Refills the magazine.</summary>
        public void ApplyPrimaryProfile(PrimaryWeaponType type)
        {
            profile = type;
            parkedAmmo = CannonMagazine;
            parkedReloadDoneTick = -1;
            if (type == PrimaryWeaponType.MachineGun)
            {
                fireInterval = MachineGunInterval; // 8 shots/sec
                shellSpeed = 120f;
                shellDamage = MachineGunDamage;
                recoilForce = 300f;
                magazineSize = 0; // heat/overheat governed
                reloadSeconds = 0f;
                ammo = 0;
                reloadDoneTick = -1;
            }
            else // Cannon
            {
                fireInterval = CannonInterval;
                shellSpeed = 70f;
                shellDamage = CannonDamage;
                recoilForce = 1500f;
                magazineSize = CannonMagazine;
                reloadSeconds = 4f;
                ammo = magazineSize;
                reloadDoneTick = -1;
            }
            AmmoChanged?.Invoke(Ammo, AmmoMax);
        }

        /// <summary>
        /// Mid-race swap: carries the upgrade and build multipliers over to the new primary, parks the cannon's
        /// magazine (its reload keeps counting) instead of refilling it, and holds fire until <paramref name="readyTick"/>.
        /// </summary>
        public void SwitchPrimaryProfile(PrimaryWeaponType type, int readyTick)
        {
            if (type == profile) return;
            bool fromCannon = profile == PrimaryWeaponType.Cannon;
            float damageScale = shellDamage / (fromCannon ? CannonDamage : MachineGunDamage);
            float intervalScale = fireInterval / (fromCannon ? CannonInterval : MachineGunInterval);
            int keptAmmo = fromCannon ? ammo : parkedAmmo;
            int keptReload = fromCannon ? reloadDoneTick : parkedReloadDoneTick;
            ApplyPrimaryProfile(type);
            shellDamage *= damageScale;
            fireInterval *= intervalScale;
            if (type == PrimaryWeaponType.Cannon) { ammo = keptAmmo; reloadDoneTick = keptReload; }
            else { parkedAmmo = keptAmmo; parkedReloadDoneTick = keptReload; }
            nextFireTick = Mathf.Max(nextFireTick, readyTick);
            AmmoChanged?.Invoke(Ammo, AmmoMax);
        }

        public void RefillAmmo()
        {
            ammo = magazineSize;
            reloadDoneTick = -1;
            parkedAmmo = CannonMagazine;
            parkedReloadDoneTick = -1;
            AmmoChanged?.Invoke(Ammo, AmmoMax);
        }

        private void Update()
        {
            // Barrel recoil spring-back (visual only)
            recoilOffset = Mathf.MoveTowards(recoilOffset, 0f, Time.deltaTime * 1.2f);
            barrel.localPosition = barrelRestPosition - barrel.localRotation * Vector3.forward * recoilOffset;
        }

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
            if (reloadDoneTick >= 0 && tick >= reloadDoneTick) RefillAmmo();
            StepTurret(tick);
            if (wantsFire) TryFire(tick);
        }

        // Runs in the simulation step before firing, so the shell leaves along the turned barrel.
        private void StepTurret(int tick)
        {
            if (turret == null || turretYawLimit <= 0f) return;
            if (!enabled) turretTarget = null;
            else if ((tick + (car != null ? car.CarId : 0)) % 5 == 0)   // re-acquire 10×/s, staggered per car
                turretTarget = TargetFinder.Find(car, turret.position, transform.forward, turretYawLimit, turretMinRange, turretMaxRange, shellSpeed, closestToAxis: true);

            float desiredYaw = 0f, desiredPitch = 0f;
            if (turretTarget != null && turretTarget.isActiveAndEnabled)
            {
                Vector3 aim = turretTarget.transform.position + Vector3.up * 0.5f;
                float speed = Mathf.Max(1f, shellSpeed);
                // Lead by the target's velocity relative to ours: the shell inherits our velocity.
                Vector3 relative = (turretTarget.Body != null ? turretTarget.Body.linearVelocity : Vector3.zero) - (rb != null ? rb.linearVelocity : Vector3.zero);
                aim += relative * (Vector3.Distance(aim, barrel.position) / speed);
                Vector3 local = turret.parent.InverseTransformDirection(aim - barrel.position);
                float flat = Mathf.Sqrt(local.x * local.x + local.z * local.z), flight = flat / speed;
                float drop = -0.5f * Physics.gravity.y * flight * flight;   // aim above the target by what the shell falls
                desiredYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -turretYawLimit, turretYawLimit);
                desiredPitch = Mathf.Clamp(-Mathf.Atan2(local.y + drop, flat) * Mathf.Rad2Deg, -barrelPitchLimit, barrelPitchLimit);
            }
            float step = turretTurnSpeed * SimulationDriver.FixedStep;
            turretYaw = Mathf.MoveTowards(turretYaw, desiredYaw, step);
            barrelPitch = Mathf.MoveTowards(barrelPitch, desiredPitch, step);
            turret.localRotation = turretRestRotation * Quaternion.Euler(0f, turretYaw, 0f);
            barrel.localRotation = barrelRestRotation * Quaternion.Euler(barrelPitch, 0f, 0f);
        }

        public bool TryFire() => TryFire(SimulationDriver.Instance.Tick);

        public bool TryFire(int tick)
        {
            if (!enabled || tick < nextFireTick) return false;
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

            ProjectileKind pKind = (weapons != null && weapons.primaryType == PrimaryWeaponType.MachineGun)
                ? ProjectileKind.MachineGun
                : ProjectileKind.Shell;

            Vector3 direction = muzzle != null ? muzzle.forward : transform.forward;
            Vector3 inherited = rb != null ? rb.linearVelocity : Vector3.zero;
            Vector3 muzzlePos = muzzle != null ? muzzle.position : transform.position;

            var director = RaceDirector.Current ?? RaceDirector.Instance;
            if (director != null)
            {
                director.RequestProjectile(new ProjectileSpawn
                {
                    Owner = gameObject,
                    OwnerId = car != null ? car.CarId : 0,
                    Position = muzzlePos,
                    Velocity = direction * shellSpeed + inherited,
                    Damage = shellDamage,
                    Tick = tick,
                    Kind = pKind,
                    Material = shellMaterial
                });
            }

            recoilOffset = 0.18f;
            var pool = EffectPool.Current ?? EffectPool.Instance;
            if (pool != null) pool.PlayMuzzle(muzzlePos);
            var sfx = SfxPlayer.Current ?? SfxPlayer.Instance;
            if (sfx != null) sfx.Play(SfxSynth.Shot, muzzlePos, 0.9f, 0.95f + 0.1f * (tick % 3));
            if (rb != null) rb.AddForceAtPosition(-direction * recoilForce, muzzlePos, ForceMode.Impulse);

            return true;
        }

        private void BuildTurret()
        {
            Material dark = FindMaterialOnCar("carbon", "dark") ?? MakeMaterial(new Color(0.12f, 0.12f, 0.13f));
            shellMaterial = MakeMaterial(new Color(1f, 0.8f, 0.25f));

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

        // TODO(M5): serialized material assets instead of Shader.Find (stripped in builds).
        private static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }
    }
}
