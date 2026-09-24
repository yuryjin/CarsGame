using System;
using System.Threading.Tasks;
using UnityEngine;
using RacingGame.Core.Ads;
using RacingGame.Core.Platform;
using RacingGame.Core.Save;
using RacingGame.Platform;

namespace RacingGame
{
    /// <summary>
    /// The only place the game talks to the store platform: save load/flush at checkpoints,
    /// balanced gameplay markers, LoadingAPI.ready() once the HUD is interactive, host pause →
    /// PauseController, and the interstitial after results gated by <see cref="AdPacer"/>.
    /// </summary>
    [RequireComponent(typeof(RaceManager))]
    public sealed class PlatformGlue : MonoBehaviour
    {
        public static SaveService Save { get; private set; }
        public static AdPacer Pacer { get; private set; }
        public static IPlatformServices Platform => PlatformRuntime.Current;

        private RaceManager race;
        private bool readySent;
        private bool interstitialPending;
        /// <summary>An interstitial is being requested or shown: rewarded offers wait (one ad at a time).</summary>
        public static bool InterstitialBusy { get; private set; }
        /// <summary>Save loaded and localization ready: menus may start races.</summary>
        public static bool IsReady => Save != null && Save.Loaded && Loc.Ready;
        public static event System.Action Ready;
        private bool readyRaised;

        private void Awake()
        {
            race = GetComponent<RaceManager>();
            if (Save == null)
            {
                string deviceId = PlayerPrefs.GetString("cars.device", "");
                if (string.IsNullOrEmpty(deviceId)) { deviceId = Guid.NewGuid().ToString("N"); PlayerPrefs.SetString("cars.device", deviceId); }
                Save = new SaveService(new PlayerPrefsSaveStore(), () => Platform?.CloudSave, () => Time.realtimeSinceStartup, deviceId);
            }
            if (Pacer == null) Pacer = new AdPacer(Time.realtimeSinceStartup);
        }

        private void OnEnable()
        {
            race.OnRaceStarted += OnRaceStarted;
            PauseController.PausedChanged += OnPausedChanged;
            if (Platform != null) { Platform.PauseChanged += OnPlatformPause; Platform.IdentityChanged += OnIdentityChanged; }
            _ = LoadSaveAsync();
        }

        private void OnDisable()
        {
            race.OnRaceStarted -= OnRaceStarted;
            PauseController.PausedChanged -= OnPausedChanged;
            if (Platform != null) { Platform.PauseChanged -= OnPlatformPause; Platform.IdentityChanged -= OnIdentityChanged; }
            Platform?.SetGameplayActive(false);
        }

        private async Task LoadSaveAsync()
        {
            var p = Platform;
            if (p != null && !p.IsInitialized)
            {
                float until = Time.realtimeSinceStartup + 15f;
                while (!p.IsInitialized && Time.realtimeSinceStartup < until && Application.isPlaying) await Task.Yield();
            }
            if (!Save.Loaded) await Save.LoadAsync();
            Loc.Initialize(p != null ? p.LanguageCode : "", Save.Data.settings.lang);
            SfxPlayer.Instance.volume = Save.Data.settings.sfx * 0.8f;
            AudioListener.volume = Save.Data.settings.sfx > 0f ? 1f : 0f;
            ApplyQuality();
        }

        /// <summary>Send every local best to the cloud boards (after sign-in, and after each finish).</summary>
        public static async Task SubmitAllBestsAsync()
        {
            var p = Platform;
            if (p == null || p.Leaderboards == null || !p.Player.IsAuthorized || Save == null) return;
            int today = DailyRace.Today;
            foreach (var b in Save.Data.progress.bests)
            {
                if (b.bestTimeMs <= 0) continue;   // stars-only rows have no time
                if (b.eventId != null && b.eventId.Length > 1 && b.eventId[0] == 'd' && char.IsDigit(b.eventId[1]))
                {
                    // Daily runs share one descending board keyed by day (see DailyChallenge.EncodeScore); only today's counts.
                    if (int.TryParse(b.eventId.Substring(1), out int day) && day == today)
                        await p.Leaderboards.SubmitAsync(DailyRace.BoardName, Core.Daily.DailyChallenge.EncodeScore(day, b.bestTimeMs));
                    continue;
                }
                await p.Leaderboards.SubmitAsync("best_" + b.eventId, b.bestTimeMs);
            }
        }

        // Sign-in swapped the account: no cloud write until the new account's save is fetched and merged.
        private void OnIdentityChanged() { Save?.OnIdentityChanged(); }

        /// <summary>
        /// Graphics tier: the player's choice, or by device (WebGL defaults every platform to the Mobile
        /// tier, which would leave desktops on 0.8 render scale and short shadows).
        /// </summary>
        public static void ApplyQuality()
        {
            if (Save == null) return;
            var s = Save.Data.settings;
            bool wantHigh = s.qualityAuto ? !IsMobileDevice : s.quality > 0;
            int level = QualityLevel(wantHigh);
            if (level >= 0 && QualitySettings.GetQualityLevel() != level) QualitySettings.SetQualityLevel(level, true);
        }

        /// <summary>
        /// Index of the Mobile or PC tier by name. Unity drops quality levels excluded for the build
        /// target and renumbers the rest, so indices are resolved at runtime; -1 = only one tier shipped.
        /// </summary>
        public static int QualityLevel(bool high)
        {
            var names = QualitySettings.names;
            if (names == null || names.Length < 2) return -1;
            string wanted = high ? "PC" : "Mobile";
            for (int i = 0; i < names.Length; i++) if (string.Equals(names[i], wanted, StringComparison.OrdinalIgnoreCase)) return i;
            return high ? names.Length - 1 : 0;
        }

        /// <summary>True when the build ships more than one quality tier (the settings row is hidden otherwise).</summary>
        public static bool HasQualityChoice => QualitySettings.names != null && QualitySettings.names.Length > 1;

        /// <summary>Phone or tablet according to the platform (falls back to Unity's own check).</summary>
        public static bool IsMobileDevice
        {
            get
            {
                var p = Platform;
                if (p != null && p.IsInitialized) return p.Device == DeviceKind.Mobile || p.Device == DeviceKind.Tablet;
                return Application.isMobilePlatform || SystemInfo.deviceType == DeviceType.Handheld;
            }
        }

        /// <summary>Settings screen: "" = follow the platform.</summary>
        public static void SetLanguageOverride(string code)
        {
            Save.Data.settings.lang = code ?? "";
            Save.MarkDirty();
            Loc.Initialize(Platform != null ? Platform.LanguageCode : "", Save.Data.settings.lang);
        }

        private void Update()
        {
            Save?.Tick();
            // Yandex: LoadingAPI.ready() as soon as the menu is usable (save + strings in), exactly once.
            if (!readyRaised && IsReady) { readyRaised = true; Ready?.Invoke(); }
            if (!readySent && IsReady && Platform != null) { readySent = true; Platform.ReadyForGameplay(); }
        }

        private void OnRaceStarted() { if (!PauseController.IsPaused) Platform?.SetGameplayActive(true); }

        private void OnPausedChanged(bool paused)
        {
            if (Platform == null) return;
            if (paused) Platform.SetGameplayActive(false);
            else if (race.State == RaceState.Racing || race.State == RaceState.Finishing) Platform.SetGameplayActive(true);
            if (paused) _ = Save.FlushAsync();
        }

        private void OnPlatformPause(PauseReason reason, bool paused) { PauseController.Set(reason, paused); }

        /// <summary>Called by GameFlow AFTER it wrote rewards/bests/stats: one ordered checkpoint
        /// (gameplay stop → flush → leaderboards → interstitial), independent of subscription order.</summary>
        public void AfterRaceRecorded() { _ = AfterRaceRecordedAsync(); }

        private async Task AfterRaceRecordedAsync()
        {
            Platform?.SetGameplayActive(false);
            Pacer.OnRaceFinished();
            await Save.FlushAsync();            // progress persisted first …
            await SubmitAllBestsAsync();       // … then the boards …
            if (this == null) return;
            await MaybeInterstitialAsync();    // … and only then the ad
        }

        private async Task MaybeInterstitialAsync()
        {
            var ads = Platform?.Ads;
            if (ads == null || interstitialPending || RewardedOffers.Busy) return;
            if (!Pacer.CanShowInterstitial(Time.realtimeSinceStartup, false)) return;
            interstitialPending = true; InterstitialBusy = true;
            var result = await ads.ShowInterstitialAsync();     // the platform pauses/resumes the game itself
            if (result == AdResult.Shown) Pacer.OnInterstitialShown(Time.realtimeSinceStartup);
            interstitialPending = false; InterstitialBusy = false;
        }
    }
}
