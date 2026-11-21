using System.Collections.Generic;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame
{
    internal sealed class TrainRouteArt
    {
        internal const float HalfWagonLength = 7.9f, TrophyDeckZ = 4.35f;
        private static readonly Color32 Ballast = new Color32(108, 101, 96, 0), Steel = new Color32(154, 158, 151, 0),
            Timber = new Color32(100, 65, 43, 0), Road = new Color32(182, 134, 84, 0), Wash = new Color32(231, 197, 136, 0),
            Red = new Color32(159, 46, 31, 0), Gold = new Color32(255, 192, 55, 84), Cream = new Color32(240, 215, 164, 0);
        private static readonly Matrix4x4 Identity = Matrix4x4.identity;
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly Material material;
        private readonly bool ownsMaterial;
        private readonly int track = LayerMask.NameToLayer("Track"), ground = LayerMask.NameToLayer("Ground");

        public TrainRouteArt(Material scenery)
        {
            ownsMaterial = scenery == null;
            material = ownsMaterial ? new Material(Shader.Find("CarsGame/StylizedLit")) { name = "TrainCanyonMaterial" } : scenery;
        }

        public Transform BuildCanyon(Transform map, TrainLine approach, TrainLine border, TrainLine siding, TrainLine[] roads,
            Vector3 switchPoint, Vector3 station, Pose borderGate)
        {
            var sand = new MeshChunkWriter("CanyonFloor");
            LowPoly.GroundQuad(sand, new Vector3(-50, 0, 700), Vector3.forward, 1100, 1020, MissionArt.Sand, -0.09f);
            Attach(map, sand, false);
            var floor = MissionArt.Pivot(map, "Sand", new Vector3(-50, -1.09f, 700));
            floor.gameObject.layer = ground;
            floor.gameObject.AddComponent<BoxCollider>().size = new Vector3(2200, 2, 2040);

            var driving = new MeshChunkWriter("DirtRoads");
            var rails = new MeshChunkWriter("IronAndSleepers");
            foreach (var line in new[] { approach, border, siding })
            {
                Strip(driving, line, -3.1f, 3.1f, 0f, Ballast);
                for (float d = 0; d < line.Length; d += 2.9f)
                {
                    Pose p = line.At(d);
                    Vector3 f = p.rotation * Vector3.forward;
                    LowPoly.GroundQuad(rails, p.position, f, 2.15f, 0.20f, Timber, 0.045f);
                }
                Strip(rails, line, -1.42f, -1.27f, 0.075f, Steel);
                Strip(rails, line, 1.27f, 1.42f, 0.075f, Steel);
            }
            for (int i = 0; i < roads.Length; i++)
            {
                var road = roads[i];
                float width = i == 1 ? 7.5f : 8.8f;
                float y = 0.012f * (i + 1);
                Strip(driving, road, -width, width, y, i == 1 ? Wash : Road);
                // Wagon ruts lead into the wash; the parallel road has dark wheel tracks.
                var rut = MeshChunkWriter.Scale(i == 1 ? Wash : Road, 0.87f);
                Strip(rails, road, -2.0f, -1.5f, y + 0.02f, rut);
                Strip(rails, road, 1.5f, 2.0f, y + 0.02f, rut);
            }
            Attach(map, driving, true);
            Attach(map, rails, false);

            var cliffs = new MeshChunkWriter("SandstoneBluffs");
            var rng = new System.Random(9802);
            foreach (var line in new[] { approach, border })
            {
                for (float d = 30f; d < line.Length; d += 92f)
                {
                    Pose p = line.At(d);
                    Mesa(cliffs, p.position - p.rotation * Vector3.right * (74 + rng.Next(15)),
                        31 + rng.Next(12), 48 + rng.Next(12), 25 + rng.Next(28));
                }
            }
            var washLine = roads[1];
            for (float d = 65; d < washLine.Length - 40; d += 81)
            {
                Pose p = washLine.At(d);
                Mesa(cliffs, p.position + p.rotation * Vector3.right * (56 + rng.Next(9)),
                    28 + rng.Next(9), 41 + rng.Next(8), 28 + rng.Next(24));
            }
            Mesa(cliffs, new Vector3(8, 0, 251), 16, 28, 28);
            Mesa(cliffs, new Vector3(-20, 0, 328), 32, 46, 42);
            Mesa(cliffs, new Vector3(-81, 0, 439), 72, 86, 54);
            Mesa(cliffs, new Vector3(-75, 0, 566), 74, 69, 45);
            Mesa(cliffs, new Vector3(-12, 0, 681), 33, 45, 29);
            Attach(map, cliffs, true);

            var fixtures = new MeshChunkWriter("RailwayLandmarks");
            ForkSign(fixtures, new Vector3(23, 0, 127));
            var stationHouse = station + new Vector3(16, 0, 3);
            Box(fixtures, stationHouse + Vector3.up * 2.1f, new Vector3(7, 4.2f, 13), Timber);
            Box(fixtures, stationHouse + new Vector3(-1, 4.4f, 0), new Vector3(10, 0.55f, 15), Red);
            for (int z = -5; z <= 5; z += 5)
                Box(fixtures, stationHouse + new Vector3(-5.2f, 2.1f, z), new Vector3(0.32f, 4.2f, 0.32f), Cream);
            Ring(fixtures, station, 8.4f, Gold);
            Ring(fixtures, switchPoint, 6.8f, Gold);
            var signal = switchPoint + new Vector3(8.8f, 0, 0);
            Box(fixtures, signal + Vector3.up * 3f, new Vector3(0.45f, 6, 0.55f), Timber);
            Box(fixtures, signal + Vector3.up * 0.4f, new Vector3(2f, 0.8f, 2.4f), MissionArt.Iron);
            Matrix4x4 gate = Matrix4x4.TRS(borderGate.position, borderGate.rotation, Vector3.one);
            foreach (float x in new[] { -5f, 21f })
                LowPoly.Box(fixtures, gate, new Vector3(x, 4, 0), new Vector3(0.9f, 8, 1f), Timber);
            for (int i = 0; i < 13; i++)
                LowPoly.Box(fixtures, gate, new Vector3(-4f + i * 2f, 7.9f, 0), new Vector3(2, 1, 1), i % 2 == 0 ? Red : Cream);
            Attach(map, fixtures, false);
            var lever = MissionArt.Pivot(map, "SwitchSemaphore", signal + Vector3.up * 5.5f);
            var arm = new MeshChunkWriter("SignalArm");
            Box(arm, Vector3.right * 1.55f, new Vector3(3.7f, 0.65f, 0.4f), Gold);
            Box(arm, new Vector3(2.8f, 0f, 0f), new Vector3(0.5f, 0.68f, 0.42f), Red);
            Attach(lever, arm, false);
            return lever;
        }

        private static void Mesa(MeshChunkWriter w, Vector3 position, float rx, float rz, float height)
        {
            var m = Matrix4x4.TRS(position + Vector3.down * 0.3f, Quaternion.identity, new Vector3(rx, 1f, rz));
            Cylinder(w, m, Vector3.zero, 1f, 0.83f, height * 0.28f, 8, MissionArt.RockDark);
            Cylinder(w, m, Vector3.up * (height * 0.28f), 0.83f, 0.66f, height * 0.50f, 8, MissionArt.Rock);
            Cylinder(w, m, Vector3.up * (height * 0.78f), 0.66f, 0.61f, height * 0.22f, 8, Road);
        }

        private static void Strip(MeshChunkWriter w, TrainLine line, float left, float right, float y, Color32 color)
        {
            Vector3 up = Vector3.up * y;
            for (int i = 1; i < line.Points.Length; i++)
            {
                Vector3 a = line.Points[i - 1] + up, b = line.Points[i] + up;
                Vector3 r0 = line.Right(i - 1), r1 = line.Right(i);
                w.AddQuad(a + r0 * left, b + r1 * left, b + r1 * right, a + r0 * right, color);
            }
        }

        private static void Ring(MeshChunkWriter w, Vector3 centre, float r, Color32 color)
        {
            centre.y = 0.10f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 a = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                Vector3 b = Quaternion.Euler(0f, (i + 1) * 22.5f, 0f) * Vector3.forward;
                w.AddQuad(centre + a * (r - 0.7f), centre + a * r, centre + b * r, centre + b * (r - 0.7f), color);
            }
        }

        private static void ForkSign(MeshChunkWriter w, Vector3 p)
        {
            Box(w, p + Vector3.up * 2.7f, new Vector3(0.55f, 5.4f, 0.55f), Timber);
            foreach (int side in new[] { -1, 1 })
            {
                var m = Matrix4x4.TRS(p + Vector3.up * (side < 0 ? 4.7f : 3.5f), Quaternion.Euler(0, 0, side * 10f), Vector3.one);
                var color = side < 0 ? MissionArt.Iron : Gold;
                LowPoly.Box(w, m, new Vector3(side * 1.1f, 0f, 0f), new Vector3(3.9f, 0.82f, 0.33f), color);
                Vector3 a = m.MultiplyPoint3x4(new Vector3(side * 2.8f, -0.75f, -0.2f));
                Vector3 b = m.MultiplyPoint3x4(new Vector3(side * 4f, 0f, -0.2f));
                Vector3 c = m.MultiplyPoint3x4(new Vector3(side * 2.8f, 0.75f, -0.2f));
                w.AddTriangle(a, b, c, color);
                w.AddTriangle(c, b, a, color);
            }
        }

        public Rigidbody BuildWagon(Transform map, int index)
        {
            string name = index == 0 ? "Locomotive" : index == 1 ? "Tender" : "TrophyFlatcar";
            var root = MissionArt.Pivot(map, name, Vector3.zero);
            root.gameObject.layer = track;
            var body = root.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var w = new MeshChunkWriter(name);
            float deckHeight = index == 2 ? 1.65f : 1.85f;
            Vector3 deckAt = Vector3.up * (deckHeight - 0.28f);
            Vector3 deckSize = new Vector3(4.6f, 0.56f, 2f * HalfWagonLength);
            Box(w, deckAt, deckSize, index == 2 ? Timber : MissionArt.Iron);
            Collider(root, deckAt, deckSize);
            for (int z = -5; z <= 5; z += 5)
            {
                var axle = Matrix4x4.TRS(new Vector3(-1.78f, 0.81f, z), Quaternion.Euler(0, 0, -90), Vector3.one);
                Cylinder(w, axle, Vector3.zero, 0.78f, 0.78f, 0.35f, 12, MissionArt.Tar);
                Cylinder(w, axle, Vector3.up * 3.18f, 0.78f, 0.78f, 0.35f, 12, MissionArt.Tar);
            }
            if (index == 0)
            {
                Box(w, new Vector3(0, 3.35f, -5.2f), new Vector3(4.2f, 3f, 4.3f), Red);
                Box(w, new Vector3(0, 4.18f, -5.25f), new Vector3(4.25f, 1.15f, 2.65f), MissionArt.Tar);
                Box(w, new Vector3(0, 5.05f, -5.2f), new Vector3(4.7f, 0.48f, 4.6f), MissionArt.Iron);
                var boiler = Matrix4x4.TRS(new Vector3(0f, 3.0f, -3.3f), Quaternion.Euler(90, 0, 0), Vector3.one);
                Cylinder(w, boiler, Vector3.zero, 1.52f, 1.52f, 9.6f, 14, MissionArt.Iron);
                foreach (float z in new[] { 1f, 6.5f, 9.4f })
                    Cylinder(w, boiler, Vector3.up * z, 1.57f, 1.57f, 0.18f, 14, Steel);
                Cylinder(w, Identity, new Vector3(0f, 4.1f, 4.7f), 0.52f, 0.63f, 1.5f, 10, MissionArt.Tar);
                Cylinder(w, Identity, new Vector3(0f, 5.6f, 4.7f), 0.63f, 1.0f, 0.65f, 10, MissionArt.Iron);
                Box(w, new Vector3(0f, 3.77f, 6.45f), new Vector3(0.95f, 0.7f, 0.3f), Gold);
                Collider(root, new Vector3(0f, 3.35f, 0f), new Vector3(3.7f, 3.0f, 13.8f));
            }
            else if (index == 1)
            {
                Box(w, new Vector3(0, 3.12f, -0.2f), new Vector3(4.1f, 2.55f, 12.7f), MissionArt.Rust);
                Box(w, new Vector3(0, 4.48f, -0.2f), new Vector3(4.25f, 0.23f, 12.9f), Steel);
                LowPoly.Pyramid(w, Identity, new Vector3(0, 4.60f, 0), new Vector2(3.9f, 12f), 1.45f, Vector2.zero, MissionArt.Tar);
                Collider(root, new Vector3(0, 3.12f, -0.2f), new Vector3(4.1f, 2.55f, 12.7f));
            }
            else
            {
                Box(w, new Vector3(0, 2.0f, TrophyDeckZ), new Vector3(2.6f, 0.70f, 2.6f), MissionArt.Tar);
                // The outer lip is nearly flush with the road so a buggy inherits the train's forward speed on landing.
                Vector3 top = new Vector3(2f, 1.65f, -2f), lip = new Vector3(8.3f, 0.12f, -2f);
                float angle = Mathf.Atan2(lip.y - top.y, lip.x - top.x) * Mathf.Rad2Deg;
                var ramp = MissionArt.Pivot(root, "SideRamp", (top + lip) * 0.5f);
                ramp.localRotation = Quaternion.Euler(0, 0, angle);
                ramp.gameObject.layer = track;
                Vector3 size = new Vector3(Vector3.Distance(top, lip), 0.16f, 5f);
                Collider(ramp, Vector3.down * 0.08f, size);
                var m = Matrix4x4.TRS(ramp.localPosition, ramp.localRotation, Vector3.one);
                LowPoly.Box(w, m, Vector3.down * 0.08f, size, MissionArt.Iron);
                foreach (float z in new[] { -2.4f, 2.4f })
                    LowPoly.Box(w, m, new Vector3(0f, 0.03f, z), new Vector3(size.x, 0.06f, 0.16f), Gold);
            }
            Attach(root, w, false);
            return body;
        }

        public Transform BuildTrophy(Transform tail)
        {
            var trophy = MissionArt.Pivot(tail, "Trophy", new Vector3(0f, 2.35f, TrophyDeckZ));
            var w = new MeshChunkWriter("GoldCup");
            Cylinder(w, Identity, Vector3.zero, 0.65f, 0.26f, 0.75f, 10, Gold);
            Cylinder(w, Identity, Vector3.up * 0.75f, 0.31f, 1.13f, 1.30f, 12, Gold);
            Cylinder(w, Identity, Vector3.up * 2.05f, 1.15f, 1.15f, 0.17f, 12, Cream);
            foreach (float side in new[] { -1f, 1f })
            {
                Box(w, new Vector3(side * 1.08f, 1.87f, 0f), new Vector3(0.85f, 0.18f, 0.22f), Gold);
                Box(w, new Vector3(side * 1.45f, 1.59f, 0f), new Vector3(0.18f, 0.70f, 0.22f), Gold);
                Box(w, new Vector3(side * 1.10f, 1.28f, 0f), new Vector3(0.85f, 0.18f, 0.22f), Gold);
            }
            Attach(trophy, w, false);
            return trophy;
        }

        private static void Collider(Transform t, Vector3 centre, Vector3 size)
        {
            var collider = t.gameObject.AddComponent<BoxCollider>();
            collider.center = centre;
            collider.size = size;
        }

        // LowPoly winds its radial rings inward. Mirroring the radial axis gives the solid bluffs front faces.
        private static void Cylinder(MeshChunkWriter w, Matrix4x4 m, Vector3 at, float bottom, float top, float height, int sides, Color32 color)
            => LowPoly.Cylinder(w, m * Matrix4x4.TRS(at, Quaternion.identity, new Vector3(-1, 1, 1)),
                Vector3.zero, bottom, top, height, sides, color);

        private static void Box(MeshChunkWriter w, Vector3 centre, Vector3 size, Color32 color)
            => LowPoly.Box(w, Identity, centre, size, color);

        private void Attach(Transform parent, MeshChunkWriter writer, bool solid)
        {
            foreach (var mesh in writer.Finish())
            {
                meshes.Add(mesh);
                var t = MissionArt.Pivot(parent, mesh.name, Vector3.zero);
                t.gameObject.layer = track;
                t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
                if (solid) t.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        public void Dispose()
        {
            foreach (var mesh in meshes) Release(mesh);
            if (ownsMaterial) Release(material);
        }

        private static void Release(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj); else Object.DestroyImmediate(obj);
        }
    }
}
