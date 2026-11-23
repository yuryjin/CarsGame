using System;
using System.Collections.Generic;

namespace RacingGame.Core.Save
{
    [Serializable] public class BodyMeasurement
    {
        public string body, conditions;
        public int accelerationMs, brakingCm, slalomMs;
        public long recordedAt;

        public static string Conditions(ProgressData p) => "asphalt1/" + p.buildPreset + "/"
            + Economy.UpgradeCatalog.Level(p.upgrades, 0) + "/" + Economy.UpgradeCatalog.Level(p.upgrades, 1)
            + "/" + Economy.UpgradeCatalog.Level(p.upgrades, 2);

        public static List<BodyMeasurement> Merge(List<BodyMeasurement> a, List<BodyMeasurement> b)
        {
            var rows = new Dictionary<string, BodyMeasurement>(StringComparer.Ordinal);
            foreach (var list in new[] { a, b }) if (list != null) foreach (var row in list)
            {
                if (row == null || Economy.CarBodies.Index(row.body) < 0 || string.IsNullOrEmpty(row.conditions)
                    || row.accelerationMs <= 0 || row.brakingCm <= 0 || row.slalomMs <= 0) continue;
                string key = row.body + "/" + row.conditions;
                if (!rows.TryGetValue(key, out var old) || row.recordedAt > old.recordedAt || (row.recordedAt == old.recordedAt
                    && (row.slalomMs < old.slalomMs || row.slalomMs == old.slalomMs && (row.brakingCm < old.brakingCm
                    || row.brakingCm == old.brakingCm && row.accelerationMs < old.accelerationMs)))) rows[key] = row;
            }
            var result = new List<BodyMeasurement>(rows.Values);
            result.Sort((x, y) =>
            {
                int t = y.recordedAt.CompareTo(x.recordedAt);
                if (t == 0) t = string.CompareOrdinal(x.body, y.body);
                return t == 0 ? string.CompareOrdinal(x.conditions, y.conditions) : t;
            });
            if (result.Count > 32) result.RemoveRange(32, result.Count - 32);
            return result;
        }
    }
}
