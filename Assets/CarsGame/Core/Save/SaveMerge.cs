using System;
using System.Collections.Generic;

namespace RacingGame.Core.Save
{
    /// <summary>
    /// Deterministic merge of two save blobs (local vs cloud). Monotonic fields never lose
    /// progress: counters take the max, best times the min, unlocks the union. Settings and
    /// the ghost follow the newer blob. Coins are never merged as a balance — earned and spent
    /// are separate monotonic counters, so a device that spent coins can never resurrect them.
    /// </summary>
    public static class SaveMerge
    {
        public static SaveData Merge(SaveData a, SaveData b)
        {
            if (a == null) return b; if (b == null) return a;
            var newer = a.updatedAtUnixMs >= b.updatedAtUnixMs ? a : b;
            var older = ReferenceEquals(newer, a) ? b : a;

            var m = new SaveData
            {
                version = Math.Max(a.version, b.version),
                updatedAtUnixMs = Math.Max(a.updatedAtUnixMs, b.updatedAtUnixMs),
                deviceId = newer.deviceId,
                settings = Clone(newer.settings),
                bestGhost = string.IsNullOrEmpty(newer.bestGhost) ? older.bestGhost : newer.bestGhost,
            };

            // Ledger rows merge per device, component-wise by max; totals are sums of rows.
            var rows = new Dictionary<string, DeviceLedger>();
            foreach (var r in a.progress.ledger ?? new List<DeviceLedger>()) MergeRow(rows, r);
            foreach (var r in b.progress.ledger ?? new List<DeviceLedger>()) MergeRow(rows, r);
            m.progress.ledger = new List<DeviceLedger>(rows.Values);
            m.progress.ledger.Sort((x, y) => string.CompareOrdinal(x.deviceId, y.deviceId));

            int n = Math.Max(a.progress.upgrades?.Length ?? 0, b.progress.upgrades?.Length ?? 0);
            m.progress.upgrades = new int[n];
            for (int i = 0; i < n; i++) m.progress.upgrades[i] = Math.Max(At(a.progress.upgrades, i), At(b.progress.upgrades, i));

            var unlocked = new HashSet<string>(a.progress.unlockedEvents ?? new List<string>());
            foreach (var u in b.progress.unlockedEvents ?? new List<string>()) unlocked.Add(u);
            m.progress.unlockedEvents = new List<string>(unlocked);
            m.progress.unlockedEvents.Sort(StringComparer.Ordinal);

            var bests = new Dictionary<string, long>();
            foreach (var e in a.progress.bests ?? new List<EventBest>()) Best(bests, e);
            foreach (var e in b.progress.bests ?? new List<EventBest>()) Best(bests, e);
            m.progress.bests = new List<EventBest>();
            foreach (var kv in bests) m.progress.bests.Add(new EventBest { eventId = kv.Key, bestTimeMs = kv.Value });
            m.progress.bests.Sort((x, y) => string.CompareOrdinal(x.eventId, y.eventId));

            return m;
        }

        private static void MergeRow(Dictionary<string, DeviceLedger> rows, DeviceLedger r)
        {
            if (r == null) return;
            string id = r.deviceId ?? "";
            if (!rows.TryGetValue(id, out var cur)) { rows[id] = Copy(r); return; }
            cur.coinsEarned = Math.Max(cur.coinsEarned, r.coinsEarned);
            cur.coinsSpent = Math.Max(cur.coinsSpent, r.coinsSpent);
            cur.races = Math.Max(cur.races, r.races);
            cur.wins = Math.Max(cur.wins, r.wins);
            cur.kills = Math.Max(cur.kills, r.kills);
            cur.deaths = Math.Max(cur.deaths, r.deaths);
            cur.distanceKm = Math.Max(cur.distanceKm, r.distanceKm);
        }

        private static DeviceLedger Copy(DeviceLedger r) => new DeviceLedger { deviceId = r.deviceId ?? "", coinsEarned = r.coinsEarned, coinsSpent = r.coinsSpent, races = r.races, wins = r.wins, kills = r.kills, deaths = r.deaths, distanceKm = r.distanceKm };

        private static int At(int[] arr, int i) => arr != null && i < arr.Length ? arr[i] : 0;
        private static void Best(Dictionary<string, long> d, EventBest e)
        {
            if (e == null || string.IsNullOrEmpty(e.eventId) || e.bestTimeMs <= 0) return;
            if (!d.TryGetValue(e.eventId, out long cur) || e.bestTimeMs < cur) d[e.eventId] = e.bestTimeMs;
        }
        private static SettingsData Clone(SettingsData s) => new SettingsData { lang = s.lang, sfx = s.sfx, music = s.music, quality = s.quality, autoThrottle = s.autoThrottle };
    }
}
