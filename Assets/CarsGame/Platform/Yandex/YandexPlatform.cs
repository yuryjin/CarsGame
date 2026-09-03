using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using RacingGame.Core.Platform;

namespace RacingGame.Platform.Yandex
{
#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// Yandex Games implementation. Every call goes to window.CarsPlatform (template JS) through
    /// the jslib; replies arrive as JSON via PlatformRuntime.OnJs with the request id. Both
    /// sides carry timeouts, so every task completes even if the SDK never answers.
    /// </summary>
    public sealed class YandexPlatform : IPlatformServices, IAds, ICloudSave, ILeaderboards, IAsyncSessions
    {
        [Serializable] private class Envelope { public int id; public bool ok; public string error; public string data; public bool rewarded; public string lang; public string device; public bool authorized; public string playerId; public string playerName; public string avatar; public bool adv; public string entries; public string sessions; }
        [Serializable] private class EntryList { public LeaderboardEntryDto[] items; }
        [Serializable] private class LeaderboardEntryDto { public int rank; public string id; public string name; public long score; public string extra; }
        [Serializable] private class SessionList { public string[] items; }

        public PlatformKind Kind => PlatformKind.Yandex;
        public bool IsInitialized { get; private set; }
        public string LanguageCode { get; private set; } = "";
        public DeviceKind Device { get; private set; } = DeviceKind.Desktop;
        public bool IsMobile => Device == DeviceKind.Mobile || Device == DeviceKind.Tablet;
        public bool IsTV => Device == DeviceKind.TV;
        public PlayerInfo Player { get; private set; } = PlayerInfo.Guest;
        public event Action<PauseReason, bool> PauseChanged;
        public event Action IdentityChanged;
        public IAds Ads => this;
        public ICloudSave CloudSave => this;
        public ILeaderboards Leaderboards => this;
        public IAsyncSessions Sessions => this;
        public IPayments Payments => null;
        public bool IsRewardedAvailable => IsInitialized && hasAdv;

        private bool hasAdv;
        private bool gameplayActive;
        private bool readyCalled;
        private PlatformRuntime runtime;

        public YandexPlatform()
        {
            runtime = PlatformRuntime.Ensure();
            runtime.HostPauseChanged += paused => PauseChanged?.Invoke(PauseReason.HostPause, paused);
            runtime.AdOpened += () => PauseChanged?.Invoke(PauseReason.Ad, true);
            runtime.AdClosed += () => PauseChanged?.Invoke(PauseReason.Ad, false);
        }

        public async Task<Result> InitializeAsync(float deadlineSeconds = 15f)
        {
            if (YandexNative.CarsYa_Available() == 0) return Result.Fail(ResultCategory.NotAvailable, "CarsPlatform missing in template");
            int id = runtime.Begin(out var task, deadlineSeconds);
            YandexNative.CarsYa_Init(id);
            var env = Parse(await task);
            ApplyEnv(env);
            IsInitialized = env.ok;
            return env.ok ? Result.Success : Result.Fail(ResultCategory.Error, env.error ?? "init failed", true);
        }

        private void ApplyEnv(Envelope e)
        {
            if (e == null) return;
            if (!string.IsNullOrEmpty(e.lang)) LanguageCode = e.lang;
            Device = e.device == "mobile" ? DeviceKind.Mobile : e.device == "tablet" ? DeviceKind.Tablet : e.device == "tv" ? DeviceKind.TV : DeviceKind.Desktop;
            Player = new PlayerInfo(string.IsNullOrEmpty(e.playerId) ? "guest" : e.playerId, string.IsNullOrEmpty(e.playerName) ? "Guest" : e.playerName, e.avatar, e.authorized);
            hasAdv = e.adv;
        }

        public async Task<Result<bool>> RequestAuthAsync()
        {
            int id = runtime.Begin(out var task, 120f);
            YandexNative.CarsYa_RequestAuth(id);
            var e = Parse(await task);
            if (e.ok) { ApplyEnv(e); IdentityChanged?.Invoke(); }
            return e.ok ? Result<bool>.Success(e.authorized) : Result<bool>.Fail(ResultCategory.Unauthorized, e.error);
        }

        public void SetGameplayActive(bool active)
        {
            if (active == gameplayActive) return;
            gameplayActive = active;
            if (active) YandexNative.CarsYa_GameplayStart(); else YandexNative.CarsYa_GameplayStop();
        }

        public void ReadyForGameplay() { if (readyCalled) return; readyCalled = true; YandexNative.CarsYa_Ready(); }

        public bool HasFeature(PlatformFeature f)
        {
            var have = PlatformFeature.CloudSave | PlatformFeature.Leaderboards | PlatformFeature.Auth | PlatformFeature.AsyncSessions;
            if (hasAdv) have |= PlatformFeature.Ads | PlatformFeature.RewardedAds;
            return (f & have) == f;
        }

        // ---- ads ----
        public async Task<AdResult> ShowInterstitialAsync()
        {
            if (!IsInitialized || !hasAdv) return AdResult.NotAvailable;
            int id = runtime.Begin(out var task, 120f);
            YandexNative.CarsYa_ShowInterstitial(id);
            var e = Parse(await task);
            if (e.error == "timeout") return AdResult.Timeout;
            return e.ok ? AdResult.Shown : (e.error == "unavailable" ? AdResult.NotAvailable : AdResult.Error);
        }

        public async Task<Result<bool>> ShowRewardedAsync()
        {
            if (!IsInitialized || !hasAdv) return Result<bool>.Fail(ResultCategory.NotAvailable);
            int id = runtime.Begin(out var task, 180f);
            YandexNative.CarsYa_ShowRewarded(id);
            var e = Parse(await task);
            return e.ok ? Result<bool>.Success(e.rewarded) : Result<bool>.Fail(e.error == "timeout" ? ResultCategory.Timeout : ResultCategory.Error, e.error);
        }

        // ---- cloud save ----
        public async Task<Result<string>> LoadAsync()
        {
            int id = runtime.Begin(out var task, 20f);
            YandexNative.CarsYa_LoadSave(id);
            var e = Parse(await task);
            return e.ok ? Result<string>.Success(e.data) : Result<string>.Fail(ResultCategory.Error, e.error, true);
        }

        public void Queue(string json) { YandexNative.CarsYa_QueueSave(json); }

        public async Task<Result> FlushAsync()
        {
            int id = runtime.Begin(out var task, 10f);
            YandexNative.CarsYa_FlushSave(id);
            var e = Parse(await task);
            return e.ok ? Result.Success : Result.Fail(ResultCategory.Error, e.error, true);
        }

        // ---- leaderboards ----
        public async Task<Result> SubmitAsync(string board, long score, string extraData = null)
        {
            if (!Player.IsAuthorized) return Result.Fail(ResultCategory.Unauthorized, "sign in first");
            int id = runtime.Begin(out var task, 20f);
            YandexNative.CarsYa_SubmitScore(id, board, (double)score, extraData ?? "");
            var e = Parse(await task);
            return e.ok ? Result.Success : Result.Fail(ResultCategory.Error, e.error, true);
        }

        public async Task<Result<IReadOnlyList<LeaderboardEntry>>> GetAsync(string board, int top = 10, bool includeUser = true, int aroundUser = 3)
        {
            int id = runtime.Begin(out var task, 20f);
            YandexNative.CarsYa_GetLeaderboard(id, board, top, includeUser ? 1 : 0, aroundUser);
            var e = Parse(await task);
            if (!e.ok) return Result<IReadOnlyList<LeaderboardEntry>>.Fail(ResultCategory.Error, e.error, true);
            var list = new List<LeaderboardEntry>();
            var parsed = SafeJson<EntryList>("{\"items\":" + (e.entries ?? "[]") + "}");
            if (parsed?.items != null) foreach (var d in parsed.items) list.Add(new LeaderboardEntry(d.rank, d.id, d.name, d.score, d.extra));
            return Result<IReadOnlyList<LeaderboardEntry>>.Success(list);
        }

        // ---- async sessions ----
        public async Task<Result<IReadOnlyList<string>>> LoadOpponentsAsync(int count, string meta)
        {
            int id = runtime.Begin(out var task, 30f);
            YandexNative.CarsYa_SessionsLoad(id, count, meta ?? "");
            var e = Parse(await task);
            if (!e.ok) return Result<IReadOnlyList<string>>.Fail(ResultCategory.Error, e.error, true);
            var parsed = SafeJson<SessionList>("{\"items\":" + (e.sessions ?? "[]") + "}");
            return Result<IReadOnlyList<string>>.Success(parsed?.items ?? Array.Empty<string>());
        }

        public void Commit(string payload) { YandexNative.CarsYa_SessionsCommit(payload); }

        public async Task<Result> PushAsync(string meta)
        {
            int id = runtime.Begin(out var task, 30f);
            YandexNative.CarsYa_SessionsPush(id, meta ?? "");
            var e = Parse(await task);
            return e.ok ? Result.Success : Result.Fail(ResultCategory.Error, e.error, true);
        }

        private static Envelope Parse(string json) => SafeJson<Envelope>(json) ?? new Envelope { ok = false, error = "bad json" };
        private static T SafeJson<T>(string json) where T : class { try { return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<T>(json); } catch { return null; } }
    }
#else
    /// <summary>Compile-time stub so the assembly builds on every platform; never instantiated outside WebGL.</summary>
    public sealed class YandexPlatform { }
#endif
}
