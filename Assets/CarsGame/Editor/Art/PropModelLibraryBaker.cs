using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RacingGame.EditorTools.Art
{
    /// <summary>
    /// Builds Assets/CarsGame/Resources/Art/PropModels.asset from the FBX files in Assets/CarsGame/Art/Props/Models
    /// (made by Tools/art/conform_prop.py). File name = entry id: Pickup_&lt;PickupKind&gt; or Weapon_&lt;PrimaryWeaponType&gt;.
    /// Throws when a name is unknown, a file holds more than one mesh, or a triangle budget is broken, so an
    /// oversized model never reaches the build. Re-runnable: overwrites the library (GUID kept).
    /// </summary>
    public static class PropModelLibraryBaker
    {
        public const string ModelsDir = "Assets/CarsGame/Art/Props/Models";
        public const string MaterialPath = "Assets/CarsGame/Art/Props/Materials/M_Props.mat";
        public const string LibraryPath = "Assets/CarsGame/Resources/" + PropModelLibrary.ResourcePath + ".asset";

        [MenuItem("Racing/Art/Bake Prop Models")]
        public static void BakeMenu() { Debug.Log(Bake()); }

        public static string Bake()
        {
            var log = new StringBuilder();
            var material = EnsureMaterial();
            var lib = AssetDatabase.LoadAssetAtPath<PropModelLibrary>(LibraryPath);
            if (lib == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                lib = ScriptableObject.CreateInstance<PropModelLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }
            lib.material = material;
            lib.entries.Clear();
            int total = 0;
            var files = Directory.Exists(ModelsDir) ? Directory.GetFiles(ModelsDir, "*.fbx").OrderBy(f => f).ToArray() : new string[0];
            foreach (var file in files)
            {
                string path = file.Replace('\\', '/');
                string id = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var meshes = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
                if (meshes.Length != 1) throw new InvalidOperationException($"{path}: expected one mesh, found {meshes.Length}");
                var mesh = meshes[0];
                int tris = Triangles(mesh);
                int max = Budget(id);
                if (tris > max) throw new InvalidOperationException($"{id}: {tris} triangles > budget {max}");
                total += tris;
                lib.entries.Add(new PropModelLibrary.Entry { id = id, mesh = mesh });
                log.AppendLine($"  {id}: {tris} tris, {mesh.vertexCount} verts, colours {mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color)}");
            }
            if (total > PropModelLibrary.BatchTrisMax) throw new InvalidOperationException($"props total {total} triangles > {PropModelLibrary.BatchTrisMax}");
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssetIfDirty(lib);
            return $"PropModels: {lib.entries.Count} models, {total} triangles\n{log}";
        }

        public static int Triangles(Mesh mesh)
        {
            long n = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) n += mesh.GetIndexCount(s);
            return (int)(n / 3);
        }

        /// <summary>Triangle budget for an entry id; throws for ids that match no pickup or weapon.</summary>
        public static int Budget(string id)
        {
            if (id.StartsWith("Pickup_") && Enum.TryParse<PickupKind>(id.Substring(7), out _)) return PropModelLibrary.PickupTrisMax;
            if (id.StartsWith("Weapon_") && Enum.TryParse<PrimaryWeaponType>(id.Substring(7), out _)) return PropModelLibrary.WeaponTrisMax;
            throw new InvalidOperationException($"unknown prop id '{id}' (expected Pickup_<PickupKind> or Weapon_<PrimaryWeaponType>)");
        }

        private static Material EnsureMaterial()
        {
            const string shaderName = "CarsGame/StylizedLit";
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + shaderName);
            var m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (m == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            m.shader = shader;
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Flat", 0f);                 // normals are already per-face in the FBX
            m.SetFloat("_EmissionBoost", 1.5f);      // vertex alpha 1 = glowing parts (crosses, stripes, laser)
            m.SetFloat("_EmissionByLamps", 0f);
            m.enableInstancing = false;
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }
    }
}
