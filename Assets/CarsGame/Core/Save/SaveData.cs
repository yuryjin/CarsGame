using System;
using System.Collections.Generic;

namespace RacingGame.Core.Save
{
    /// <summary>
    /// Player settings. <c>qualityAuto</c> stays true until the player picks a graphics level, so
    /// phones get the Mobile tier and desktops the PC one without a stored choice getting in the way.
    /// New fields default for old saves (JsonUtility leaves missing fields at their initial value).
    /// </summary>
    [Serializable] public class SettingsData
    {
        public string lang = "";
        public float sfx = 1f;
        public float music = 0.8f;
        public int quality = 1;
        public bool qualityAuto = true;
        public bool autoThrottle = false;
        public bool cameraShake = true;
        /// <summary>Turret tracks rivals by itself; off = the mouse aims (desktop only, touch always auto).</summary>
        public bool autoAim = true;
        /// <summary>Last difficulty the player raced ("" = pick by experience).</summary>
        public string tier = "";
        /// <summary>Contextual tips already shown (bit per <c>Core.Onboarding.Hint</c>).</summary>
        public int hintsSeen;
    }
    [Serializable] public class EventBest { public string eventId; public long bestTimeMs; public int stars; }
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

    /// <summary>One paid daily first-finish bonus. Keyed by day, so merged saves pay each day once.</summary>
    [Serializable] public class DailyClaim { public int day; public int coins; }

    /// <summary>
    /// Daily race state. Days are UTC day indices. Both lists merge as unions keyed by day: the streak is derived
    /// from <see cref="finishDays"/> (never a copied scalar) and each day's bonus exists once in <see cref="claims"/>,
    /// whose coins count towards <see cref="SaveData.CoinsEarned"/> outside the per-device ledger.
    /// </summary>
    [Serializable] public class DailyData
    {
        /// <summary>Days with a finished daily race (the last month is kept).</summary>
        public List<int> finishDays = new List<int>();
        public List<DailyClaim> claims = new List<DailyClaim>();

        public const int KeepDays = 31;

        public bool Claimed(int day) { if (claims != null) foreach (var c in claims) if (c != null && c.day == day) return true; return false; }
        public long ClaimedCoins { get { long t = 0; if (claims != null) foreach (var c in claims) if (c != null) t += c.coins; return t; } }
    }

    [Serializable] public class ProgressData
    {
        public List<DeviceLedger> ledger = new List<DeviceLedger>();
        public int[] upgrades = new int[3];
        public int[] weapons = new int[4];   // WeaponKind levels
        public int equippedSecondary = 0;
        public int equippedPrimary = 0;
        public int reservePrimary = 2; // shotgun; independent magazine, swapped with the primary
        public bool fullArsenal; // optional free selection of the entire owned arsenal
        public int buildPreset; // CarBuild; zero preserves the handling of old saves
        public List<string> unlockedEvents = new List<string>();
        public List<EventBest> bests = new List<EventBest>();
        /// <summary>Owned cosmetics as "paint:id" / "livery:id" (free ones are implicit).</summary>
        public List<string> cosmetics = new List<string>();
        public string paint = "";
        public string livery = "";
        /// <summary>Stripe colour as a paint id; "" = automatic contrast.</summary>
        public string stripe = "";
        /// <summary>Selected car body id (<c>CarBodies</c>); "" = the standard car. Bought bodies live in <see cref="cosmetics"/> as "body:id".</summary>
        public string body = "";
        public DailyData daily = new DailyData();
    }

    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 5;
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

        public long CoinsEarned { get { long t = 0; foreach (var l in progress.ledger) t += l.coinsEarned; return t + (progress.daily != null ? progress.daily.ClaimedCoins : 0); } }
        public long CoinsSpent { get { long t = 0; foreach (var l in progress.ledger) t += l.coinsSpent; return t; } }
        public long Coins => CoinsEarned - CoinsSpent;
        public int Races { get { int t = 0; foreach (var l in progress.ledger) t += l.races; return t; } }
        public int Wins { get { int t = 0; foreach (var l in progress.ledger) t += l.wins; return t; } }
        public int Kills { get { int t = 0; foreach (var l in progress.ledger) t += l.kills; return t; } }
        public int Deaths { get { int t = 0; foreach (var l in progress.ledger) t += l.deaths; return t; } }
        public int TotalStars
        {
            get
            {
                int total = 0;
                if (progress?.bests != null)
                {
                    foreach (var b in progress.bests)
                    {
                        if (b != null)
                        {
                            int s = b.stars;
                            if ((s & 1) != 0) total++;
                            if ((s & 2) != 0) total++;
                            if ((s & 4) != 0) total++;
                        }
                    }
                }
                return total;
            }
        }
    }
}
