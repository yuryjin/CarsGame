using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using RacingGame.Core.Platform;

namespace RacingGame.Platform.Mock
{
    /// <summary>
    /// Desktop / no-store build: no ads (pacer sees Suppressed), local save only, guest player.
    /// Also the base class for the Editor and Web mocks.
    /// </summary>
    public class StandalonePlatform : IPlatformServices, ICloudSave
    {
        public virtual PlatformKind Kind => PlatformKind.Standalone;
        public bool IsInitialized { get; protected set; }
        public virtual string LanguageCode => Normalize(Application.systemLanguage);
        public virtual DeviceKind Device => Application.isMobilePlatform ? DeviceKind.Mobile : DeviceKind.Desktop;
        public bool IsMobile => Device == DeviceKind.Mobile || Device == DeviceKind.Tablet;
        public bool IsTV => Device == DeviceKind.TV;
        public PlayerInfo Player { get; protected set; } = PlayerInfo.Guest;
        public event Action<PauseReason, bool> PauseChanged;
        public event Action IdentityChanged;
        protected void RaiseIdentityChanged() => IdentityChanged?.Invoke();
        public virtual IAds Ads => null;
        public virtual ICloudSave CloudSave => this;
        public virtual ILeaderboards Leaderboards => null;
        public virtual IAsyncSessions Sessions => null;
        public virtual IPayments Payments => null;

        protected bool gameplayActive;
        protected string cloudBlob;   // in-memory "cloud" for mocks
        public int GameplayStarts { get; private set; }
        public int GameplayStops { get; private set; }
        public bool ReadyCalled { get; private set; }

        public virtual Task<Result> InitializeAsync(float deadlineSeconds = 15f)
        {
            IsInitialized = true;
            return Task.FromResult(Result.Success);
        }

        public virtual Task<Result<bool>> RequestAuthAsync() => Task.FromResult(Result<bool>.Success(Player.IsAuthorized));

        public void SetGameplayActive(bool active)
        {
            if (active == gameplayActive) return;    // balanced by construction
            gameplayActive = active;
            if (active) GameplayStarts++; else GameplayStops++;
            OnGameplayChanged(active);
        }
        protected virtual void OnGameplayChanged(bool active) { }

        public void ReadyForGameplay()
        {
            if (ReadyCalled) return;
            ReadyCalled = true;
            OnReady();
        }
        protected virtual void OnReady() { }

        public virtual bool HasFeature(PlatformFeature feature) => (feature & PlatformFeature.CloudSave) != 0 && CloudSave != null;

        protected void RaisePause(PauseReason reason, bool paused) => PauseChanged?.Invoke(reason, paused);

        // ICloudSave (mock: memory)
        public virtual Task<Result<string>> LoadAsync() => Task.FromResult(Result<string>.Success(cloudBlob));
        public virtual void Queue(string json) { cloudBlob = json; }
        public virtual Task<Result> FlushAsync() => Task.FromResult(Result.Success);

        protected static string Normalize(SystemLanguage lang)
        {
            switch (lang)
            {
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Turkish: return "tr";
                case SystemLanguage.Ukrainian: return "uk";
                case SystemLanguage.German: return "de";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.Spanish: return "es";
                case SystemLanguage.Portuguese: return "pt";
                case SystemLanguage.Italian: return "it";
                case SystemLanguage.Polish: return "pl";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.ChineseSimplified: case SystemLanguage.Chinese: case SystemLanguage.ChineseTraditional: return "zh";
                case SystemLanguage.Arabic: return "ar";
                case SystemLanguage.Hebrew: return "he";
                case SystemLanguage.Vietnamese: return "vi";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Hungarian: return "hu";
                case SystemLanguage.Czech: return "cs";
                case SystemLanguage.Romanian: return "ro";
                case SystemLanguage.Dutch: return "nl";
                case SystemLanguage.Bulgarian: return "bg";
                case SystemLanguage.Catalan: return "ca";
                case SystemLanguage.Slovak: return "sk";
                default: return "en";
            }
        }
    }
}
