using UnityEditor;

namespace RacingGame.EditorTools.Art
{
    /// <summary>
    /// Import settings for generated models under Assets/CarsGame/Art/Cars and Art/Props (see Tools/art/README.md):
    /// metres, Blender axes baked (no -90° root rotation), keep empties (wheel pivots, turret, muzzle),
    /// normals from the file (flat-shaded art), vertex colours kept, no materials/animation/cameras.
    /// Materials are assigned by the bakers, never extracted from the FBX. Props (small single meshes) also get
    /// Medium mesh compression, like the baked environment chunks.
    /// </summary>
    public sealed class CarArtModelPostprocessor : AssetPostprocessor
    {
        public const string Folder = "Assets/CarsGame/Art/Cars/";
        public const string PropsFolder = "Assets/CarsGame/Art/Props/";

        private void OnPreprocessModel()
        {
            bool props = assetPath.StartsWith(PropsFolder);
            if (!(assetPath.StartsWith(Folder) || props) || !(assetImporter is ModelImporter mi)) return;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = true;
            mi.preserveHierarchy = true;
            mi.sortHierarchyByName = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.addCollider = false;
            mi.isReadable = false;
            mi.meshCompression = props ? ModelImporterMeshCompression.Medium : ModelImporterMeshCompression.Off;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = true;
            mi.generateSecondaryUV = false;
        }
    }
}
