using NUnit.Framework;
using RacingGame.Core.Ads;

namespace RacingGame.Tests
{
    public class AdPacerTests
    {
        [Test]
        public void NoInterstitialBeforeSecondResultsOrDuringGameplay()
        {
            var p = new AdPacer(0);
            Assert.IsFalse(p.CanShowInterstitial(500, false));
            p.OnRaceFinished();
            Assert.IsFalse(p.CanShowInterstitial(500, true));
            Assert.IsFalse(p.CanShowInterstitial(500, false));
            p.OnRaceFinished();
            Assert.IsTrue(p.CanShowInterstitial(500, false));
        }

        [Test]
        public void RespectsSessionAndIntervalPacing()
        {
            var p = new AdPacer(0); p.OnRaceFinished(); p.OnRaceFinished();
            Assert.IsFalse(p.CanShowInterstitial(30, false), "too early in the session");
            Assert.IsTrue(p.CanShowInterstitial(61, false));
            p.OnInterstitialShown(61);
            Assert.IsFalse(p.CanShowInterstitial(120, false));
            Assert.IsTrue(p.CanShowInterstitial(61 + 75, false));
        }

        [Test]
        public void NotRightAfterRewarded()
        {
            var p = new AdPacer(0); p.OnRaceFinished(); p.OnRaceFinished();
            p.OnRewardedShown(100);
            Assert.IsFalse(p.CanShowInterstitial(110, false));
            Assert.IsTrue(p.CanShowInterstitial(131, false));
        }
    }
}
