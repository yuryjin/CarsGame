using NUnit.Framework;
using RacingGame.Core.Onboarding;

namespace RacingGame.Tests
{
    public class HintTrackerTests
    {
        [Test]
        public void EachHintShowsOnce()
        {
            var t = new HintTracker(0, 0f);
            Assert.IsTrue(t.TryShow(Hint.Grass, 1f));
            Assert.IsFalse(t.TryShow(Hint.Grass, 100f));
            Assert.IsTrue(t.Seen(Hint.Grass));
            Assert.IsFalse(t.Seen(Hint.UnderFire));
        }

        [Test]
        public void CooldownDefersWithoutConsuming()
        {
            var t = new HintTracker(0, 6f);
            Assert.IsTrue(t.TryShow(Hint.Grass, 10f));
            Assert.IsFalse(t.TryShow(Hint.PickupNitro, 12f), "second tip inside the cooldown");
            Assert.IsFalse(t.Seen(Hint.PickupNitro), "a deferred tip stays unseen");
            Assert.IsTrue(t.TryShow(Hint.PickupNitro, 16.5f));
        }

        [Test]
        public void MaskRoundTripsThroughSave()
        {
            var t = new HintTracker(0, 0f);
            t.TryShow(Hint.AimFire, 0f); t.TryShow(Hint.PickupAmmo, 0f);
            var restored = new HintTracker(t.Mask, 0f);
            Assert.IsTrue(restored.Seen(Hint.AimFire));
            Assert.IsTrue(restored.Seen(Hint.PickupAmmo));
            Assert.IsFalse(restored.TryShow(Hint.AimFire, 50f));
            Assert.IsTrue(restored.TryShow(Hint.Grass, 50f));
        }
    }
}
