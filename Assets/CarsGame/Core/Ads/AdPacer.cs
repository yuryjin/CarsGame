namespace RacingGame.Core.Ads
{
    /// <summary>
    /// Decides when an interstitial may be shown. Pure logic (seconds are realtime):
    /// never during gameplay, never before the second results screen of a session, at least
    /// <see cref="MinIntervalSeconds"/> since the last interstitial, at least
    /// <see cref="MinSessionSeconds"/> into the session, and not right after a rewarded ad.
    /// </summary>
    public sealed class AdPacer
    {
        public float MinIntervalSeconds = 75f;
        public float MinSessionSeconds = 60f;
        public float RewardedCooldownSeconds = 30f;

        public double SessionStart { get; }
        public double LastInterstitialAt { get; private set; } = double.NegativeInfinity;
        public double LastRewardedAt { get; private set; } = double.NegativeInfinity;
        public int RacesFinished { get; private set; }

        public AdPacer(double sessionStart) { SessionStart = sessionStart; }

        public void OnRaceFinished() { RacesFinished++; }
        public void OnInterstitialShown(double now) { LastInterstitialAt = now; }
        public void OnRewardedShown(double now) { LastRewardedAt = now; }

        public bool CanShowInterstitial(double now, bool inGameplay)
        {
            if (inGameplay) return false;
            if (RacesFinished < 2) return false;
            if (now - SessionStart < MinSessionSeconds) return false;
            if (now - LastInterstitialAt < MinIntervalSeconds) return false;
            if (now - LastRewardedAt < RewardedCooldownSeconds) return false;
            return true;
        }
    }
}
