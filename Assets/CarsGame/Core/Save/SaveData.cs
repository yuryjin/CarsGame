using System;
using System.Collections.Generic;

namespace RacingGame.Core.Save
{
    [Serializable] public class SettingsData { public string lang = ""; public float sfx = 1f; public float music = 0.8f; public int quality = 1; public bool autoThrottle = false; }
    [Serializable] public class EventBest { public string eventId; public long bestTimeMs; }
    /// <summary>
    /// Monotonic counters written by ONE device. Every device owns its own row; totals are the
    /// sum of rows, and rows merge component-wise by max — so two devices that both spent
    /// coins from the same baseline keep both purchases (no resurrected coins, no lost stats).
    /// </summary>
    [Serializable] public class DeviceLedger
    {
        public string deviceId = "";
        public long coinsEarned;
        public long coinsSpent;
        public int races;
        public int wins;
        public int kills;
        public int deaths;
        public double distanceKm;
    }

    [Serializable] public class ProgressData
    {
        public List<DeviceLedger> ledger = new List<DeviceLedger>();
        public int[] upgrades = new int[3];
        public List<string> unlockedEvents = new List<string>();
        public List<EventBest> bests = new List<EventBest>();
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;
        public long updatedAtUnixMs;
        public string deviceId = "";
        public SettingsData settings = new SettingsData();
        public ProgressData progress = new ProgressData();
        public string bestGhost = "";

        public DeviceLedger Local(string id)
        {
            foreach (var l in progress.ledger) if (l.deviceId == id) return l;
            var row = new DeviceLedger { deviceId = id };
            progress.ledger.Add(row);
            return row;
        }

        public long CoinsEarned { get { long t = 0; foreach (var l in progress.ledger) t += l.coinsEarned; return t; } }
        public long CoinsSpent { get { long t = 0; foreach (var l in progress.ledger) t += l.coinsSpent; return t; } }
        public long Coins => CoinsEarned - CoinsSpent;
        public int Races { get { int t = 0; foreach (var l in progress.ledger) t += l.races; return t; } }
        public int Wins { get { int t = 0; foreach (var l in progress.ledger) t += l.wins; return t; } }
        public int Kills { get { int t = 0; foreach (var l in progress.ledger) t += l.kills; return t; } }
        public int Deaths { get { int t = 0; foreach (var l in progress.ledger) t += l.deaths; return t; } }
    }
}
