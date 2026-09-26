using System.Collections.Generic;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>
    /// The mission desert, built at runtime far from the circuit: dunes, the boss's packed route across them, mesas and
    /// scrap hulks for cover, a mountain rim as the edge, and the settlement the fortress crawls towards. All driving
    /// surfaces are on the Track layer (full grip; the Ground layer would apply the circuit's grass drag).
    /// Local space: the route runs from the south-west (boss start) to the north-east (the settlement).
    /// </summary>
    public sealed class DesertMap : MonoBehaviour
    {
        public const float HalfSize = 800f, Playable = 640f, Cell = 16f;
        public static readonly Vector3 Origin = new Vector3(-4000f, 0f, 0f);

        private static readonly Vector2[] RouteControl =
        {
            new Vector2(-540f, -330f), new Vector2(-330f, -170f), new Vector2(-130f, -230f), new Vector2(70f, -70f),
            new Vector2(190f, 130f), new Vector2(370f, 220f), new Vector2(505f, 330f)
        };

        private readonly List<Vector3> route = new List<Vector3>();
        private readonly List<float> routeDistance = new List<float>();
        private readonly List<Vector2> mesas = new List<Vector2>();
        private int trackLayer;

        public float RouteLength { get; private set; }
        public Vector3 Town => RoutePoint(RouteLength);

        public static DesertMap Create()
        {
            var map = new GameObject("MissionDesert").AddComponent<DesertMap>();
            map.transform.position = Origin;
            int layer = LayerMask.NameToLayer("Track");
            map.trackLayer = layer >= 0 ? layer : 0;
            map.BuildRoute();
            map.PlaceMesas();
            map.BuildTerrain();
            map.BuildProps();
            return map;
        }

        // ---------- route ----------

        private void BuildRoute()
        {
            route.Clear(); routeDistance.Clear();
            for (int i = 0; i < RouteControl.Length - 1; i++)
            {
                Vector2 p0 = RouteControl[Mathf.Max(0, i - 1)], p1 = RouteControl[i], p2 = RouteControl[i + 1], p3 = RouteControl[Mathf.Min(RouteControl.Length - 1, i + 2)];
                for (int s = 0; s < 24; s++)
                {
                    float t = s / 24f;
                    Vector2 p = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                    route.Add(new Vector3(p.x, 0f, p.y));
                }
            }
            var last = RouteControl[RouteControl.Length - 1];
            route.Add(new Vector3(last.x, 0f, last.y));
            float total = 0f;
            for (int i = 0; i < route.Count; i++)
            {
                if (i > 0) total += Vector3.Distance(route[i - 1], route[i]);
                routeDistance.Add(total);
            }
            RouteLength = total;
        }

        /// <summary>World point on the route <paramref name="distance"/> metres from the boss start (on the ground).</summary>
        public Vector3 RoutePoint(float distance)
        {
            Local(distance, out var p, out _);
            return transform.TransformPoint(p);
        }

        /// <summary>World heading of the route at <paramref name="distance"/> (flat).</summary>
        public Vector3 RouteForward(float distance)
        {
            Local(distance, out _, out var f);
            return transform.TransformDirection(f);
        }

        private void Local(float distance, out Vector3 point, out Vector3 forward)
        {
            distance = Mathf.Clamp(distance, 0f, RouteLength);
            int i = 1;
            while (i < route.Count - 1 && routeDistance[i] < distance) i++;
            float span = Mathf.Max(0.001f, routeDistance[i] - routeDistance[i - 1]);
            float t = Mathf.Clamp01((distance - routeDistance[i - 1]) / span);
            point = Vector3.Lerp(route[i - 1], route[i], t);
            forward = (route[i] - route[i - 1]).normalized;
            point.y = LocalHeight(point.x, point.z);
        }

        /// <summary>Distance (m) along the route of the point closest to <paramref name="world"/>.</summary>
        public float ProjectOnRoute(Vector3 world, out float lateral)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            float best = float.MaxValue, bestDistance = 0f;
            lateral = 0f;
            for (int i = 1; i < route.Count; i++)
            {
                Vector3 a = route[i - 1], b = route[i];
                Vector3 ab = b - a; ab.y = 0f;
                Vector3 ap = p - a; ap.y = 0f;
                float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(0.001f, ab.sqrMagnitude));
                Vector3 closest = a + ab * t;
                Vector3 off = p - closest; off.y = 0f;
                float d = off.sqrMagnitude;
                if (d < best) { best = d; bestDistance = routeDistance[i - 1] + ab.magnitude * t; lateral = Vector3.Cross(ab.normalized, off).y < 0f ? off.magnitude : -off.magnitude; }
            }
            return bestDistance;
        }

        private float DistanceToRoute(float x, float z)
        {
            float best = float.MaxValue;
            for (int i = 1; i < route.Count; i++)
            {
                float ax = route[i - 1].x, az = route[i - 1].z, bx = route[i].x - ax, bz = route[i].z - az;
                float px = x - ax, pz = z - az;
                float t = Mathf.Clamp01((px * bx + pz * bz) / Mathf.Max(0.001f, bx * bx + bz * bz));
                float dx = px - bx * t, dz = pz - bz * t;
                float d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        // ---------- height ----------

        private float[] heights;
        private int gridN;

        /// <summary>Ground height at a world point (bilinear over the built grid, analytic outside it).</summary>
        public float Height(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return transform.position.y + LocalHeight(p.x, p.z);
        }

        private float LocalHeight(float x, float z)
        {
            if (heights == null) return Analytic(x, z, DistanceToRoute(x, z));
            float gx = (x + HalfSize) / Cell, gz = (z + HalfSize) / Cell;
            int ix = Mathf.Clamp(Mathf.FloorToInt(gx), 0, gridN - 1), iz = Mathf.Clamp(Mathf.FloorToInt(gz), 0, gridN - 1);
            float fx = Mathf.Clamp01(gx - ix), fz = Mathf.Clamp01(gz - iz);
            int stride = gridN + 1;
            float h00 = heights[iz * stride + ix], h10 = heights[iz * stride + ix + 1], h01 = heights[(iz + 1) * stride + ix], h11 = heights[(iz + 1) * stride + ix + 1];
            // Same diagonal split as the mesh (a-c), so a spawn lands exactly on the collider.
            return fx > fz ? h00 + (h10 - h00) * fx + (h11 - h10) * fz : h00 + (h11 - h01) * fx + (h01 - h00) * fz;
        }

        private float Analytic(float x, float z, float toRoute)
        {
            float dunes = 1.3f * Mathf.Sin(x * 0.045f + Mathf.Sin(z * 0.017f) * 2.2f) * Mathf.Cos(z * 0.033f + x * 0.011f)
                        + 0.7f * Mathf.Sin((x + z) * 0.021f + 1.1f);
            float big = Mathf.Max(0f, Mathf.Sin(x * 0.011f + 1.3f) * Mathf.Sin(z * 0.013f - 0.7f) - 0.35f) * 11f;
            float flatten = TerrainMeshGen.SmoothStep(16f, 70f, toRoute);
            float h = (dunes + big * TerrainMeshGen.SmoothStep(60f, 160f, toRoute)) * flatten;
            // Mesas get a skirt so cars ramp up against them instead of stopping on a vertical edge.
            foreach (var m in mesas)
            {
                float d = Vector2.Distance(new Vector2(x, z), m);
                h += (1f - TerrainMeshGen.SmoothStep(18f, 42f, d)) * 3f;
            }
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float rim = TerrainMeshGen.SmoothStep(Playable - 20f, HalfSize - 30f, edge);
            float jag = Mathf.Abs(Mathf.Sin(x * 0.031f) * Mathf.Cos(z * 0.027f)) * 35f + Mathf.Abs(Mathf.Sin((x - z) * 0.013f)) * 25f;
            return h + rim * (38f + jag);
        }

        private void BuildTerrain()
        {
            gridN = Mathf.RoundToInt(HalfSize * 2f / Cell);
            int stride = gridN + 1;
            var h = new float[stride * stride];
            var toRoute = new float[h.Length];
            for (int iz = 0; iz <= gridN; iz++)
                for (int ix = 0; ix <= gridN; ix++)
                {
                    float x = -HalfSize + ix * Cell, z = -HalfSize + iz * Cell;
                    int k = iz * stride + ix;
                    toRoute[k] = DistanceToRoute(x, z);
                    h[k] = Analytic(x, z, toRoute[k]);
                }
            heights = h;

            var w = new MeshChunkWriter("DesertTerrain");
            for (int iz = 0; iz < gridN; iz++)
                for (int ix = 0; ix < gridN; ix++)
                {
                    float x0 = -HalfSize + ix * Cell, z0 = -HalfSize + iz * Cell, x1 = x0 + Cell, z1 = z0 + Cell;
                    int k00 = iz * stride + ix, k10 = k00 + 1, k01 = k00 + stride, k11 = k01 + 1;
                    Vector3 a = new Vector3(x0, h[k00], z0), b = new Vector3(x0, h[k01], z1), c = new Vector3(x1, h[k11], z1), d = new Vector3(x1, h[k10], z0);
                    float road = Mathf.Min(Mathf.Min(toRoute[k00], toRoute[k11]), Mathf.Min(toRoute[k10], toRoute[k01]));
                    w.AddTriangle(a, b, c, FaceColour(a, b, c, road, ix, iz));
                    w.AddTriangle(a, c, d, FaceColour(a, c, d, road, ix + 7, iz + 3));
                }
            AddMeshes(w, true, "Terrain");
        }

        private static Color32 FaceColour(Vector3 a, Vector3 b, Vector3 c, float road, int ix, int iz)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            float slope = 1f - Mathf.Abs(n.y);
            float y = (a.y + b.y + c.y) / 3f;
            uint hash = (uint)(ix * 73856093 ^ iz * 19349663);
            float jitter = ((hash >> 8) & 255) / 255f;
            Color32 col = MeshChunkWriter.Lerp(MissionArt.Sand, MissionArt.SandDark, jitter * 0.35f + Mathf.Clamp01(-n.x * 0.8f) * 0.4f);
            if (road < 14f) col = MeshChunkWriter.Lerp(MissionArt.Hardpan, MissionArt.SandDark, jitter * 0.4f);   // packed by the crawler's treads
            if (slope > 0.35f || y > 14f) col = MeshChunkWriter.Lerp(col, MissionArt.Rock, Mathf.Clamp01((slope - 0.3f) * 2.5f + (y - 14f) / 30f));
            return MeshChunkWriter.Scale(col, 0.94f + jitter * 0.08f);
        }

        // ---------- props ----------

        private void PlaceMesas()
        {
            mesas.Clear();
            var rng = new System.Random(4242);
            for (int attempt = 0; attempt < 400 && mesas.Count < 16; attempt++)
            {
                var p = new Vector2(((float)rng.NextDouble() * 2f - 1f) * (Playable - 70f), ((float)rng.NextDouble() * 2f - 1f) * (Playable - 70f));
                if (DistanceToRoute(p.x, p.y) < 85f) continue;
                bool clear = true;
                foreach (var m in mesas) if (Vector2.Distance(m, p) < 120f) { clear = false; break; }
                if (clear) mesas.Add(p);
            }
        }

        private void BuildProps()
        {
            var rocks = new MeshChunkWriter("DesertRocks");
            var rng = new System.Random(77);
            foreach (var m in mesas)
            {
                float y = Analytic(m.x, m.y, DistanceToRoute(m.x, m.y)) - 2f;
                float r = 12f + (float)rng.NextDouble() * 10f, height = 12f + (float)rng.NextDouble() * 16f;
                var rot = Matrix4x4.TRS(new Vector3(m.x, y, m.y), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one);
                LowPoly.Cylinder(rocks, rot, Vector3.zero, r, r * 0.82f, height * 0.55f, 7, MissionArt.Rock);
                LowPoly.Cylinder(rocks, rot, new Vector3(0f, height * 0.55f, 0f), r * 0.82f, r * 0.7f, height * 0.45f, 7, MissionArt.RockDark);
                LowPoly.Cylinder(rocks, rot, new Vector3(0f, height, 0f), r * 0.72f, r * 0.62f, 1.2f, 7, MissionArt.Rock);
            }
            // Scrap hulks (buses, tankers) half sunk in the sand near the route: cover from the fortress's guns.
            for (int i = 0; i < 18; i++)
            {
                float along = 60f + i * (RouteLength - 160f) / 18f;
                Local(along, out var p, out var f);
                Vector3 side = Vector3.Cross(Vector3.up, f) * ((i % 2 == 0 ? 1f : -1f) * (45f + (float)rng.NextDouble() * 40f));
                Vector3 at = p + side;
                at.y = LocalHeight(at.x, at.z) - 0.6f;
                var hulk = Matrix4x4.TRS(at, Quaternion.Euler(((float)rng.NextDouble() - 0.5f) * 14f, (float)rng.NextDouble() * 360f, ((float)rng.NextDouble() - 0.5f) * 20f), Vector3.one);
                bool tanker = i % 3 == 0;
                LowPoly.Box(rocks, hulk, new Vector3(0f, 1.4f, 0f), tanker ? new Vector3(3f, 2.8f, 9f) : new Vector3(2.8f, 2.6f, 10f), i % 2 == 0 ? MissionArt.Rust : MissionArt.RustLight);
                if (tanker) LowPoly.Cylinder(rocks, hulk * Matrix4x4.TRS(new Vector3(0f, 3.2f, -1f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), new Vector3(0f, -3.5f, 0f), 1.4f, 1.4f, 7f, 8, MissionArt.Iron);
                else LowPoly.Box(rocks, hulk, new Vector3(0f, 2.9f, 0.5f), new Vector3(2.6f, 0.3f, 8.5f), MissionArt.Iron);
            }
            BuildGiantWreck(rocks);
            BuildTown(rocks);
            AddMeshes(rocks, true, "Props");
        }

        // A dead bucket-wheel excavator from an earlier war: a landmark the size of the boss, lying beside the route.
        private void BuildGiantWreck(MeshChunkWriter w)
        {
            Local(RouteLength * 0.46f, out var p, out var f);
            Vector3 side = Vector3.Cross(Vector3.up, f);
            Vector3 at = p + side * 95f;
            at.y = LocalHeight(at.x, at.z) - 3f;
            var root = Matrix4x4.TRS(at, Quaternion.LookRotation(f) * Quaternion.Euler(0f, 0f, 8f), Vector3.one);
            LowPoly.Box(w, root, new Vector3(0f, 6f, 0f), new Vector3(20f, 12f, 30f), MissionArt.Rust);
            LowPoly.Box(w, root, new Vector3(0f, 14f, -4f), new Vector3(14f, 6f, 16f), MissionArt.RustLight);
            LowPoly.Box(w, root, new Vector3(0f, 17f, 26f), new Vector3(3f, 3f, 44f), MissionArt.Iron);   // boom
            var wheel = root * Matrix4x4.TRS(new Vector3(0f, 16f, 48f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                LowPoly.Box(w, wheel, new Vector3(Mathf.Cos(a) * 11f, 0f, Mathf.Sin(a) * 11f), new Vector3(4f, 3f, 4f), i % 2 == 0 ? MissionArt.Rust : MissionArt.Iron);
            }
        }

        private void BuildTown(MeshChunkWriter w)
        {
            Local(RouteLength, out var end, out var f);
            Vector3 side = Vector3.Cross(Vector3.up, f);
            var rng = new System.Random(9);
            for (int i = 0; i < 14; i++)
            {
                float ring = 26f + (i % 3) * 14f;
                float a = i * 0.9f;
                Vector3 at = end + f * (Mathf.Cos(a) * ring + 30f) + side * Mathf.Sin(a) * ring;
                at.y = LocalHeight(at.x, at.z) - 0.3f;
                var shack = Matrix4x4.TRS(at, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), Vector3.one);
                var size = new Vector3(5f + (float)rng.NextDouble() * 4f, 3f + (float)rng.NextDouble() * 2f, 5f + (float)rng.NextDouble() * 4f);
                LowPoly.Box(w, shack, new Vector3(0f, size.y * 0.5f, 0f), size, i % 2 == 0 ? MissionArt.RustLight : MissionArt.Bone);
                LowPoly.Pyramid(w, shack, new Vector3(0f, size.y, 0f), new Vector2(size.x + 0.6f, size.z + 0.6f), 1.4f, Vector2.zero, MissionArt.Iron);
            }
            // The well and its water tower: what the fortress is coming for.
            Vector3 tower = end + f * 34f;
            tower.y = LocalHeight(tower.x, tower.z);
            var tm = Matrix4x4.Translate(tower);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + 0.785f;
                LowPoly.Box(w, tm, new Vector3(Mathf.Cos(a) * 3f, 7f, Mathf.Sin(a) * 3f), new Vector3(0.5f, 14f, 0.5f), MissionArt.Iron);
            }
            LowPoly.Cylinder(w, tm, new Vector3(0f, 14f, 0f), 5f, 5f, 6f, 10, MissionArt.RustLight);
            LowPoly.Cylinder(w, tm, new Vector3(0f, 20f, 0f), 5.4f, 0f, 2.5f, 10, MissionArt.Rust);
            var lamps = new MeshChunkWriter("TownLamps");
            LowPoly.Cylinder(lamps, tm, new Vector3(0f, 16.5f, 0f), 5.05f, 5.05f, 0.5f, 10, MissionArt.CyanGlow, false);
            AddMeshes(lamps, false, "TownLamps", MissionArt.Glow);
        }

        private void AddMeshes(MeshChunkWriter w, bool collider, string name, Material material = null)
        {
            foreach (var mesh in MissionArt.Own(w))
            {
                var go = new GameObject(name + "_" + mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = trackLayer;
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = material != null ? material : MissionArt.Solid;
                r.shadowCastingMode = name == "Terrain" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        /// <summary>A ground pose at a world XZ point facing <paramref name="forward"/>, lifted a little above the sand.</summary>
        public void GroundPose(Vector3 world, Vector3 forward, out Vector3 position, out Quaternion rotation)
        {
            world.y = Height(world);
            if (Physics.Raycast(world + Vector3.up * 60f, Vector3.down, out var hit, 120f, 1 << trackLayer, QueryTriggerInteraction.Ignore))
                world.y = hit.point.y;
            position = world + Vector3.up * 0.6f;
            forward.y = 0f;
            rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward, Vector3.up);
        }

        /// <summary>True while a point is inside the drivable area (inside the mountain rim).</summary>
        public bool Inside(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z)) < Playable + 40f && world.y > transform.position.y - 20f;
        }

        public IReadOnlyList<Vector2> Mesas => mesas;
    }
}
