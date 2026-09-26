using System;
using System.Collections.Generic;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>
    /// «Ржавый Левиафан»: a sand-crawler fortress on four track pods with a bucket wheel in front, crawling along the
    /// map's route towards the settlement. Its HP is the reactor core at the back; hull hits reach the core at a
    /// fraction, the core itself takes full damage. Turrets, flak, a mortar and a drone hangar are separate
    /// <see cref="BossPart"/>s that can be shot off. Moved by the <see cref="MissionDirector"/> in its tick.
    /// </summary>
    public sealed class BossCrawler : MonoBehaviour
    {
        public const float Length = 46f, Width = 26f, HullShare = 0.3f;
        public const float BaseSpeed = 5f;   // m/s: ~4.5 minutes from the start to the settlement

        public DesertMap Map { get; private set; }
        public float Distance { get; private set; }
        public int Phase { get; private set; } = 1;
        public bool Defeated { get; private set; }
        public bool ReachedTown { get; private set; }
        public BossPart Core { get; private set; }
        public BossPart Hull { get; private set; }
        public readonly List<BossPart> Parts = new List<BossPart>();
        public float CoreFraction => Core != null ? Core.Health / Core.MaxHealth : 0f;
        public float Progress01 => Map != null ? Distance / Map.RouteLength : 0f;
        public float RemainingMetres => Map != null ? Mathf.Max(0f, Map.RouteLength - Distance) : 0f;
        public event Action<int> PhaseChanged;
        public event Action<BossPart> PartDestroyed;

        private Rigidbody rb;
        private Transform wheel;
        private float speedScale = 1f;
        private int holdUntilTick = -1;
        private readonly Dictionary<int, int> crushTick = new Dictionary<int, int>();

        public static BossCrawler Create(DesertMap map, float startDistance, float coreHealth)
        {
            var go = new GameObject("Boss_Leviathan");
            var boss = go.AddComponent<BossCrawler>();
            boss.Map = map;
            boss.Distance = startDistance;
            map.GroundPose(map.RoutePoint(startDistance), map.RouteForward(startDistance), out var pos, out var rot);
            go.transform.SetPositionAndRotation(pos - Vector3.up * 0.6f, rot);
            boss.rb = go.AddComponent<Rigidbody>();
            boss.rb.isKinematic = true;
            boss.rb.interpolation = RigidbodyInterpolation.Interpolate;
            boss.BuildHull();
            boss.BuildParts(coreHealth);
            return boss;
        }

        // ---------- model ----------

        private void BuildHull()
        {
            var w = new MeshChunkWriter("BossHull");
            var g = new MeshChunkWriter("BossHullGlow");
            var I = Matrix4x4.identity;
            // Track pods with tread plates.
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                {
                    Vector3 c = new Vector3(sx * 10.5f, 2.6f, sz * 14.5f);
                    LowPoly.Box(w, I, c, new Vector3(7f, 5.2f, 14f), MissionArt.Iron);
                    for (int i = 0; i < 9; i++)
                        LowPoly.Box(w, I, c + new Vector3(sx * 3.55f, 0f, -6.4f + i * 1.6f), new Vector3(0.2f, 4.2f, 1f), i % 2 == 0 ? MissionArt.Tar : MissionArt.Iron);
                    LowPoly.Box(w, I, c + new Vector3(0f, 3.2f, 0f), new Vector3(8f, 1.2f, 15f), MissionArt.Rust);   // fender
                }
            LowPoly.Box(w, I, new Vector3(0f, 7.5f, 0f), new Vector3(22f, 6f, 40f), MissionArt.Iron);            // belly
            // Superstructure: the sand-crawler profile (z, y) extruded across the width.
            Extrude(w, new[] { new Vector2(-22f, 9f), new Vector2(-22f, 20f), new Vector2(-6f, 24f), new Vector2(8f, 24f), new Vector2(22f, 11f), new Vector2(22f, 9f) }, 12.5f, MissionArt.Rust, MissionArt.RustLight);
            // Armour plates and hazard stripes on the slopes.
            for (int i = 0; i < 6; i++)
            {
                var slope = Matrix4x4.TRS(new Vector3(0f, 17.6f, 15.2f), Quaternion.Euler(43f, 0f, 0f), Vector3.one);
                LowPoly.Box(w, slope, new Vector3(-10.4f + i * 4.16f, 0f, 0.1f), new Vector3(3.9f, 0.4f, 17.5f), i % 2 == 0 ? MissionArt.Rust : MissionArt.RustLight);
            }
            for (int i = 0; i < 8; i++)
                LowPoly.Box(w, I, new Vector3(-11f + i * 3.15f, 9.8f, 22.2f), new Vector3(1.6f, 1.2f, 0.4f), i % 2 == 0 ? MissionArt.Hazard : MissionArt.Tar);
            // A row of red windows under the front brow, like the crawler's "eyes".
            var brow = Matrix4x4.TRS(new Vector3(0f, 20.5f, 11.8f), Quaternion.Euler(43f, 0f, 0f), Vector3.one);
            for (int i = 0; i < 7; i++) LowPoly.Box(g, brow, new Vector3(-9f + i * 3f, 0f, 0.25f), new Vector3(1.8f, 0.2f, 0.9f), MissionArt.EnemyGlow);
            // Exhaust stacks and side ramps (where troopers come out).
            foreach (float sx in new[] { -6f, 6f })
            {
                LowPoly.Cylinder(w, I, new Vector3(sx, 20f, -17f), 1.3f, 1.1f, 9f, 8, MissionArt.Tar);
                LowPoly.Cylinder(g, I, new Vector3(sx, 28.9f, -17f), 1.12f, 1.12f, 0.2f, 8, MissionArt.HazardGlow, false);
            }
            foreach (float sx in new[] { -1f, 1f })
                LowPoly.Box(w, I, new Vector3(sx * 12.7f, 12f, 4f), new Vector3(0.6f, 6f, 8f), MissionArt.Iron);
            // Boom that carries the bucket wheel.
            LowPoly.Box(w, I, new Vector3(0f, 12f, 25f), new Vector3(4f, 3f, 10f), MissionArt.Iron);
            Attach(transform, MissionArt.Own(w), MissionArt.Solid, true);
            Attach(transform, MissionArt.Own(g), MissionArt.Glow, false);

            wheel = MissionArt.Pivot(transform, "BucketWheel", new Vector3(0f, 10.5f, 30f));
            var ww = new MeshChunkWriter("BossWheel");
            LowPoly.Cylinder(ww, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 0f, 90f), Vector3.one), new Vector3(0f, -1.5f, 0f), 2.2f, 2.2f, 3f, 8, MissionArt.Tar);
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2f / 10f;
                var spoke = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(a * Mathf.Rad2Deg, 0f, 0f), Vector3.one);
                LowPoly.Box(ww, spoke, new Vector3(0f, 4.6f, 0f), new Vector3(1.2f, 7f, 0.8f), MissionArt.Iron);
                LowPoly.Box(ww, spoke, new Vector3(0f, 8.2f, 0.4f), new Vector3(3.2f, 1.8f, 2.4f), i % 2 == 0 ? MissionArt.Rust : MissionArt.Hazard);   // buckets
            }
            Attach(wheel, MissionArt.Own(ww), MissionArt.Solid, true);

            int layer = 0;
            AddBox("HullBelly", new Vector3(0f, 8f, 0f), new Vector3(24f, 9f, 42f), Quaternion.identity, layer);
            AddBox("HullTop", new Vector3(0f, 17f, -6f), new Vector3(25f, 10f, 32f), Quaternion.identity, layer);
            AddBox("HullSlope", new Vector3(0f, 16.5f, 14.8f), new Vector3(25f, 3f, 19f), Quaternion.Euler(43f, 0f, 0f), layer);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sz in new[] { -1f, 1f })
                    AddBox("Tracks", new Vector3(sx * 10.5f, 2.6f, sz * 14.5f), new Vector3(7f, 5.2f, 14f), Quaternion.identity, layer);
            AddBox("Wheel", new Vector3(0f, 10.5f, 30f), new Vector3(4f, 19f, 19f), Quaternion.identity, layer);
        }

        private void AddBox(string name, Vector3 centre, Vector3 size, Quaternion rotation, int layer)
        {
            var go = new GameObject("Col_" + name);
            go.layer = layer;
            go.transform.SetParent(transform, false);
            go.transform.SetLocalPositionAndRotation(centre, rotation);
            go.AddComponent<BoxCollider>().size = size;
        }

        private static void Extrude(MeshChunkWriter w, Vector2[] profile, float halfWidth, Color32 side, Color32 top)
        {
            int n = profile.Length;
            Vector3 L(int i) => new Vector3(-halfWidth, profile[i].y, profile[i].x);
            Vector3 R(int i) => new Vector3(halfWidth, profile[i].y, profile[i].x);
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 e = profile[j] - profile[i];
                var shade = Mathf.Abs(e.x) > Mathf.Abs(e.y) ? top : MeshChunkWriter.Scale(side, 0.9f);
                w.AddQuad(L(i), L(j), R(j), R(i), shade);
            }
            for (int i = 1; i < n - 1; i++)
            {
                w.AddTriangle(L(0), L(i + 1), L(i), side);
                w.AddTriangle(R(0), R(i), R(i + 1), side);
            }
        }

        private static void Attach(Transform parent, List<Mesh> meshes, Material material, bool shadows)
        {
            foreach (var mesh in meshes)
            {
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        private void BuildParts(float coreHealth)
        {
            Hull = BossPart.Create(this, BossRole.Hull, transform, Vector3.zero);
            Core = BossPart.Create(this, BossRole.Core, transform, new Vector3(0f, 13f, -22.4f));
            Core.SetMaxHealth(coreHealth);
            Parts.Add(Core);
            foreach (float sx in new[] { -1f, 1f })
            {
                Parts.Add(BossPart.Create(this, BossRole.Cannon, transform, new Vector3(sx * 8.5f, 24f, 5f)));
                Parts.Add(BossPart.Create(this, BossRole.Cannon, transform, new Vector3(sx * 8.5f, 21.2f, -15.5f)));
                Parts.Add(BossPart.Create(this, BossRole.Flak, transform, new Vector3(sx * 13.4f, 15.5f, -4f)));
            }
            Parts.Add(BossPart.Create(this, BossRole.Mortar, transform, new Vector3(0f, 24f, -1f)));
            Parts.Add(BossPart.Create(this, BossRole.Hangar, transform, new Vector3(0f, 21.5f, -9f)));
            foreach (var p in Parts) p.Destroyed += OnPartDestroyed;
        }

        private void OnPartDestroyed(BossPart part) => PartDestroyed?.Invoke(part);

        // ---------- spawn points (world) ----------

        public Vector3 HangarPoint => transform.TransformPoint(new Vector3(0f, 26f, -9f));
        public Vector3 GatePoint(float side) => transform.TransformPoint(new Vector3(side * 6f, 0f, -Length * 0.5f - 8f));
        public Vector3 RampPoint(float side) => transform.TransformPoint(new Vector3(side * (Width * 0.5f + 3f), 0f, 4f));
        public int AliveCount(BossRole role)
        {
            int n = 0;
            foreach (var p in Parts) if (p.Role == role && p.Alive) n++;
            return n;
        }

        /// <summary>Stop the tracks for a while (troopers climbing down the ramps).</summary>
        public void Hold(int tick, float seconds) => holdUntilTick = Mathf.Max(holdUntilTick, tick + SimulationDriver.SecondsToTicks(seconds));

        // ---------- simulation (called by the director) ----------

        public void Step(int tick, bool running)
        {
            if (Defeated) return;
            UpdatePhase();
            if (!running) return;
            float speed = tick < holdUntilTick ? 0f : BaseSpeed * speedScale;
            Distance = Mathf.Min(Map.RouteLength, Distance + speed * SimulationDriver.FixedStep);
            Vector3 pos = Map.RoutePoint(Distance);
            pos.y = Map.Height(pos);
            Vector3 ahead = Map.RoutePoint(Mathf.Min(Map.RouteLength, Distance + 22f)) - pos;
            ahead.y = 0f;
            var want = ahead.sqrMagnitude > 0.01f ? Quaternion.LookRotation(ahead, Vector3.up) : transform.rotation;
            rb.MovePosition(pos);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, want, 7f * SimulationDriver.FixedStep));
            if (Distance >= Map.RouteLength - 1f) ReachedTown = true;
            if (tick % 5 == 0) Crush(tick);
        }

        private void UpdatePhase()
        {
            float f = CoreFraction;
            int phase = f > 0.66f ? 1 : f > 0.33f ? 2 : 3;
            if (phase <= Phase) return;
            Phase = phase;
            speedScale = phase == 2 ? 1.1f : 1.25f;
            PhaseChanged?.Invoke(phase);
        }

        // The wheel and the tracks grind whatever is in front of or under them.
        private void Crush(int tick)
        {
            var director = RaceDirector.Current;
            if (director == null) return;
            int cooldown = SimulationDriver.SecondsToTicks(0.6f);
            foreach (var car in director.Cars)
            {
                if (car == null || !car.isActiveAndEnabled || !Teams.Hostile(gameObject, car.gameObject)) continue;
                Vector3 local = transform.InverseTransformPoint(car.transform.position);
                if (local.y > 9f || Mathf.Abs(local.x) > Width * 0.5f + 1.5f) continue;
                bool wheelZone = local.z > Length * 0.5f - 2f && local.z < Length * 0.5f + 20f && Mathf.Abs(local.x) < 5f;
                bool trackZone = Mathf.Abs(local.z) < Length * 0.5f && Mathf.Abs(local.x) > 6f && local.y < 5f;
                if (!wheelZone && !trackZone) continue;
                if (crushTick.TryGetValue(car.CarId, out int last) && tick - last < cooldown) continue;
                crushTick[car.CarId] = tick;
                var health = car.GetComponent<CarHealth>();
                if (health != null) health.TakeDamage(wheelZone ? 30f : 22f, gameObject);
                if (car.Body != null)
                {
                    Vector3 away = transform.right * Mathf.Sign(local.x == 0f ? 1f : local.x) + transform.forward * 0.6f;
                    car.Body.AddForce(away.normalized * 9f + Vector3.up * 3f, ForceMode.VelocityChange);
                }
                EffectPool.Current?.PlaySparks(car.transform.position + Vector3.up * 0.5f);
            }
        }

        private void Update()
        {
            if (wheel != null && !Defeated && MissionUnit.Running) wheel.localRotation *= Quaternion.Euler(Time.deltaTime * 40f, 0f, 0f);
        }

        /// <summary>Core down: the tracks stop, every part goes dark (no kill credit for the parts themselves).</summary>
        public void MarkDefeated()
        {
            if (Defeated) return;
            Defeated = true;
            foreach (var p in Parts) if (p != Core) p.Shutdown();
            Hull.Shutdown();
        }

        public Vector3 RandomHullPoint(System.Random rng)
        {
            var local = new Vector3(((float)rng.NextDouble() - 0.5f) * Width, 6f + (float)rng.NextDouble() * 18f, ((float)rng.NextDouble() - 0.5f) * Length);
            return transform.TransformPoint(local);
        }
    }

    public enum BossRole { Hull, Core, Cannon, Flak, Mortar, Hangar }

    /// <summary>One piece of the fortress: the hull (passes hits to the core), the core, or a weapon station.</summary>
    public sealed class BossPart : MissionUnit
    {
        public BossRole Role { get; private set; }
        public BossCrawler Boss { get; private set; }
        public event Action<BossPart> Destroyed;
        /// <summary>Set by the director: the hangar asks for a drone, the mortar for a strike.</summary>
        public static Func<BossPart, int, bool> RequestDrone;
        public static Action<BossPart, Vector3, int> RequestStrike;

        private Transform head, muzzle;
        private LineRenderer laser;
        private CarController target;
        private int lastTargetId;
        private int readyTick, fireAtTick = -1, burstLeft;
        private Vector3 aimPoint;
        private Collider hullBox;

        protected override GameObject ShotOwner => Boss != null ? Boss.gameObject : gameObject;
        public override bool Targetable => base.Targetable && Boss != null && !Boss.Defeated;

        public static BossPart Create(BossCrawler boss, BossRole role, Transform parent, Vector3 local)
        {
            GameObject go = role == BossRole.Hull ? boss.gameObject : new GameObject("Boss_" + role);
            if (role != BossRole.Hull) { go.transform.SetParent(parent, false); go.transform.localPosition = local; }
            var part = go.AddComponent<BossPart>();
            part.Boss = boss;
            part.Role = role;
            part.Kind = role == BossRole.Core ? UnitKind.BossCore : role == BossRole.Hull ? UnitKind.BossHull : UnitKind.BossPart;
            part.Build();
            return part;
        }

        public void SetMaxHealth(float hp) => SetHealth(hp);

        private void Build()
        {
            switch (Role)
            {
                case BossRole.Hull:
                    SetHealth(1e6f); ShowBar = false; Radius = 14f;
                    hullBox = transform.Find("Col_HullBelly")?.GetComponent<Collider>();
                    return;
                case BossRole.Core:
                    SetHealth(4000f); ShowBar = false; Radius = 4f;
                    MissionArt.Attach(transform, MissionArt.Cached("BossCore", (w, g) =>
                    {
                        LowPoly.Box(w, Matrix4x4.identity, Vector3.zero, new Vector3(10f, 8f, 1.2f), MissionArt.Iron);
                        for (int i = 0; i < 5; i++) LowPoly.Box(g, Matrix4x4.identity, new Vector3(-3.6f + i * 1.8f, 0f, -0.65f), new Vector3(1f, 6.4f, 0.1f), MissionArt.HazardGlow);
                        LowPoly.Box(w, Matrix4x4.identity, new Vector3(0f, 4.4f, -0.4f), new Vector3(11f, 0.8f, 2f), MissionArt.Hazard);
                    }), 0);
                    AddCollider(new Vector3(0f, 0f, -0.3f), new Vector3(10f, 8f, 1.6f));
                    break;
                case BossRole.Cannon:
                    SetHealth(320f); Radius = 2.4f;
                    BuildTurret(1.5f);
                    break;
                case BossRole.Flak:
                    SetHealth(200f); Radius = 1.6f;
                    BuildTurret(1f);
                    transform.localRotation = Quaternion.Euler(0f, transform.localPosition.x > 0f ? 90f : -90f, 0f);
                    break;
                case BossRole.Mortar:
                    SetHealth(420f); Radius = 2.6f;
                    MissionArt.Attach(transform, MissionArt.Cached("BossMortar", (w, g) =>
                    {
                        LowPoly.Cylinder(w, Matrix4x4.identity, Vector3.zero, 3f, 2.6f, 1.6f, 8, MissionArt.Iron);
                        var tube = Matrix4x4.TRS(new Vector3(0f, 1.4f, 0f), Quaternion.Euler(-35f, 0f, 0f), Vector3.one);
                        LowPoly.Cylinder(w, tube, Vector3.zero, 1.2f, 1f, 5f, 8, MissionArt.Rust);
                        LowPoly.Cylinder(g, tube, new Vector3(0f, 4.9f, 0f), 0.8f, 0.8f, 0.15f, 8, MissionArt.EnemyGlow, false);
                    }), 0);
                    AddCollider(new Vector3(0f, 1.5f, 0f), new Vector3(5.5f, 3f, 5.5f));
                    muzzle = MissionArt.Pivot(transform, "Muzzle", new Vector3(0f, 5.5f, 2.9f));
                    break;
                case BossRole.Hangar:
                    SetHealth(480f); Radius = 3f;
                    MissionArt.Attach(transform, MissionArt.Cached("BossHangar", (w, g) =>
                    {
                        LowPoly.Box(w, Matrix4x4.identity, new Vector3(0f, 2f, 0f), new Vector3(8f, 4f, 7f), MissionArt.RustLight);
                        LowPoly.Box(w, Matrix4x4.identity, new Vector3(0f, 4.3f, 0f), new Vector3(8.6f, 0.6f, 7.6f), MissionArt.Iron);
                        LowPoly.Box(g, Matrix4x4.identity, new Vector3(0f, 2f, -3.55f), new Vector3(5f, 2.6f, 0.1f), MissionArt.EnemyGlow);
                        for (int i = 0; i < 4; i++) LowPoly.Box(w, Matrix4x4.identity, new Vector3(-3f + i * 2f, 2f, -3.62f), new Vector3(0.9f, 2.8f, 0.12f), MissionArt.Hazard);
                    }), 0);
                    AddCollider(new Vector3(0f, 2f, 0f), new Vector3(8f, 4f, 7f));
                    break;
            }
            CollectRenderers();
        }

        private void BuildTurret(float scale)
        {
            MissionArt.Attach(transform, MissionArt.Cached("TurretBase", MissionArt.TurretBase), 0);
            transform.localScale = Vector3.one * scale;
            head = MissionArt.Pivot(transform, "Head", new Vector3(0f, 1.6f, 0f));
            MissionArt.Attach(head, MissionArt.Cached("TurretHead", MissionArt.TurretHead), 0);
            muzzle = MissionArt.Pivot(head, "Muzzle", new Vector3(0f, 0.4f, 2.8f));
            AddCollider(new Vector3(0f, 1.2f, 0f), new Vector3(4.2f, 2.6f, 4.2f));
            if (Role != BossRole.Cannon) return;
            laser = new GameObject("AimLaser").AddComponent<LineRenderer>();
            laser.transform.SetParent(transform, false);
            laser.sharedMaterial = MissionArt.MarkerRed;
            laser.positionCount = 2;
            laser.startColor = laser.endColor = new Color(1f, 0.12f, 0.1f, 1f);
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.enabled = false;
        }

        private void AddCollider(Vector3 centre, Vector3 size)
        {
            var box = gameObject.AddComponent<BoxCollider>();
            box.center = centre;
            box.size = size;
        }

        public override Vector3 AimPoint => Role == BossRole.Hull ? transform.TransformPoint(new Vector3(0f, 8f, 0f))
            : muzzle != null && Role != BossRole.Mortar ? (head != null ? head.position : transform.position) + Vector3.up * 0.5f
            : transform.position + transform.up * 1.5f;

        // The hull is 46 m long: aim at the part of it nearest to the shooter, at a height the barrels reach.
        public override Vector3 AimPointFrom(Vector3 origin)
        {
            if (Role != BossRole.Hull || hullBox == null) return AimPoint;
            Vector3 p = hullBox.ClosestPoint(origin);
            Vector3 local = transform.InverseTransformPoint(p);
            local.y = Mathf.Clamp(local.y, 4f, 9f);
            return transform.TransformPoint(local);
        }

        public override Vector3 Velocity => Boss != null ? Boss.transform.forward * BossCrawler.BaseSpeed : Vector3.zero;

        public override bool TakeHit(GameObject attacker, float damage, Vector3 point)
        {
            if (Boss == null || Boss.Defeated) return false;
            if (Role == BossRole.Hull)
            {
                // The armour soaks most of it; what gets through shakes the reactor.
                if (!Teams.Hostile(attacker, gameObject)) return false;
                EffectPool.Current?.PlaySparks(point);
                return Boss.Core != null && Boss.Core.TakeHit(attacker, damage * BossCrawler.HullShare, point);
            }
            return base.TakeHit(attacker, damage, point);
        }

        protected override float DespawnSeconds => 1e5f;   // parts stay on the wreck

        protected override void OnDied(GameObject attacker)
        {
            Explode(transform.position + Vector3.up, Role == BossRole.Core ? 4f : 2f);
            Tint(new Color(0.15f, 0.12f, 0.1f));
            if (laser != null) laser.enabled = false;
            Destroyed?.Invoke(this);
        }

        /// <summary>The boss is down: silence this station without an explosion or credit.</summary>
        public void Shutdown()
        {
            if (laser != null) laser.enabled = false;
            target = null;
            if (Alive) Remove();
            DespawnTick = int.MaxValue;
        }

        protected override void Step(int tick)
        {
            if (Boss == null || Boss.Defeated) return;
            switch (Role)
            {
                case BossRole.Cannon: StepCannon(tick); break;
                case BossRole.Flak: StepFlak(tick); break;
                case BossRole.Mortar: StepMortar(tick); break;
                case BossRole.Hangar: StepHangar(tick); break;
            }
        }

        private float PhaseScale => Boss.Phase == 1 ? 1f : Boss.Phase == 2 ? 0.82f : 0.65f;

        private void StepCannon(int tick)
        {
            const float range = 125f, shellSpeed = 85f;
            if (tick < readyTick) return;
            if (target == null || !StillValid(target, range))
            {
                target = FindHostileCar(muzzle.position, range, true, lastTargetId);
                fireAtTick = target != null ? tick + SimulationDriver.SecondsToTicks(1.2f) : -1;
                if (target == null) { readyTick = tick + 25; return; }
            }
            aimPoint = Lead(muzzle.position, target.transform.position + Vector3.up * 0.6f, target.Body != null ? target.Body.linearVelocity : Vector3.zero, shellSpeed);
            TurnHead(aimPoint, 70f);
            if (tick < fireAtTick) return;
            FireShell(muzzle.position, (aimPoint - muzzle.position).normalized * shellSpeed, 20f, tick, impact: 6500f);
            lastTargetId = target.CarId;
            target = null;
            readyTick = tick + SimulationDriver.SecondsToTicks(3.4f * PhaseScale);
        }

        private void StepFlak(int tick)
        {
            const float range = 75f, speed = 115f;
            if (tick < readyTick) return;
            if (burstLeft <= 0)
            {
                target = FindHostileCar(muzzle.position, range, true);
                if (target == null) { readyTick = tick + 20; return; }
                burstLeft = Boss.Phase >= 3 ? 7 : 5;
                fireAtTick = tick + SimulationDriver.SecondsToTicks(0.45f);
            }
            if (target == null || !StillValid(target, range)) { burstLeft = 0; return; }
            aimPoint = Lead(muzzle.position, target.transform.position + Vector3.up * 0.5f, target.Body != null ? target.Body.linearVelocity : Vector3.zero, speed, false);
            TurnHead(aimPoint, 160f);
            if (tick < fireAtTick) return;
            FireShell(muzzle.position, (aimPoint - muzzle.position).normalized * speed, 3.5f, tick, ProjectileKind.MachineGun, PrimaryWeaponType.MachineGun, 700f);
            burstLeft--;
            readyTick = burstLeft > 0 ? tick + SimulationDriver.SecondsToTicks(0.09f) : tick + SimulationDriver.SecondsToTicks(2.2f * PhaseScale);
        }

        private void StepMortar(int tick)
        {
            if (tick < readyTick) return;
            // Shells a car well away from the hull (close in, the fortress's own flak covers it).
            var car = FindHostileCar(transform.position, 240f, false, lastTargetId);
            if (car == null || (car.transform.position - transform.position).sqrMagnitude < 45f * 45f) { readyTick = tick + 30; return; }
            lastTargetId = car.CarId;
            Vector3 v = car.Body != null ? car.Body.linearVelocity : Vector3.zero;
            Vector3 point = car.transform.position + Vector3.ClampMagnitude(v * 2.6f, 60f);
            RequestStrike?.Invoke(this, point, tick);
            Explode(muzzle.position, 1.2f);
            readyTick = tick + SimulationDriver.SecondsToTicks(7.5f * PhaseScale);
        }

        private void StepHangar(int tick)
        {
            if (tick < readyTick) return;
            bool launched = RequestDrone != null && RequestDrone(this, tick);
            readyTick = tick + SimulationDriver.SecondsToTicks(launched ? 15f * PhaseScale : 3f);
        }

        private bool StillValid(CarController car, float range)
        {
            if (car == null || !car.isActiveAndEnabled) return false;
            var h = car.GetComponent<CarHealth>();
            if (h != null && (h.IsDead || h.IsInvulnerable)) return false;
            return (car.transform.position - muzzle.position).sqrMagnitude < range * range;
        }

        private void TurnHead(Vector3 point, float degreesPerSecond)
        {
            if (head == null) return;
            Vector3 flat = head.parent.InverseTransformDirection(point - head.position);
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f) return;
            head.localRotation = Quaternion.RotateTowards(head.localRotation, Quaternion.LookRotation(flat), degreesPerSecond * SimulationDriver.FixedStep);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (laser == null) return;
            bool show = Alive && target != null && fireAtTick > 0 && Running && Boss != null && !Boss.Defeated;
            laser.enabled = show;
            if (!show) return;
            laser.SetPosition(0, muzzle.position);
            laser.SetPosition(1, target.transform.position + Vector3.up * 0.6f);
            laser.startWidth = laser.endWidth = 0.08f + 0.08f * Mathf.PingPong(Time.time * 8f, 1f);
        }
    }
}
