using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingGame
{
    /// <summary>
    /// Imported low-poly meshes that replace primitive-built visuals (pickups, weapon heads). Baked by
    /// Racing/Art/Bake Prop Models from Assets/CarsGame/Art/Props/Models (Tools/art/conform_prop.py) into
    /// Resources/Art/PropModels. Each model is one mesh with vertex colours drawn with the shared M_Props
    /// (CarsGame/StylizedLit, SRP Batcher). A missing library or entry means "keep the primitives".
    /// </summary>
    public sealed class PropModelLibrary : ScriptableObject
    {
        public const string ResourcePath = "Art/PropModels";
        // Size/perf budget (Tools/art/README.md); the baker and an EditMode test enforce it.
        public const int PickupTrisMax = 800;
        public const int WeaponTrisMax = 1200;
        public const int BatchTrisMax = 12000;

        [System.Serializable]
        public struct Entry
        {
            public string id;   // "Pickup_<PickupKind>" or "Weapon_<PrimaryWeaponType>"
            public Mesh mesh;
        }

        public Material material;
        public List<Entry> entries = new List<Entry>();

        private static PropModelLibrary instance;
        private static bool loaded;
        private Dictionary<string, Mesh> byId;

        public static PropModelLibrary Instance
        {
            get
            {
                if (!loaded) { instance = Resources.Load<PropModelLibrary>(ResourcePath); loaded = true; }
                return instance;
            }
        }

        public bool TryGet(string id, out Mesh mesh)
        {
            if (byId == null)
            {
                byId = new Dictionary<string, Mesh>();
                foreach (var e in entries) if (e.mesh != null && !string.IsNullOrEmpty(e.id)) byId[e.id] = e.mesh;
            }
            mesh = null;
            return material != null && byId.TryGetValue(id, out mesh);
        }

        public static bool TryGetPickup(PickupKind kind, out Mesh mesh, out Material material) => TryGet("Pickup_" + kind, out mesh, out material);

        public static bool TryGetWeapon(PrimaryWeaponType type, out Mesh mesh, out Material material) => TryGet("Weapon_" + type, out mesh, out material);

        private static bool TryGet(string id, out Mesh mesh, out Material material)
        {
            var lib = Instance;
            material = lib != null ? lib.material : null;
            mesh = null;
            return lib != null && lib.TryGet(id, out mesh);
        }

        /// <summary>One renderer for a prop: shared mesh + shared material, no shadows, no probes.</summary>
        public static Transform Spawn(Transform parent, string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go.transform;
        }
    }
}
