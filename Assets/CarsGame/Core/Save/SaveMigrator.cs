using UnityEngine;

namespace RacingGame.Core.Save
{
    /// <summary>Parses any known save version into the current schema. Unknown/corrupt input yields null.</summary>
    public enum SaveParseStatus { Empty, Ok, Corrupt, NewerVersion }

    public static class SaveMigrator
    {
        public static SaveData FromJson(string json) => Parse(json, out _);

        /// <summary>Parses and reports why a blob was rejected, so callers can tell "no save yet" from "unsafe to overwrite".</summary>
        public static SaveData Parse(string json, out SaveParseStatus status)
        {
            status = SaveParseStatus.Empty;
            if (string.IsNullOrWhiteSpace(json)) return null;
            SaveData data;
            try { data = JsonUtility.FromJson<SaveData>(json); }
            catch { status = SaveParseStatus.Corrupt; return null; }
            if (data == null) { status = SaveParseStatus.Corrupt; return null; }
            if (data.version <= 0) data.version = 1;
            if (data.version == 1)
            {
                // version 1 -> 2: stars in EventBest
                data.version = 2;
            }
            if (data.version > SaveData.CurrentVersion) { status = SaveParseStatus.NewerVersion; return null; }    // from a newer build: refuse rather than corrupt
            status = SaveParseStatus.Ok;
            data.settings ??= new SettingsData();
            data.progress ??= new ProgressData();
            data.progress.upgrades ??= new int[3];
            data.progress.unlockedEvents ??= new System.Collections.Generic.List<string>();
            data.progress.bests ??= new System.Collections.Generic.List<EventBest>();
            data.progress.ledger ??= new System.Collections.Generic.List<DeviceLedger>();
            data.version = SaveData.CurrentVersion;
            return data;
        }

        public static string ToJson(SaveData data) => JsonUtility.ToJson(data);
    }
}
