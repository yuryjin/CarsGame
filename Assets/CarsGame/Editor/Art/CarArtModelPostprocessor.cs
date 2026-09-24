using UnityEditor;

namespace RacingGame.EditorTools.Art
{
    /// <summary>
    /// Import settings for generated car models under Assets/CarsGame/Art/Cars (see Tools/art/README.md):
    /// metres, Blender axes baked (no -90° root rotation), keep empties (wheel pivots, turret, muzzle),
    /// normals from the file (flat-shaded art), vertex colours kept, no materials/animation/cameras.
    /// Materials are assigned by the prefab baker, never extracted from the FBX.
    /// </summary>
    public sealed class CarArtModelPostprocessor : AssetPostprocessor
    {
        public const string Folder = "Assets/CarsGame/Art/Cars/";

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder) || !(assetImporter is ModelImporter mi)) return;
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
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = true;
            mi.generateSecondaryUV = false;
        }
    }
}
