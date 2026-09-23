using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Roof-mounted cannon. Builds its own turret/barrel visuals at start. Firing intent comes
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

        [Header("References (auto-created if empty)")]
        public Transform barrel;
        public Transform muzzle;

        private Rigidbody rb;
        private CarController car;
        private CarStatus status;
        private CarWeapons weapons;
        private int ammo;
        private int reloadDoneTick = -1;
        private int nextFireTick;
        private bool wantsFire;
        private float recoilOffset;
        private Vector3 barrelRestPosition;
        private Material shellMaterial;

        public int FireIntervalTicks => SimulationDriver.SecondsToTicks(fireInterval);
        public bool IsReady => SimulationDriver.Instance.Tick >= nextFireTick && (magazineSize <= 0 || ammo > 0);
        public int Ammo => magazineSize <= 0 ? int.MaxValue : ammo;
        public int AmmoMax => magazineSize;
        public bool IsReloading => reloadDoneTick >= 0;
        /// <summary>0..1 while reloading, 1 when ready.</summary>
        public float ReloadProgress(int tick) => reloadDoneTick < 0 ? 1f : 1f - Mathf.Clamp01((reloadDoneTick - tick) / (float)SimulationDriver.SecondsToTicks(reloadSeconds));
        public event System.Action<int, int> AmmoChanged;   // ammo, max

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
            ApplyPrimaryProfile(weapons != null ? weapons.primaryType : PrimaryWeaponType.Cannon);
        }

        public void ApplyPrimaryProfile(PrimaryWeaponType type)
        {
            if (type == PrimaryWeaponType.MachineGun)
            {
                fireInterval = 0.125f; // 8 shots/sec
                shellSpeed = 120f;
                shellDamage = 8f;
                recoilForce = 300f;
                magazineSize = 0; // heat/overheat governed
                reloadSeconds = 0f;
                ammo = 0;
                reloadDoneTick = -1;
            }
            else // Cannon
            {
                fireInterval = 0.4f;
                shellSpeed = 70f;
                shellDamage = 25f;
                recoilForce = 1500f;
                magazineSize = 5;
                reloadSeconds = 4f;
                ammo = magazineSize;
                reloadDoneTick = -1;
            }
            AmmoChanged?.Invoke(Ammo, AmmoMax);
        }

        public void RefillAmmo()
        {
            ammo = magazineSize;
            reloadDoneTick = -1;
            AmmoChanged?.Invoke(Ammo, AmmoMax);
        }

        private void Update()
        {
            // Barrel recoil spring-back (visual only)
            recoilOffset = Mathf.MoveTowards(recoilOffset, 0f, Time.deltaTime * 1.2f);
            barrel.localPosition = barrelRestPosition - Vector3.forward * recoilOffset;
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
            if (wantsFire) TryFire(tick);
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
