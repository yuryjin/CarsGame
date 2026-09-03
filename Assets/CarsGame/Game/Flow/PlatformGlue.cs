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
        }

        /// <summary>Send every local best to the cloud boards (after sign-in, and after each finish).</summary>
        public static async Task SubmitAllBestsAsync()
        {
            var p = Platform;
            if (p == null || p.Leaderboards == null || !p.Player.IsAuthorized || Save == null) return;
            foreach (var b in Save.Data.progress.bests) await p.Leaderboards.SubmitAsync("best_" + b.eventId, b.bestTimeMs);
        }

        // Sign-in swapped the account: no cloud write until the new account's save is fetched and merged.
        private void OnIdentityChanged() { Save?.OnIdentityChanged(); }

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
            if (ads == null || interstitialPending) return;
            if (!Pacer.CanShowInterstitial(Time.realtimeSinceStartup, false)) return;
            interstitialPending = true;
            var result = await ads.ShowInterstitialAsync();     // the platform pauses/resumes the game itself
            if (result == AdResult.Shown) Pacer.OnInterstitialShown(Time.realtimeSinceStartup);
            interstitialPending = false;
        }
    }
}
