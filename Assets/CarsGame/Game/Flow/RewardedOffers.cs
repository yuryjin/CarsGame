using System;
using System.Threading.Tasks;
using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame
{
    /// <summary>
    /// Opt-in rewarded ads beyond "double the reward": continue after a wreck in a one-life race, try the next
    /// level of an upgrade for one race, and unlock the ad-only paints and liveries. One ad at a time; the
    /// reward is granted only when the platform reports it earned, and every grant goes through the pacer so
    /// no interstitial follows right after.
    /// </summary>
    public static class RewardedOffers
    {
        private static bool busy;
        public static bool Busy => busy;

        public static bool Available
        {
            get { var ads = PlatformGlue.Platform?.Ads; return ads != null && ads.IsRewardedAvailable; }
        }

        /// <summary>
        /// Show a rewarded ad and run <paramref name="onReward"/> only if it was earned (and <paramref name="stillValid"/>
        /// holds afterwards, e.g. the same race). Returns whether the reward was granted.
        /// </summary>
        public static async Task<bool> ShowAsync(Action onReward, Func<bool> stillValid = null)
        {
            if (busy || PlatformGlue.InterstitialBusy || (UnityEngine.Object.FindAnyObjectByType<GameFlow>()?.IsDoublingReward ?? false)) return false;   // one ad at a time
            var ads = PlatformGlue.Platform?.Ads;
            if (ads == null || !ads.IsRewardedAvailable) { MenuScreens.Toast(Loc.Get("ads.unavailable")); return false; }
            busy = true;
            try
            {
                var r = await ads.ShowRewardedAsync();
                bool earned = r.Ok && r.Value;
                if (!earned) { MenuScreens.Toast(Loc.Get(r.Ok ? "ads.no_reward" : "ads.unavailable")); return false; }
                if (stillValid != null && !stillValid()) return false;
                PlatformGlue.Pacer?.OnRewardedShown(Time.realtimeSinceStartup);
                onReward?.Invoke();
                return true;
            }
            finally { busy = false; }
        }

        // ---------- trial upgrade ----------
        /// <summary>Upgrade line boosted by one level for the next race only (−1 = none). Not saved.</summary>
        public static int TrialUpgrade { get; private set; } = -1;
        /// <summary>The line the last race was driven with (for the results nudge), −1 = none.</summary>
        public static int LastRaceTrial { get; private set; } = -1;

        public static bool CanTry(int[] levels, UpgradeKind kind) =>
            TrialUpgrade < 0 && UpgradeCatalog.Level(levels, (int)kind) < UpgradeCatalog.MaxLevel;

        public static void GrantTrial(UpgradeKind kind) { TrialUpgrade = (int)kind; }

        /// <summary>Levels to race with: the saved ones plus the trial level, which is used up by this race.</summary>
        public static int[] ConsumeTrial(int[] saved)
        {
            LastRaceTrial = TrialUpgrade;
            if (TrialUpgrade < 0) return saved;
            var levels = new int[3];
            for (int i = 0; i < levels.Length; i++) levels[i] = UpgradeCatalog.Level(saved, i);
            levels[TrialUpgrade] = Math.Min(UpgradeCatalog.MaxLevel, levels[TrialUpgrade] + 1);
            TrialUpgrade = -1;
            return levels;
        }

        public static void ClearLastRaceTrial() { LastRaceTrial = -1; }
    }
}
