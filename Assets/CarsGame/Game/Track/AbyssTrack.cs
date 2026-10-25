using System.Collections.Generic;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>A circuit suspended over empty space; the player's completed laps change it for the whole field.</summary>
    public sealed class AbyssTrack : MonoBehaviour
    {
        public const float WideHalfWidth = 5f;
        public const float NarrowHalfWidth = 3f;
        public const int LayoutVersion = 3; // Finish paint no longer changes the driving surface.
        private TrackPath path;
        private RaceProgress progress;
        private float originalWidth;
        private GameObject shoulders, barriers;
        private readonly List<GameObject> hidden = new List<GameObject>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        public int Lap { get; private set; }

        public static AbyssTrack Create(TrackPath track)
        {
            var map = new GameObject("AbyssTrack").AddComponent<AbyssTrack>();
            map.path = track;
            map.originalWidth = track.halfWidth;
            var material = SceneryMaterial();
            HideScenery(map.hidden, Scenery);
            map.Build(material);
            map.SetLap(1);
            return map;
        }

        /// <summary>The ground circuit's road, terrain and decor; a track built over the void hides them.</summary>
        public static readonly string[] Scenery = { "Ground_Terrain", "Environment/Colliders", "Environment/Props", "Environment/Terrain",
            "RaceTrack/Track_Road", "RaceTrack/Track_LeftKerb", "RaceTrack/Track_RightKerb", "RaceTrack/Barriers", "RaceTrack/Track_Decor", "RaceTrack/StartFinish_Gantry" };

        /// <summary>The baked environment's vertex-colour material, including its build-time shader reference. Call before hiding it.</summary>
        public static Material SceneryMaterial()
        {
            var terrain = GameObject.Find("Environment/Terrain");
            var renderer = terrain != null ? terrain.GetComponentInChildren<Renderer>() : null;
            if (renderer == null) renderer = GameObject.Find("RaceTrack/Track_Road")?.GetComponent<Renderer>();
            return renderer != null ? renderer.sharedMaterial : null;
        }

        public static void HideScenery(List<GameObject> hidden, IEnumerable<string> paths)
        {
            foreach (string name in paths)
            {
                var go = GameObject.Find(name);
                if (go != null) { hidden.Add(go); go.SetActive(false); }
            }
        }

        public void Follow(RaceProgress player)
        {
            if (progress != null) progress.LapCompleted -= OnLapCompleted;
            progress = player;
            if (progress != null) progress.LapCompleted += OnLapCompleted;
        }

        private void OnLapCompleted(int completed, double seconds) => SetLap(completed + 1);

        public void SetLap(int lap)
        {
            Lap = Mathf.Clamp(lap, 1, 3);
            barriers.SetActive(Lap == 1);
            shoulders.SetActive(Lap < 3);
            path.halfWidth = Lap < 3 ? WideHalfWidth : NarrowHalfWidth;
        }

        private void Build(Material material)
        {
            var road = new MeshChunkWriter("AbyssRoad");
            var markings = new MeshChunkWriter("AbyssMarkings");
            var edges = new MeshChunkWriter("AbyssShoulders");
            var walls = new MeshChunkWriter("AbyssBarriers");
            int count = Mathf.CeilToInt(path.Length / 2f);
            float step = path.Length / count;
            for (int i = 0; i < count; i++)
            {
                float a = i * step, b = (i + 1) * step;
                Strip(road, a, b, -NarrowHalfWidth, NarrowHalfWidth);
                Strip(edges, a, b, -WideHalfWidth, -NarrowHalfWidth);
                Strip(edges, a, b, NarrowHalfWidth, WideHalfWidth);
            }
            // Eight metres of protection followed by a four-metre opening on both sides.
            for (float d = 0; d < path.Length - 8f; d += 12f)
                foreach (float side in new[] { -1f, 1f })
                {
                    path.Pose(d + 4f, side * (WideHalfWidth - 0.25f), out var p, out var r);
                    LowPoly.Box(walls, Matrix4x4.TRS(p, r, Vector3.one), Vector3.up * 0.6f,
                        new Vector3(0.5f, 1.2f, 8f), new Color32(235, 173, 48, 0));
                }
            // A visible finish line remains after the shoulders disappear.
            path.Pose(path.startLineDistance, 0f, out var start, out var rotation);
            // Paint must not become a raised collision edge that launches cars at speed.
            LowPoly.GroundQuad(markings, start, rotation * Vector3.forward, NarrowHalfWidth, 0.35f,
                new Color32(235, 235, 225, 0), start.y + 0.025f);
            AddMeshes("Road", road, material);
            AddMeshes("Markings", markings, material, false);
            shoulders = AddMeshes("Shoulders", edges, material);
            barriers = AddMeshes("Barriers", walls, material);
        }

        private void Strip(MeshChunkWriter writer, float from, float to, float left, float right)
        {
            Vector3 a = path.Position(from) + path.Right(from) * left;
            Vector3 b = path.Position(to) + path.Right(to) * left;
            Vector3 c = path.Position(to) + path.Right(to) * right;
            Vector3 d = path.Position(from) + path.Right(from) * right;
            Vector3 down = Vector3.down * 1.5f;
            var asphalt = new Color32(57, 65, 77, 0);
            var cliff = new Color32(82, 74, 91, 0);
            writer.AddQuad(a, b, c, d, asphalt);
            writer.AddQuad(a + down, b + down, b, a, cliff);
            writer.AddQuad(d, c, c + down, d + down, cliff);
            writer.AddQuad(d + down, c + down, b + down, a + down, cliff);
        }

        private GameObject AddMeshes(string groupName, MeshChunkWriter writer, Material material, bool solid = true)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(transform, false);
            int layer = LayerMask.NameToLayer("Track");
            foreach (var mesh in writer.Finish())
            {
                meshes.Add(mesh);
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(group.transform, false);
                if (layer >= 0) go.layer = layer;
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                if (solid) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            return group;
        }

        public void Restore()
        {
            Follow(null);
            if (path != null) { path.halfWidth = originalWidth; path = null; }
            foreach (var go in hidden) if (go != null) go.SetActive(true);
            hidden.Clear();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Restore();
            foreach (var mesh in meshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
