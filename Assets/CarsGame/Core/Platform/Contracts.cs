using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RacingGame.Core.Platform
{
    public enum PlatformKind { EditorMock, WebMock, Yandex, VkGames, VkPlay, CrazyGames, Steam, Standalone }
    public enum DeviceKind { Desktop, Mobile, Tablet, TV }
    public enum AdResult { Shown, NotAvailable, Error, Timeout, Suppressed }
    public enum ResultCategory { Ok, NotAvailable, Timeout, Error, Suppressed, Unauthorized }

    [Flags]
    public enum PauseReason { None = 0, User = 1, Ad = 2, HostPause = 4, Hidden = 8, Menu = 16, Orientation = 32 }

    [Flags]
    public enum PlatformFeature { None = 0, Ads = 1, RewardedAds = 2, CloudSave = 4, Leaderboards = 8, Auth = 16, AsyncSessions = 32, Payments = 64, Share = 128 }

    /// <summary>Expected failures are values; only broken invariants throw.</summary>
    public readonly struct Result
    {
        public readonly bool Ok;
        public readonly ResultCategory Category;
        public readonly bool Retryable;
        public readonly string Message;
        public Result(bool ok, ResultCategory category, bool retryable, string message) { Ok = ok; Category = category; Retryable = retryable; Message = message; }
        public static readonly Result Success = new Result(true, ResultCategory.Ok, false, null);
        public static Result Fail(ResultCategory category, string message = null, bool retryable = false) => new Result(false, category, retryable, message);
        public override string ToString() => Ok ? "Ok" : $"{Category}: {Message}";
    }

    public readonly struct Result<T>
    {
        public readonly bool Ok;
        public readonly T Value;
        public readonly ResultCategory Category;
        public readonly bool Retryable;
        public readonly string Message;
        public Result(T value) { Ok = true; Value = value; Category = ResultCategory.Ok; Retryable = false; Message = null; }
        public Result(ResultCategory category, string message, bool retryable) { Ok = false; Value = default; Category = category; Retryable = retryable; Message = message; }
        public static Result<T> Success(T value) => new Result<T>(value);
        public static Result<T> Fail(ResultCategory category, string message = null, bool retryable = false) => new Result<T>(category, message, retryable);
        public override string ToString() => Ok ? $"Ok({Value})" : $"{Category}: {Message}";
    }

    public readonly struct PlayerInfo
    {
        public readonly string Id, Name, AvatarUrl;
        public readonly bool IsAuthorized;
        public PlayerInfo(string id, string name, string avatarUrl, bool isAuthorized) { Id = id; Name = name; AvatarUrl = avatarUrl; IsAuthorized = isAuthorized; }
        public static readonly PlayerInfo Guest = new PlayerInfo("guest", "Guest", null, false);
    }

    public readonly struct LeaderboardEntry
    {
        public readonly int Rank; public readonly string PlayerId, Name; public readonly long Score; public readonly string ExtraData;
        public LeaderboardEntry(int rank, string playerId, string name, long score, string extraData) { Rank = rank; PlayerId = playerId; Name = name; Score = score; ExtraData = extraData; }
    }

    public interface IAds
    {
        bool IsRewardedAvailable { get; }
        /// <summary>Always completes (Shown / NotAvailable / Error / Timeout / Suppressed).</summary>
        Task<AdResult> ShowInterstitialAsync();
        /// <summary>Value is true only when the reward was actually earned.</summary>
        Task<Result<bool>> ShowRewardedAsync();
    }

    public interface ICloudSave
    {
        Task<Result<string>> LoadAsync();
        /// <summary>Coalesced write; the platform decides when it really goes out.</summary>
        void Queue(string json);
        Task<Result> FlushAsync();
    }

    public interface ILeaderboards
    {
        Task<Result> SubmitAsync(string board, long score, string extraData = null);
        Task<Result<IReadOnlyList<LeaderboardEntry>>> GetAsync(string board, int top = 10, bool includeUser = true, int aroundUser = 3);
    }

    /// <summary>Asynchronous multiplayer (Yandex multiplayer.sessions): record my run, replay others'.</summary>
    public interface IAsyncSessions
    {
        Task<Result<IReadOnlyList<string>>> LoadOpponentsAsync(int count, string meta);
        void Commit(string payload);
        Task<Result> PushAsync(string meta);
    }

    public interface IPayments { bool IsAvailable { get; } }

    /// <summary>Optional: the host's clock (Yandex ysdk.serverTime()), so date-based rewards ignore the device clock.</summary>
    public interface IServerClock
    {
        /// <summary>Unix time in ms, or ≤ 0 when the host has not provided it (fall back to the device clock).</summary>
        long ServerTimeUnixMs { get; }
    }

    public interface IPlatformServices
    {
        PlatformKind Kind { get; }
        bool IsInitialized { get; }
        Task<Result> InitializeAsync(float deadlineSeconds = 15f);

        string LanguageCode { get; }
        DeviceKind Device { get; }
        bool IsMobile { get; }
        bool IsTV { get; }

        PlayerInfo Player { get; }
        Task<Result<bool>> RequestAuthAsync();

        void SetGameplayActive(bool active);
        void ReadyForGameplay();

        event Action<PauseReason, bool> PauseChanged;
        /// <summary>Raised after a successful sign-in: the player identity (and its cloud save) changed.</summary>
        event Action IdentityChanged;

        bool HasFeature(PlatformFeature feature);
        IAds Ads { get; }
        ICloudSave CloudSave { get; }
        ILeaderboards Leaderboards { get; }
        IAsyncSessions Sessions { get; }
        IPayments Payments { get; }
    }
}
