using NUnit.Framework;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    public class RubberBandTests
    {
        [Test]
        public void NoHelpDuringTheFirstSecondsOrWhenOff()
        {
            Assert.AreEqual(1f, RubberBand.Multiplier(-500f, false, 5.0, 1f));
            Assert.AreEqual(1f, RubberBand.Multiplier(-500f, false, 60.0, 0f));
        }

        [Test]
        public void CatchUpIsCappedAndScaledByStrength()
        {
            Assert.AreEqual(1f + RubberBand.MaxCatchUp, RubberBand.Multiplier(-1000f, false, 60.0, 1f), 1e-5f);
            Assert.AreEqual(1f + RubberBand.MaxCatchUp * 0.5f, RubberBand.Multiplier(-1000f, false, 60.0, 0.5f), 1e-5f);
            Assert.AreEqual(1f + 15f / RubberBand.GapScaleMetres, RubberBand.Multiplier(-15f, false, 60.0, 1f), 1e-5f);
        }

        [Test]
        public void HoldBackIsSmallerAndOffOnTheLastLap()
        {
            Assert.AreEqual(1f - RubberBand.MaxHoldBack, RubberBand.Multiplier(1000f, false, 60.0, 1f), 1e-5f);
            Assert.Less(RubberBand.MaxHoldBack, RubberBand.MaxCatchUp);
            Assert.AreEqual(1f, RubberBand.Multiplier(1000f, true, 60.0, 1f), "a leader on its last lap is never slowed");
            Assert.Greater(RubberBand.Multiplier(-300f, true, 60.0, 1f), 1f, "a car behind may still catch up on its last lap");
        }
    }
}
