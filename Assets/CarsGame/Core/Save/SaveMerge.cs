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
            m.progress.upgrades = new int[Math.Max(3, n)];
            for (int i = 0; i < m.progress.upgrades.Length; i++) m.progress.upgrades[i] = Math.Max(At(a.progress.upgrades, i), At(b.progress.upgrades, i));

            int nw = Math.Max(a.progress.weapons?.Length ?? 0, b.progress.weapons?.Length ?? 0);
            m.progress.weapons = new int[Math.Max(4, nw)];
            for (int i = 0; i < m.progress.weapons.Length; i++) m.progress.weapons[i] = Math.Max(At(a.progress.weapons, i), At(b.progress.weapons, i));

            m.progress.equippedSecondary = newer.progress.equippedSecondary;
            m.progress.equippedPrimary = newer.progress.equippedPrimary;
            m.progress.buildPreset = Math.Clamp(newer.progress.buildPreset, 0, 3);

            // Cosmetics: owned items are a union (a purchase is never lost); the selection follows the newer blob.
            var owned = new HashSet<string>(a.progress.cosmetics ?? new List<string>());
            foreach (var c in b.progress.cosmetics ?? new List<string>()) owned.Add(c);
            m.progress.cosmetics = new List<string>(owned);
            m.progress.cosmetics.Sort(StringComparer.Ordinal);
            m.progress.paint = newer.progress.paint ?? "";
            m.progress.livery = newer.progress.livery ?? "";
            m.progress.body = newer.progress.body ?? "";
            m.progress.stripe = newer.progress.stripe ?? "";
            m.progress.daily = MergeDaily(a.progress.daily, b.progress.daily);

            var unlocked = new HashSet<string>(a.progress.unlockedEvents ?? new List<string>());
            foreach (var u in b.progress.unlockedEvents ?? new List<string>()) unlocked.Add(u);
            m.progress.unlockedEvents = new List<string>(unlocked);
            m.progress.unlockedEvents.Sort(StringComparer.Ordinal);

            var bests = new Dictionary<string, EventBest>();
            foreach (var e in a.progress.bests ?? new List<EventBest>()) Best(bests, e);
            foreach (var e in b.progress.bests ?? new List<EventBest>()) Best(bests, e);
            m.progress.bests = new List<EventBest>(bests.Values);
            m.progress.bests.Sort((x, y) => string.CompareOrdinal(x.eventId, y.eventId));

            return m;
        }

        // Unions keyed by day: finishes from both devices (the streak is derived from them), and one bonus per
        // day — two devices that each paid the same day's bonus before syncing keep a single payment.
        private static DailyData MergeDaily(DailyData a, DailyData b)
        {
            a ??= new DailyData(); b ??= new DailyData();
            var days = new SortedSet<int>(a.finishDays ?? new List<int>());
            foreach (var d in b.finishDays ?? new List<int>()) days.Add(d);
            var m = new DailyData { finishDays = new List<int>(days) };
            if (m.finishDays.Count > DailyData.KeepDays) m.finishDays.RemoveRange(0, m.finishDays.Count - DailyData.KeepDays);
            var claims = new SortedDictionary<int, int>();
            foreach (var list in new[] { a.claims, b.claims })
                if (list != null)
                    foreach (var c in list)
                        if (c != null) claims[c.day] = claims.TryGetValue(c.day, out int coins) ? Math.Max(coins, c.coins) : c.coins;
            foreach (var kv in claims) m.claims.Add(new DailyClaim { day = kv.Key, coins = kv.Value });
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
        private static void Best(Dictionary<string, EventBest> d, EventBest e)
        {
            if (e == null || string.IsNullOrEmpty(e.eventId)) return;
            if (!d.TryGetValue(e.eventId, out var cur))
            {
                d[e.eventId] = new EventBest { eventId = e.eventId, bestTimeMs = e.bestTimeMs, stars = e.stars };
            }
            else
            {
                if (e.bestTimeMs > 0 && (cur.bestTimeMs <= 0 || e.bestTimeMs < cur.bestTimeMs)) cur.bestTimeMs = e.bestTimeMs;
                cur.stars |= e.stars;
            }
        }
        private static SettingsData Clone(SettingsData s) => new SettingsData
        {
            lang = s.lang, sfx = s.sfx, music = s.music, quality = s.quality, qualityAuto = s.qualityAuto,
            autoThrottle = s.autoThrottle, cameraShake = s.cameraShake, tier = s.tier, hintsSeen = s.hintsSeen
        };
    }
}
