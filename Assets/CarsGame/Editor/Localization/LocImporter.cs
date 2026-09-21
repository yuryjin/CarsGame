using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace RacingGame.EditorTools
{
    /// <summary>
    /// Source of truth is Assets/CarsGame/Localization/Source/strings.csv (key, context, max_chars,
    /// then one column per language). This tool creates the LocalizationSettings, one Locale per
    /// column, the "Game" string table collection (preloaded) and fills every entry. Re-runnable.
    /// It fails on placeholder mismatches and max_chars overflows so broken translations never ship.
    /// </summary>
    public static class LocImporter
    {
        public const string CsvPath = "Assets/CarsGame/Localization/Source/strings.csv";
        public const string LocRoot = "Assets/Localization";
        public const string TableName = "Game";

        [MenuItem("Racing/Localization/Import strings.csv")]
        public static void ImportMenu() => Debug.Log(Import());

        public static string Import()
        {
            var rows = ReadCsv(File.ReadAllText(CsvPath, Encoding.UTF8));
            if (rows.Count < 2) return "csv empty";
            var header = rows[0];
            var langs = new List<string>();
            for (int c = 3; c < header.Count; c++) langs.Add(header[c].Trim());

            EnsureFolder(LocRoot); EnsureFolder(LocRoot + "/Locales"); EnsureFolder(LocRoot + "/Tables");

            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "LocalizationSettings";
                AssetDatabase.CreateAsset(settings, LocRoot + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            var locales = new Dictionary<string, Locale>();
            foreach (var code in langs)
            {
                var existing = LocalizationEditorSettings.GetLocale(code);
                if (existing == null)
                {
                    existing = Locale.CreateLocale(new LocaleIdentifier(code));
                    existing.name = code;
                    AssetDatabase.CreateAsset(existing, $"{LocRoot}/Locales/{code}.asset");
                    LocalizationEditorSettings.AddLocale(existing);
                }
                locales[code] = existing;
            }

            var collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            if (collection == null)
                collection = LocalizationEditorSettings.CreateStringTableCollection(TableName, LocRoot + "/Tables", new List<Locale>(locales.Values));
            foreach (var l in locales.Values) if (collection.GetTable(l.Identifier) == null) collection.AddNewTable(l.Identifier);

            // Locales dropped from the CSV leave the project entirely (table + locale asset), so nothing stale ships.
            foreach (var table in new List<StringTable>(collection.StringTables))
            {
                string code = table.LocaleIdentifier.Code.ToLowerInvariant();
                if (langs.Contains(code)) continue;
                string tablePath = AssetDatabase.GetAssetPath(table);
                collection.RemoveTable(table);
                if (!string.IsNullOrEmpty(tablePath)) AssetDatabase.DeleteAsset(tablePath);
            }
            foreach (var locale in new List<Locale>(LocalizationEditorSettings.GetLocales()))
            {
                if (langs.Contains(locale.Identifier.Code.ToLowerInvariant())) continue;
                string localePath = AssetDatabase.GetAssetPath(locale);
                LocalizationEditorSettings.RemoveLocale(locale);
                if (!string.IsNullOrEmpty(localePath)) AssetDatabase.DeleteAsset(localePath);
            }

            // Preload so GetLocalizedString is synchronous after initialization (WebGL cannot WaitForCompletion).
            var preload = collection.SharedData.Metadata.GetMetadata<UnityEngine.Localization.Metadata.PreloadAssetTableMetadata>();
            if (preload == null) collection.SharedData.Metadata.AddMetadata(new UnityEngine.Localization.Metadata.PreloadAssetTableMetadata { Behaviour = UnityEngine.Localization.Metadata.PreloadAssetTableMetadata.PreloadBehaviour.PreloadAll });

            // Pass 1: validate the whole CSV before touching any asset.
            var problems = new StringBuilder();
            var keys = new HashSet<string>();
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count < 4 || string.IsNullOrWhiteSpace(row[0])) continue;
                string key = row[0].Trim();
                if (!keys.Add(key)) problems.AppendLine($"{key}: duplicate key");
                int max = int.TryParse(row[2], out int m) ? m : 0;
                var placeholders = Placeholders(row[3]);
                for (int c = 3; c < header.Count && c < row.Count; c++)
                {
                    string value = row[c];
                    if (string.IsNullOrEmpty(value)) continue;
                    if (Placeholders(value) != placeholders) problems.AppendLine($"{key} [{langs[c - 3]}]: placeholders {Placeholders(value)} != {placeholders}");
                    if (max > 0 && value.Length > (int)(max * 1.5f)) problems.AppendLine($"{key} [{langs[c - 3]}]: {value.Length} chars > 1.5×max {max}");
                }
            }
            if (problems.Length > 0) throw new System.Exception("Localization import problems (nothing written):\n" + problems);

            // Pass 2: reconcile — removed keys disappear, blank cells clear old translations.
            foreach (var entry in new List<SharedTableData.SharedTableEntry>(collection.SharedData.Entries))
                if (!keys.Contains(entry.Key)) collection.RemoveEntry(entry.Id);

            int entries = 0;
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count < 4 || string.IsNullOrWhiteSpace(row[0])) continue;
                string key = row[0].Trim();
                // AddKey uniquifies existing names ("key 1"); imports must reuse their stable IDs.
                if (collection.SharedData.GetEntry(key) == null) collection.SharedData.AddKey(key);
                for (int c = 3; c < header.Count; c++)
                {
                    string code = langs[c - 3];
                    string value = c < row.Count ? row[c] : "";
                    var table = (StringTable)collection.GetTable(locales[code].Identifier);
                    if (string.IsNullOrEmpty(value)) { var e = table.GetEntry(key); if (e != null) table.RemoveEntry(e.KeyId); }
                    else table.AddEntry(key, value);
                    EditorUtility.SetDirty(table);
                }
                entries++;
            }
            EditorUtility.SetDirty(collection.SharedData);
            foreach (var t in collection.StringTables) EditorUtility.SetDirty(t);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return $"Imported {entries} keys × {langs.Count} languages into '{TableName}' ({string.Join(",", langs)})";
        }

        private static string Placeholders(string s)
        {
            var list = new List<string>();
            int i = 0;
            while ((i = s.IndexOf('{', i)) >= 0)
            {
                int j = s.IndexOf('}', i);
                if (j < 0) break;
                list.Add(s.Substring(i + 1, j - i - 1));
                i = j + 1;
            }
            list.Sort();
            return string.Join("|", list);
        }

        private static List<List<string>> ReadCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (quoted)
                {
                    if (ch == '"') { if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else quoted = false; }
                    else cell.Append(ch);
                }
                else if (ch == '"') quoted = true;
                else if (ch == ',') { row.Add(cell.ToString()); cell.Clear(); }
                else if (ch == '\n' || ch == '\r')
                {
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(cell.ToString()); cell.Clear();
                    if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
                    row = new List<string>();
                }
                else cell.Append(ch);
            }
            if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
