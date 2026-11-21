using System;
using System.Collections.Generic;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>Desert basin with six bridge approaches, an open centre and safe perimeter spawns.</summary>
    public sealed class ArenaLayout : MonoBehaviour, IMineFieldSite, ISimulated
    {
        public const float HalfSize = 120f;
        public const float BridgeEnd = 9f, BridgeStart = 65f, BridgeHeight = 12f;
        public const float BombRadius = 16f, BombDelay = 2f;
        private MissionStrikes strikes;
        private RaceSpawner spawner;
        private RaceManager race;
        private Transform bomberFormation;
        private bool ownsArt;
        public int SimulationOrder => 2100;
        public int LiveBombs => strikes != null ? strikes.Count : 0;
        public bool Running => race != null && race.State == RaceState.Racing;
        /// <summary>The fight zone the minefield covers: a ring between the centre and the spawn circle.</summary>
        public const float MineInnerRadius = 7f, MineOuterRadius = 34f, MineSpacing = 7f;

        public static ArenaLayout Create(Material material)
        {
            var arena = new GameObject("Arena").AddComponent<ArenaLayout>();
            // Keep the runtime arena clear of the circuit and its baked environment.
            arena.transform.position = new Vector3(1000f, 0f, 0f);
            MissionArt.Init(AbyssTrack.SceneryMaterial());
            arena.ownsArt = true;
            arena.BuildDesert();
            return arena;
        }

        private Transform AddBox(string boxName, Vector3 position, Vector3 size, Color32 color)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;
            box.transform.SetParent(transform, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            int trackLayer = LayerMask.NameToLayer("Track");
            if (trackLayer >= 0) box.layer = trackLayer;
            var meshes = MissionArt.Cached("ArenaBox" + color, (w, g) =>
                LowPoly.Box(color.a > 0 ? g : w, Matrix4x4.identity, Vector3.zero, Vector3.one, color));
            box.GetComponent<MeshFilter>().sharedMesh = meshes[0];
            box.GetComponent<Renderer>().sharedMaterial = color.a > 0 ? MissionArt.Glow : MissionArt.Solid;
            return box.transform;
        }

        private void BuildDesert()
        {
            var sand = MissionArt.Sand;
            var rock = MissionArt.Rock;
            var deck = MissionArt.Iron;
            var edge = MissionArt.HazardGlow;
            var ruinedBridgehead = Resources.Load<GameObject>("Art/Arena/RuinedBridgehead");
            AddBox("SandBasin", new Vector3(0f, -1f, 0f), new Vector3(244f, 2f, 244f), sand);
            // Broad approaches are climbable from the sand; all six face the central gap.
            float run = BridgeStart - BridgeEnd;
            float slope = Mathf.Atan2(BridgeHeight, run) * Mathf.Rad2Deg;
            for (int arm = 0; arm < 6; arm++)
            {
                var yaw = Quaternion.Euler(0f, arm * 60f, 0f);
                var centre = yaw * new Vector3(0f, BridgeHeight * 0.5f - 0.5f, (BridgeStart + BridgeEnd) * 0.5f);
                var ramp = AddBox("BridgeApproach" + arm, centre,
                    new Vector3(14f, 1f, Mathf.Sqrt(run * run + BridgeHeight * BridgeHeight)), deck);
                ramp.localRotation = yaw * Quaternion.Euler(slope, 0f, 0f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var stripe = AddBox("BridgeEdge", centre + yaw * new Vector3(side * 6.6f, 0.6f, 0f),
                        new Vector3(0.3f, 0.08f, ramp.localScale.z), edge);
                    stripe.localRotation = ramp.localRotation;
                }
                AddButtress(arm, ruinedBridgehead);
            }
            // Distant dunes dress the boundary without adding runtime mesh colliders.
            var backdrop = new GameObject("Dunes").transform;
            backdrop.SetParent(transform, false);
            MissionArt.Attach(backdrop, MissionArt.Cached("ArenaDunes", (w, g) =>
            {
                LowPoly.Box(w, Matrix4x4.identity, new Vector3(0f, -3f, 0f), new Vector3(700f, 2f, 700f), MissionArt.SandDark);
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI * 2f / 32f;
                    var point = new Vector3(Mathf.Sin(a) * 175f, -2f, Mathf.Cos(a) * 175f);
                    LowPoly.Pyramid(w, Matrix4x4.identity, point, new Vector2(48f, 48f), 10f + i % 4 * 4f,
                        new Vector2(i % 3 * 3f, -4f), i % 2 == 0 ? MissionArt.SandDark : MissionArt.Rock);
                }
            }), LayerMask.NameToLayer("Track"));
            BuildScenery();
            AddBox("NorthRim", new Vector3(0f, 3f, HalfSize), new Vector3(244f, 6f, 3f), rock);
            AddBox("SouthRim", new Vector3(0f, 3f, -HalfSize), new Vector3(244f, 6f, 3f), rock);
            AddBox("EastRim", new Vector3(HalfSize, 3f, 0f), new Vector3(3f, 6f, 238f), rock);
            AddBox("WestRim", new Vector3(-HalfSize, 3f, 0f), new Vector3(3f, 6f, 238f), rock);
        }

        private void AddButtress(int arm, GameObject ruinedBridgehead)
        {
            var position = Quaternion.Euler(0f, arm * 60f + 30f, 0f) * (Vector3.forward * 78f);
            var rotation = Quaternion.Euler(0f, arm * 60f + 15f, 0f);
            var size = new Vector3(17f, 12f, 24f);
            var centre = Vector3.up * (size.y * 0.5f);
            if (arm % 2 == 0 && ruinedBridgehead != null)
            {
                var ruin = Instantiate(ruinedBridgehead, transform, false);
                ruin.transform.SetLocalPositionAndRotation(position, rotation);
                ruin.layer = LayerMask.NameToLayer("Track");
                var blocker = ruin.AddComponent<BoxCollider>();
                blocker.center = centre;
                blocker.size = size;
                return;
            }

            var mesa = AddBox("SandstoneMesa" + arm, position + centre, size, MissionArt.Rock);
            mesa.localRotation = rotation;
            mesa.GetComponent<MeshFilter>().sharedMesh = MissionArt.Cached("ArenaMesa", (w, g) =>
                LowPoly.Pyramid(w, Matrix4x4.identity, Vector3.down * 0.5f, Vector2.one, 1f, new Vector2(0.12f, -0.1f), MissionArt.Rock))[0];
        }

        private void BuildScenery()
        {
            var ruins = Resources.Load<GameObject>("Art/Arena/RuinedWorks");
            if (ruins != null) Instantiate(ruins, transform, false).name = ruins.name;

            var bomber = Resources.Load<GameObject>("Art/Arena/Bomber");
            if (bomber == null) return;
            bomberFormation = MissionArt.Pivot(transform, "BomberFormation", Vector3.zero);
            foreach (var offset in new[] { new Vector3(74f, 33f, -40f), new Vector3(98f, 37f, -23f) })
            {
                var plane = Instantiate(bomber, bomberFormation, false).transform;
                var heading = Quaternion.LookRotation(Vector3.Cross(Vector3.up, offset));
                plane.SetLocalPositionAndRotation(offset, heading * Quaternion.Euler(0f, 0f, -13f));
            }
        }

        public void Initialize(RaceSpawner field, RaceManager manager)
        {
            spawner = field;
            race = manager;
            strikes = new MissionStrikes(_ => transform.position.y, transform, Blast);
        }

        private void OnEnable() => SimulationDriver.Instance.Register(this);
        private void OnDisable()
        {
            if (SimulationDriver.Current != null) SimulationDriver.Current.Unregister(this);
            strikes?.Clear();
            if (ownsArt) { MissionArt.Clear(); ownsArt = false; }
        }

        public void SimulationStep(int tick)
        {
            if (!Running) { strikes?.Clear(); return; }
            strikes?.Step(tick);
        }

        private void LateUpdate()
        {
            if (!Running) return;
            if (bomberFormation != null) bomberFormation.localRotation = Quaternion.Euler(0f, (float)race.RaceTime * 9f, 0f);
            if (SimulationDriver.Current != null) strikes?.UpdateVisuals(SimulationDriver.Current.Tick, 0f);
        }

        public bool IsBombZone(Vector3 world)
        {
            var p = transform.InverseTransformPoint(world);
            return p.y > BridgeHeight - 2f && p.x * p.x + p.z * p.z < 14f * 14f;
        }

        public Vector3 BombLanding(CarController car)
        {
            // Retain a little forward motion, keeping the attack in the central basin.
            var point = car.transform.position + car.Body.linearVelocity * 0.25f * BombDelay;
            var local = transform.InverseTransformPoint(point);
            var flat = Vector3.ClampMagnitude(new Vector3(local.x, 0f, local.z), BridgeEnd - 1f);
            return transform.TransformPoint(flat);
        }

        public bool DropBomb(CarController car, int tick)
        {
            if (!Running || strikes == null || !IsBombZone(car.transform.position)) return false;
            strikes.Add(BombLanding(car), BombRadius, 125f, BombDelay, car.gameObject, tick,
                launchPoint: car.transform.position - Vector3.up);
            return true;
        }

        public bool Threatens(Vector3 world) => strikes != null && strikes.Threatens(world, 5f);

        private void Blast(GameObject source, Vector3 point, float radius, float damage)
        {
            if (spawner == null) return;
            foreach (var p in spawner.Participants)
            {
                if (p == null || p.Health == null || p.Health.IsDead) continue;
                float distance = Vector3.Distance(p.transform.position, point);
                if (distance > radius) continue;
                // The bomber is vulnerable too. ApplyHit preserves shield, respawn protection and kill credit.
                var hit = new Core.Combat.HitInfo(source, damage * Mathf.Lerp(1f, 0.35f, distance / radius), Core.Combat.HitType.Explosion)
                    { Point = p.transform.position, Normal = Vector3.up };
                p.Health.ApplyHit(in hit);
            }
        }

        public void GetSpawnPose(int slot, int total, out Vector3 position, out Quaternion rotation)
        {
            if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total));
            if (slot < 0 || slot >= total) throw new ArgumentOutOfRangeException(nameof(slot));
            float angle = slot * Mathf.PI * 2f / total;
            var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            position = transform.TransformPoint(radial * (HalfSize - 10f) + Vector3.up * 0.5f);
            rotation = transform.rotation * Quaternion.LookRotation(-radial, Vector3.up);
        }

        /// <summary>
        /// Mines scattered over the fight zone: none in the middle (bots head there when no rival is in sight and
        /// the recovery fallback drops cars there), none on the spawn circle, at least <see cref="MineSpacing"/>
        /// apart so there is always a way through.
        /// </summary>
        public void GetMinePoses(int count, int seed, List<Pose> into)
        {
            var rng = new System.Random(seed);
            int first = into.Count;
            for (int attempt = 0; attempt < count * 60 && into.Count - first < count; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                // Uniform over the ring's area, not bunched at the inner edge.
                float r = Mathf.Sqrt(Mathf.Lerp(MineInnerRadius * MineInnerRadius, MineOuterRadius * MineOuterRadius, (float)rng.NextDouble()));
                var local = new Vector3(Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
                if (!FreeOfMines(into, first, transform.TransformPoint(local))) continue;
                into.Add(new Pose(transform.TransformPoint(local), transform.rotation * Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f)));
            }
        }

        private static bool FreeOfMines(List<Pose> poses, int first, Vector3 point)
        {
            for (int i = first; i < poses.Count; i++)
                if ((poses[i].position - point).sqrMagnitude < MineSpacing * MineSpacing) return false;
            return true;
        }

        public bool TryGetRecoveryPose(CarController car, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (car == null) return false;

            int carLayer = LayerMask.NameToLayer("Car");
            int mask = carLayer >= 0 ? 1 << carLayer : Physics.AllLayers;
            Physics.SyncTransforms();
            // More candidates than the maximum eight cars, including the centre as a fallback.
            for (int i = 0; i <= 16; i++)
            {
                GetSpawnPose(i % 16, 16, out position, out rotation);
                if (i == 16) position = transform.TransformPoint(Vector3.up * 0.5f);
                var overlaps = Physics.OverlapBox(position + Vector3.up * 0.5f,
                    new Vector3(1.2f, 0.7f, 2.5f), rotation, mask, QueryTriggerInteraction.Ignore);
                bool occupied = false;
                foreach (var hit in overlaps)
                    if (!hit.transform.IsChildOf(car.transform)) { occupied = true; break; }
                if (!occupied) return true;
            }
            return false;
        }
    }
}
