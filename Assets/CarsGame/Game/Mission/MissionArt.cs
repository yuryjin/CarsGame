using System;
using System.Collections.Generic;
using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>
    /// Materials and flat-shaded vertex-colour meshes of the boss mission. Everything is procedural (no textures,
    /// no imported models: the zip budget), built once per mission, shared by every instance and freed by <see cref="Clear"/>.
    /// Palette follows Docs/AI/ArtDirection.md: rust and tar hulls, hazard orange, red enemy glow.
    /// </summary>
    public static class MissionArt
    {
        public static readonly Color32 Sand = new Color32(214, 176, 118, 0), SandDark = new Color32(190, 150, 98, 0),
            Hardpan = new Color32(168, 132, 92, 0), Rock = new Color32(150, 96, 64, 0), RockDark = new Color32(104, 66, 46, 0),
            Rust = new Color32(128, 64, 38, 0), RustLight = new Color32(160, 88, 50, 0), Iron = new Color32(62, 52, 46, 0),
            Tar = new Color32(28, 22, 18, 0), Hazard = new Color32(255, 138, 31, 0), Bone = new Color32(206, 196, 170, 0),
            EnemyGlow = new Color32(255, 52, 36, 255), HazardGlow = new Color32(255, 150, 40, 255), CyanGlow = new Color32(63, 224, 255, 255);

        public static Material Solid { get; private set; }
        public static Material Glow { get; private set; }
        public static Material EnemyShell { get; private set; }
        public static Material MarkerRed { get; private set; }

        private static readonly Dictionary<string, List<Mesh>> cache = new Dictionary<string, List<Mesh>>();
        private static readonly List<Mesh> owned = new List<Mesh>();
        private static readonly List<Material> ownedMaterials = new List<Material>();

        /// <summary>Takes the circuit's vertex-colour material (call before the circuit scenery is hidden).</summary>
        public static void Init(Material scenery)
        {
            if (Solid != null) return;
            if (scenery == null)
            {
                var shader = Shader.Find("CarsGame/StylizedLit") ?? Shader.Find("Universal Render Pipeline/Lit");
                scenery = new Material(shader) { name = "MissionSolidFallback" };
                ownedMaterials.Add(scenery);
            }
            Solid = scenery;
            Glow = new Material(scenery) { name = "MissionGlow" };
            if (Glow.HasProperty("_EmissionBoost")) Glow.SetFloat("_EmissionBoost", 3f);
            ownedMaterials.Add(Glow);
            EnemyShell = MakeColour(scenery, "MissionEnemyShell", new Color(1f, 0.3f, 0.2f));
            MarkerRed = MakeColour(scenery, "MissionMarker", new Color(1f, 0.15f, 0.1f));
        }

        private static Material MakeColour(Material template, string name, Color color)
        {
            var flash = Resources.Load<Material>("FX_Flash");
            var m = flash != null ? new Material(flash) : new Material(template);
            m.name = name;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            ownedMaterials.Add(m);
            return m;
        }

        public static void Clear()
        {
            foreach (var m in owned) if (m != null) Release(m);
            owned.Clear();
            cache.Clear();
            foreach (var m in ownedMaterials) if (m != null) Release(m);
            ownedMaterials.Clear();
            Solid = Glow = EnemyShell = MarkerRed = null;
        }

        private static void Release(UnityEngine.Object resource)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(resource);
            else UnityEngine.Object.DestroyImmediate(resource);
        }

        /// <summary>Meshes built once per key.</summary>
        public static List<Mesh> Cached(string key, Action<MeshChunkWriter, MeshChunkWriter> build)
        {
            if (cache.TryGetValue(key, out var meshes)) return meshes;
            var solid = new MeshChunkWriter(key);
            var glow = new MeshChunkWriter(key + "_Glow");
            build(solid, glow);
            meshes = solid.Finish();
            var glowMeshes = glow.Finish();
            foreach (var m in glowMeshes) m.name = "GLOW:" + m.name;
            meshes.AddRange(glowMeshes);
            owned.AddRange(meshes);
            cache[key] = meshes;
            return meshes;
        }

        /// <summary>Renderers for cached meshes under <paramref name="parent"/>; glow meshes get the emissive material.</summary>
        public static void Attach(Transform parent, List<Mesh> meshes, int layer, bool shadows = true)
        {
            foreach (var mesh in meshes)
            {
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.layer = layer;
                go.transform.SetParent(parent, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                bool glow = mesh.name.StartsWith("GLOW:", StringComparison.Ordinal);
                r.sharedMaterial = glow ? Glow : Solid;
                r.shadowCastingMode = shadows && !glow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>One-off meshes (the map, the boss) freed with the mission.</summary>
        public static List<Mesh> Own(MeshChunkWriter writer)
        {
            var meshes = writer.Finish();
            owned.AddRange(meshes);
            return meshes;
        }

        public static Transform Pivot(Transform parent, string name, Vector3 localPosition)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            return t;
        }

        // ---------- unit models (car space: x right, y up, z forward) ----------

        public static void Drone(MeshChunkWriter w, MeshChunkWriter g)
        {
            var I = Matrix4x4.identity;
            LowPoly.Box(w, I, Vector3.zero, new Vector3(0.8f, 0.34f, 1f), Rust);
            LowPoly.Box(w, I, new Vector3(0f, 0.22f, -0.1f), new Vector3(0.5f, 0.14f, 0.6f), Iron);
            LowPoly.Box(w, I, new Vector3(0f, -0.28f, 0.25f), new Vector3(0.16f, 0.22f, 0.5f), Tar);   // gun pod
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                var arm = Matrix4x4.TRS(new Vector3(sx * 0.55f, 0.05f, sz * 0.5f), Quaternion.Euler(0f, sx * sz * 45f, 0f), Vector3.one);
                LowPoly.Box(w, arm, Vector3.zero, new Vector3(0.12f, 0.08f, 0.7f), Iron);
                LowPoly.Cylinder(w, I, new Vector3(sx * 0.8f, 0.12f, sz * 0.75f), 0.48f, 0.48f, 0.03f, 8, new Color32(40, 34, 30, 0));
            }
            LowPoly.Box(g, I, new Vector3(0f, 0.02f, 0.51f), new Vector3(0.5f, 0.09f, 0.03f), EnemyGlow);
        }

        public static void TrooperBody(MeshChunkWriter w, MeshChunkWriter g, bool rocket)
        {
            var I = Matrix4x4.identity;
            LowPoly.Box(w, I, new Vector3(0f, 1.05f, 0f), new Vector3(0.24f, 0.2f, 0.18f), Iron);     // hips
            LowPoly.Box(w, I, new Vector3(0f, 1.42f, 0f), new Vector3(0.56f, 0.56f, 0.34f), Rust);    // chest plate
            LowPoly.Box(w, I, new Vector3(0f, 1.46f, -0.2f), new Vector3(0.4f, 0.4f, 0.12f), Iron);   // power pack
            LowPoly.Box(w, I, new Vector3(0f, 1.86f, 0.02f), new Vector3(0.26f, 0.28f, 0.28f), Bone);  // skull-less helmet
            LowPoly.Box(w, I, new Vector3(-0.36f, 1.4f, 0.12f), new Vector3(0.14f, 0.5f, 0.14f), Iron);
            LowPoly.Box(w, I, new Vector3(0.36f, 1.4f, 0.12f), new Vector3(0.14f, 0.5f, 0.14f), Iron);
            if (rocket)
            {
                LowPoly.Cylinder(w, Matrix4x4.TRS(new Vector3(0.36f, 1.72f, -0.3f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), Vector3.zero, 0.13f, 0.13f, 1.3f, 6, Hazard);
                LowPoly.Box(g, I, new Vector3(0.36f, 1.72f, 1.01f), new Vector3(0.16f, 0.16f, 0.02f), HazardGlow);
            }
            else LowPoly.Box(w, I, new Vector3(0.2f, 1.3f, 0.5f), new Vector3(0.12f, 0.14f, 0.8f), Tar);   // rifle
            LowPoly.Box(g, I, new Vector3(0f, 1.9f, 0.165f), new Vector3(0.2f, 0.06f, 0.02f), EnemyGlow);   // visor
        }

        public static void TrooperLeg(MeshChunkWriter w, MeshChunkWriter g)
        {
            LowPoly.Box(w, Matrix4x4.identity, new Vector3(0f, -0.48f, 0f), new Vector3(0.14f, 0.96f, 0.16f), Iron);
            LowPoly.Box(w, Matrix4x4.identity, new Vector3(0f, -0.96f, 0.06f), new Vector3(0.18f, 0.1f, 0.3f), Tar);
        }

        public static void TurretBase(MeshChunkWriter w, MeshChunkWriter g)
        {
            var I = Matrix4x4.identity;
            LowPoly.Cylinder(w, I, Vector3.zero, 2.3f, 2f, 1.1f, 8, SandDark);          // sandbag ring
            LowPoly.Cylinder(w, I, new Vector3(0f, 1.1f, 0f), 1.2f, 1.1f, 0.5f, 8, Iron);
            LowPoly.Box(w, I, new Vector3(0f, 0.5f, 2.2f), new Vector3(1.6f, 1f, 0.4f), Rust);
        }

        public static void TurretHead(MeshChunkWriter w, MeshChunkWriter g)
        {
            var I = Matrix4x4.identity;
            LowPoly.Box(w, I, new Vector3(0f, 0.35f, 0f), new Vector3(1.5f, 0.8f, 1.7f), Rust);
            LowPoly.Box(w, I, new Vector3(0f, 0.8f, -0.2f), new Vector3(1.1f, 0.2f, 1f), Iron);
            foreach (float x in new[] { -0.32f, 0.32f })
                LowPoly.Cylinder(w, Matrix4x4.TRS(new Vector3(x, 0.4f, 0.7f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), Vector3.zero, 0.14f, 0.11f, 1.8f, 6, Tar);
            LowPoly.Box(g, I, new Vector3(0f, 0.55f, 0.86f), new Vector3(0.7f, 0.12f, 0.03f), EnemyGlow);
        }

        public static void Plane(MeshChunkWriter w, MeshChunkWriter g)
        {
            var I = Matrix4x4.identity;
            LowPoly.Cylinder(w, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one), new Vector3(0f, -5f, 0f), 0.75f, 0.95f, 7f, 8, Rust);   // fuselage
            LowPoly.Cylinder(w, Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(90f, 0f, 0f), Vector3.one), new Vector3(0f, 2f, 0f), 0.95f, 0.35f, 3f, 8, RustLight);  // nose
            LowPoly.Box(w, I, new Vector3(0f, -0.15f, 0.6f), new Vector3(13f, 0.18f, 2.2f), Iron);        // wing
            LowPoly.Box(w, I, new Vector3(0f, 0.1f, -4.6f), new Vector3(4.6f, 0.14f, 1.2f), Iron);        // tailplane
            LowPoly.Box(w, I, new Vector3(0f, 1f, -4.6f), new Vector3(0.14f, 2f, 1.4f), Rust);            // fin
            LowPoly.Box(w, I, new Vector3(0f, 0.75f, 1.2f), new Vector3(0.8f, 0.5f, 1.4f), Tar);          // canopy (no pilot: a drone plane)
            foreach (float x in new[] { -3.4f, 3.4f })
            {
                LowPoly.Cylinder(w, Matrix4x4.TRS(new Vector3(x, -0.2f, 1.4f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), new Vector3(0f, -1.2f, 0f), 0.45f, 0.4f, 2.2f, 6, Tar);   // engines
                LowPoly.Box(w, I, new Vector3(x, -0.75f, 0.4f), new Vector3(0.35f, 0.35f, 1.6f), Hazard);  // bombs
                LowPoly.Cylinder(g, Matrix4x4.TRS(new Vector3(x, -0.2f, 2.45f), Quaternion.Euler(90f, 0f, 0f), Vector3.one), Vector3.zero, 1.2f, 1.2f, 0.04f, 10, new Color32(90, 80, 70, 120));
            }
            LowPoly.Box(g, I, new Vector3(0f, 0f, 3.6f), new Vector3(0.5f, 0.2f, 0.1f), EnemyGlow);
        }

        /// <summary>Flat ring on the ground, unit radius (scaled per strike): the red "incoming" telegraph.</summary>
        public static void Ring(MeshChunkWriter w, MeshChunkWriter g)
        {
            const int n = 32;
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 o0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), o1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                g.AddQuad(o0 * 0.9f, o1 * 0.9f, o1, o0, EnemyGlow);
                if (i % 2 == 0) g.AddQuad(o0 * 0.2f, o1 * 0.2f, o1 * 0.26f, o0 * 0.26f, EnemyGlow);
            }
            // flip-proof: the same ring facing down, so a tilted slope never shows its back
            for (int i = 0; i < n; i++)
            {
                float a0 = i * Mathf.PI * 2f / n, a1 = (i + 1) * Mathf.PI * 2f / n;
                Vector3 o0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), o1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                g.AddQuad(o0, o1, o1 * 0.9f, o0 * 0.9f, EnemyGlow);
            }
        }

        /// <summary>A dashed strip along +z, unit length and width: the strafing-run telegraph.</summary>
        public static void Dashes(MeshChunkWriter w, MeshChunkWriter g)
        {
            for (int i = 0; i < 10; i++)
            {
                float z0 = i / 10f, z1 = z0 + 0.06f;
                g.AddQuad(new Vector3(-0.5f, 0f, z0), new Vector3(-0.5f, 0f, z1), new Vector3(0.5f, 0f, z1), new Vector3(0.5f, 0f, z0), EnemyGlow);
            }
        }
    }
}
