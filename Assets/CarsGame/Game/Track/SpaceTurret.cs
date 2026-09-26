using System.Collections.Generic;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>
    /// Neutral track turret: locks onto the nearest visible car (player or AI alike, preferring a different car
    /// than last time), shows a red aiming laser for <see cref="AimSeconds"/>, then fires a short burst with lead.
    /// Its shells have no owner, so a wreck it causes credits nobody. Runs only while the race is on.
    /// </summary>
    public sealed class SpaceTurret : MonoBehaviour, ISimulated
    {
        // A hazard, not a killer: in a 3-lap autopilot race 9 dmg × 3 every 2.4 s at 55 m cost each car 5–10 wrecks.
        public const float Range = 45f, AimSeconds = 1.1f, CooldownSeconds = 4f;
        public const float ShellSpeed = 70f, ShellDamage = 6f, ShellImpact = 2500f;
        public const int Burst = 2;
        public const int DestroyPoints = 50;
        public const float MaxHealth = 50f;
        private static readonly List<SpaceTurret> all = new List<SpaceTurret>();
        public static IReadOnlyList<SpaceTurret> All => all;
        private const float BurstGapSeconds = 0.12f, TurnDegreesPerSecond = 150f, FireCone = 8f;

        private enum Phase { Idle, Aiming, Firing, Cooling }

        private RaceManager race;
        private Transform head, muzzle;
        private LineRenderer laser;
        private Material shellMaterial;
        private int order;
        private Phase phase;
        private CarController target;
        private int phaseTick, shotsLeft, lastTargetId;
        private Vector3 aimPoint;
        private int trackMask;
        private float health = MaxHealth;
        private readonly List<Mesh> meshes = new List<Mesh>();

        public CarController Target => target;
        public bool Aiming => phase == Phase.Aiming;
        public int ShotsFired { get; private set; }
        public int SimulationOrder => int.MaxValue - 1000 + order;   // after every car, before the director moves the shells
        public Vector3 MuzzlePosition => muzzle.position;
        public Vector3 AimPoint => head.position;
        public bool IsAlive => health > 0f;

        public static SpaceTurret Create(Transform parent, Vector3 position, Vector3 facing, int index, RaceManager race, Material solid, Material glow, Material shells)
        {
            var turret = new GameObject("SpaceTurret_" + (index + 1)).AddComponent<SpaceTurret>();
            turret.transform.SetParent(parent, false);
            turret.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.ProjectOnPlane(facing, Vector3.up), Vector3.up));
            turret.race = race;
            turret.order = index;
            turret.shellMaterial = shells;
            turret.Build(solid, glow);
            return turret;
        }

        private void Build(Material solid, Material glow)
        {
            int trackLayer = LayerMask.NameToLayer("Track");
            trackMask = trackLayer >= 0 ? 1 << trackLayer : Physics.DefaultRaycastLayers;
            var hull = new Color32(70, 64, 92, 0);
            var body = new MeshChunkWriter("TurretBase");
            LowPoly.Cylinder(body, Matrix4x4.identity, Vector3.down * 6f, 0.5f, 0.9f, 5.2f, 8, hull);
            LowPoly.Cylinder(body, Matrix4x4.identity, Vector3.down * 0.8f, 1.6f, 1.2f, 0.8f, 8, new Color32(90, 84, 118, 0));
            var ring = new MeshChunkWriter("TurretRing");
            LowPoly.Cylinder(ring, Matrix4x4.identity, Vector3.down * 0.25f, 1.65f, 1.65f, 0.12f, 12, new Color32(255, 60, 90, 255), false);
            Add(transform, body, solid, true);
            Add(transform, ring, glow, false);

            head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            head.localPosition = Vector3.up * 0.6f;
            var gun = new MeshChunkWriter("TurretHead");
            LowPoly.Box(gun, Matrix4x4.identity, Vector3.zero, new Vector3(1.5f, 1f, 1.7f), new Color32(110, 104, 140, 0));
            var barrel = Matrix4x4.TRS(new Vector3(0f, 0.05f, 0.6f), Quaternion.Euler(90f, 0f, 0f), Vector3.one);
            LowPoly.Cylinder(gun, barrel, Vector3.zero, 0.22f, 0.18f, 1.9f, 6, new Color32(45, 42, 60, 0));
            var eye = new MeshChunkWriter("TurretEye");
            LowPoly.Box(eye, Matrix4x4.identity, new Vector3(0f, 0.2f, 0.86f), new Vector3(0.9f, 0.18f, 0.05f), new Color32(255, 50, 80, 255));
            Add(head, gun, solid, true);
            Add(head, eye, glow, false);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(head, false);
            muzzle.localPosition = new Vector3(0f, 0.05f, 2.8f);

            laser = new GameObject("AimLaser").AddComponent<LineRenderer>();
            laser.transform.SetParent(transform, false);
            laser.sharedMaterial = glow;
            laser.generateLightingData = true;
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.positionCount = 2;
            laser.startColor = laser.endColor = new Color(1f, 0.12f, 0.2f, 1f);
            laser.enabled = false;
        }

        private void Add(Transform parent, MeshChunkWriter writer, Material material, bool solid)
        {
            foreach (var mesh in writer.Finish())
            {
                meshes.Add(mesh);
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
            SimulationDriver.Instance.Register(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            var driver = SimulationDriver.Current;
            if (driver != null) driver.Unregister(this);
        }

        public bool TakeHit(GameObject attacker, float damage)
        {
            var participant = attacker != null ? attacker.GetComponent<RaceParticipant>() : null;
            if (!IsAlive || damage <= 0f || participant == null || race == null ||
                (race.State != RaceState.Racing && race.State != RaceState.Finishing)) return false;
            health = Mathf.Max(0f, health - damage);
            if (health > 0f) return true;
            participant.TurretPoints += DestroyPoints;
            if (participant.isPlayer)
            {
                var hud = race.GetComponent<HudScreen>();
                if (hud != null) hud.ShowEvent(Loc.Get("combat.destroyed") + "  +" + DestroyPoints, new Color(1f, 0.45f, 0.2f));
            }
            if (EffectPool.Current != null) EffectPool.Current.PlayFlash(head.position, new Color(1f, 0.35f, 0.1f), 0.4f, 2f, 12f, 8f);
            gameObject.SetActive(false);
            return true;
        }

        public void SimulationStep(int tick)
        {
            bool live = race != null && (race.State == RaceState.Racing || race.State == RaceState.Finishing);
            if (!live) { phase = Phase.Idle; target = null; return; }
            if (phase == Phase.Cooling)
            {
                if (tick < phaseTick) return;
                phase = Phase.Idle;
            }
            if (!Valid(target)) { target = Pick(); phase = target != null ? Phase.Aiming : Phase.Idle; phaseTick = tick + SimulationDriver.SecondsToTicks(AimSeconds); }
            if (target == null) return;

            // Last 0.35 s of the laser is a fixed shot line: a quick lane change can dodge it.
            if (phase == Phase.Aiming && tick < phaseTick - SimulationDriver.SecondsToTicks(0.35f)) aimPoint = Lead(target);
            var want = Quaternion.LookRotation(aimPoint - head.position, Vector3.up);
            head.rotation = Quaternion.RotateTowards(head.rotation, want, TurnDegreesPerSecond * SimulationDriver.FixedStep);
            bool onTarget = Quaternion.Angle(head.rotation, want) < FireCone;

            if (phase == Phase.Aiming && tick >= phaseTick && onTarget) { phase = Phase.Firing; shotsLeft = Burst; phaseTick = tick; }
            if (phase != Phase.Firing || tick < phaseTick) return;
            Fire(tick);
            phaseTick = tick + SimulationDriver.SecondsToTicks(BurstGapSeconds);
            if (--shotsLeft > 0) return;
            lastTargetId = target.CarId;
            target = null;
            phase = Phase.Cooling;
            phaseTick = tick + SimulationDriver.SecondsToTicks(CooldownSeconds);
        }

        /// <summary>Whether the turret could lock onto this car now (alive, in range, in sight).</summary>
        public bool CanTarget(CarController car) => Valid(car);

        private bool Valid(CarController car)
        {
            if (car == null || !car.isActiveAndEnabled || !car.inputEnabled) return false;
            var health = car.GetComponent<CarHealth>();
            if (health != null && health.IsDead) return false;
            Vector3 centre = BodyCentre(car);
            if ((centre - muzzle.position).sqrMagnitude > Range * Range) return false;
            // The road deck and the tunnel shell hide a car.
            return !Physics.Linecast(muzzle.position, centre, trackMask, QueryTriggerInteraction.Ignore);
        }

        private CarController Pick()
        {
            var director = RaceDirector.Current;
            if (director == null) return null;
            CarController best = null;
            float bestScore = float.MaxValue;
            foreach (var car in director.Cars)
            {
                if (!Valid(car)) continue;
                // Everyone is a target: the car shot last is only picked when nobody else is in sight.
                float score = (BodyCentre(car) - muzzle.position).sqrMagnitude + (car.CarId == lastTargetId ? 1e6f : 0f);
                if (score < bestScore || (score == bestScore && best != null && car.CarId < best.CarId)) { best = car; bestScore = score; }
            }
            return best;
        }

        /// <summary>Middle of the body. The centre of mass sits low (about level with the road), so sighting it is blocked by the deck.</summary>
        private static Vector3 BodyCentre(CarController car) => car.transform.position + car.transform.up * 0.5f;

        // Where to shoot so a shell meets a car holding its velocity, lifted by the drop the shell has on the way.
        private Vector3 Lead(CarController car)
        {
            Vector3 point = BodyCentre(car);
            float t = Vector3.Distance(point, muzzle.position) / ShellSpeed;
            point += car.Body.linearVelocity * t;
            t = Vector3.Distance(point, muzzle.position) / ShellSpeed;
            return point - Physics.gravity * (0.5f * t * t);
        }

        private void Fire(int tick)
        {
            var director = RaceDirector.Current;
            if (director == null) return;
            director.RequestProjectile(new ProjectileSpawn
            {
                Owner = null,
                OwnerId = 0,
                Position = muzzle.position,
                Velocity = (aimPoint - muzzle.position).normalized * ShellSpeed,
                Damage = ShellDamage,
                Tick = tick,
                Kind = ProjectileKind.Shell,
                Weapon = PrimaryWeaponType.Cannon,
                Impact = ShellImpact,
                Material = shellMaterial
            });
            ShotsFired++;
            if (EffectPool.Current != null) EffectPool.Current.PlayFlash(muzzle.position, new Color(1f, 0.2f, 0.3f), 0.12f, 0.6f, 6f, 4f);
        }

        // Visual only: the laser shows the lock, thin while aiming and flaring just before the burst.
        private void LateUpdate()
        {
            bool show = target != null && (phase == Phase.Aiming || phase == Phase.Firing);
            laser.enabled = show;
            if (!show) return;
            laser.SetPosition(0, muzzle.position);
            laser.SetPosition(1, aimPoint);
            float width = phase == Phase.Firing ? 0.16f : 0.05f + 0.05f * Mathf.PingPong(Time.time * 8f, 1f);
            laser.startWidth = laser.endWidth = width;
        }

        private void OnDestroy()
        {
            foreach (var mesh in meshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
