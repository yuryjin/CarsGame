using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Bakes static TMP SDF atlases from the characters actually used in strings.csv and
    /// registers them as fallbacks of the default TMP font. Static atlases only: runtime
    /// rasterisation of CJK on WebGL stalls the main thread and a full Noto SC TTF is 10 MB.
    /// </summary>
    public static class FontAtlasBaker
    {
        private const string FontsDir = "Assets/CarsGame/Fonts";

        [MenuItem("Racing/Localization/Bake CJK font atlas")]
        public static void BakeMenu() => Debug.Log(BakeChinese());

        public static string BakeChinese()
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontsDir + "/NotoSansSC-Subset.ttf");
            if (font == null) return "NotoSansSC-Subset.ttf missing";
            // FontEngine caches faces natively; after the TTF is reimported the stale face yields no glyphs.
            FontEngine.UnloadAllFontFaces();

            // Characters: every non-Latin/Cyrillic char from all translations + digits/punctuation.
            var chars = new StringBuilder("0123456789:%/.,!?()+-—·…");
            foreach (var line in File.ReadAllLines(LocImporter.CsvPath, Encoding.UTF8))
                foreach (char c in line) if (c > 0x2E80 && chars.ToString().IndexOf(c) < 0) chars.Append(c);

            var asset = TMP_FontAsset.CreateFontAsset(font, 36, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            asset.name = "NotoSansSC-Subset SDF";
            asset.TryAddCharacters(chars.ToString(), out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[FontAtlasBaker] glyphs missing in subset: " + missing);
            asset.atlasPopulationMode = AtlasPopulationMode.Static;   // freeze: no runtime rasterisation
            asset.isMultiAtlasTexturesEnabled = false;

            string path = FontsDir + "/NotoSansSC-Subset SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            asset.material.name = asset.name + " Material";
            asset.atlasTexture.name = asset.name + " Atlas";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.SaveAssets();

            // Register as a global fallback so every TMP text finds CJK glyphs.
            var settings = TMP_Settings.instance;
            var defaultFont = TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
            {
                if (defaultFont.fallbackFontAssetTable == null) defaultFont.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                defaultFont.fallbackFontAssetTable.RemoveAll(f => f == null || f.name == asset.name);
                defaultFont.fallbackFontAssetTable.Add(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path));
                EditorUtility.SetDirty(defaultFont);
            }
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                var list = so.FindProperty("m_fallbackFontAssets");
                if (list != null)
                {
                    // The asset is deleted and recreated above, so older entries dangle as null.
                    for (int i = list.arraySize - 1; i >= 0; i--)
                        if (list.GetArrayElementAtIndex(i).objectReferenceValue == null) list.DeleteArrayElementAtIndex(i);
                    bool present = false;
                    for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == asset) present = true;
                    if (!present) { list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path); }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(settings);
                }
            }
            AssetDatabase.SaveAssets();
            return $"Baked {path}: {asset.characterTable.Count} chars, atlas {asset.atlasWidth}x{asset.atlasHeight}, missing='{missing}'";
        }
    }
}
