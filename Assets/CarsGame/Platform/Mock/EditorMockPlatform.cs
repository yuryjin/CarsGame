using System.Threading.Tasks;
using UnityEngine;
using RacingGame.Core.Platform;

namespace RacingGame.Platform.Mock
{
    /// <summary>
    /// Editor stand-in for a store: simulates init latency and failure, ad fill/duration,
    /// cloud-save latency/failure and a fake player, so every platform flow can be exercised
    /// in Play mode. Knobs are static so an eval script or a debug menu can flip them.
    /// </summary>
    public sealed class EditorMockPlatform : StandalonePlatform, IAds, ILeaderboards
    {
        public static float InitDelay = 0.3f;
        public static bool FailInit = false;
        public static float AdFillRate = 1f;
        public static float RewardedGrantRate = 1f;
        public static float AdDuration = 1.5f;
        public static float SaveLatency = 0.2f;
        public static float SaveFailRate = 0f;
        public static string Language = "ru";
        public static DeviceKind MockDevice = DeviceKind.Desktop;
        public static bool Authorized = false;

        public override PlatformKind Kind => PlatformKind.EditorMock;
        public override string LanguageCode => Language;
        public override DeviceKind Device => MockDevice;
        public override IAds Ads => this;
        public override ILeaderboards Leaderboards => this;
        public bool IsRewardedAvailable => IsInitialized && AdFillRate > 0f;
        public int InterstitialsShown { get; private set; }
        public int RewardedShown { get; private set; }
        public bool AdOpen { get; private set; }
        public readonly System.Collections.Generic.Dictionary<string, long> Boards = new System.Collections.Generic.Dictionary<string, long>();

        private static readonly System.Random rng = new System.Random(42);

        public override async Task<Result> InitializeAsync(float deadlineSeconds = 15f)
        {
            await RealtimeDelay(InitDelay);
            if (FailInit) { IsInitialized = false; return Result.Fail(ResultCategory.Error, "mock: init failed"); }
            Player = Authorized ? new PlayerInfo("mock-1", "MockPlayer", null, true) : PlayerInfo.Guest;
            IsInitialized = true;
            return Result.Success;
        }

        public override Task<Result<bool>> RequestAuthAsync()
        {
            Authorized = true;
            Player = new PlayerInfo("mock-1", "MockPlayer", null, true);
            RaiseIdentityChanged();
            return Task.FromResult(Result<bool>.Success(true));
        }

        public override bool HasFeature(PlatformFeature f) => f == PlatformFeature.None || (f & (PlatformFeature.Ads | PlatformFeature.RewardedAds | PlatformFeature.CloudSave | PlatformFeature.Leaderboards | PlatformFeature.Auth)) == f;

        public async Task<AdResult> ShowInterstitialAsync()
        {
            if (!IsInitialized) return AdResult.NotAvailable;
            if (rng.NextDouble() > AdFillRate) return AdResult.NotAvailable;
            await RunAd();
            InterstitialsShown++;
            return AdResult.Shown;
        }

        public async Task<Result<bool>> ShowRewardedAsync()
        {
            if (!IsRewardedAvailable) return Result<bool>.Fail(ResultCategory.NotAvailable, "mock: no fill");
            if (rng.NextDouble() > AdFillRate) return Result<bool>.Fail(ResultCategory.NotAvailable, "mock: no fill");
            await RunAd();
            RewardedShown++;
            bool granted = rng.NextDouble() <= RewardedGrantRate;
            return Result<bool>.Success(granted);
        }

        // Mirrors the real SDK: the game must pause on open and resume on close, whatever happens.
        private async Task RunAd()
        {
            AdOpen = true;
            bool resume = gameplayActive;
            if (resume) SetGameplayActive(false);
            RaisePause(PauseReason.Ad, true);
            Debug.Log("[EditorMockPlatform] ad open");
            await RealtimeDelay(AdDuration);
            RaisePause(PauseReason.Ad, false);
            if (resume) SetGameplayActive(true);
            AdOpen = false;
            Debug.Log("[EditorMockPlatform] ad close");
        }

        public override async Task<Result<string>> LoadAsync()
        {
            await RealtimeDelay(SaveLatency);
            if (rng.NextDouble() < SaveFailRate) return Result<string>.Fail(ResultCategory.Error, "mock: load failed", true);
            return Result<string>.Success(cloudBlob);
        }

        public override async Task<Result> FlushAsync()
        {
            await RealtimeDelay(SaveLatency);
            if (rng.NextDouble() < SaveFailRate) return Result.Fail(ResultCategory.Error, "mock: flush failed", true);
            return Result.Success;
        }

        public Task<Result> SubmitAsync(string board, long score, string extraData = null)
        {
            if (!Player.IsAuthorized) return Task.FromResult(Result.Fail(ResultCategory.Unauthorized, "sign in first"));
            bool descending = board == Core.Sim.ArenaScore.Board || board == "daily";
            if (!Boards.TryGetValue(board, out long best) || (descending ? score > best : score < best)) Boards[board] = score;
            return Task.FromResult(Result.Success);
        }

        public Task<Result<System.Collections.Generic.IReadOnlyList<LeaderboardEntry>>> GetAsync(string board, int top = 10, bool includeUser = true, int aroundUser = 3)
        {
            var list = new System.Collections.Generic.List<LeaderboardEntry>();
            if (Boards.TryGetValue(board, out long s)) list.Add(new LeaderboardEntry(1, Player.Id, Player.Name, s, null));
            return Task.FromResult(Result<System.Collections.Generic.IReadOnlyList<LeaderboardEntry>>.Success(list));
        }

        protected override void OnGameplayChanged(bool active) => Debug.Log($"[EditorMockPlatform] gameplay {(active ? "start" : "stop")} (starts={GameplayStarts} stops={GameplayStops})");
        protected override void OnReady() => Debug.Log("[EditorMockPlatform] LoadingAPI.ready()");

        // Realtime wait that survives timeScale = 0 (ads pause the game).
        private static async Task RealtimeDelay(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until && Application.isPlaying) await Task.Yield();
        }
    }
}
