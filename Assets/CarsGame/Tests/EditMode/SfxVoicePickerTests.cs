using NUnit.Framework;

namespace RacingGame.Tests
{
    public class SfxVoicePickerTests
    {
        private static readonly object Shot = new object(), Finish = new object(), Hit = new object();

        [Test]
        public void FreeVoicesAreUsedFirst()
        {
            var p = new SfxVoicePicker(3, 3);
            Assert.AreEqual(0, p.Pick(Shot, 1f, 1f, 0f));
            Assert.AreEqual(1, p.Pick(Hit, 1f, 1f, 0f));
            Assert.AreEqual(2, p.Pick(Finish, 1f, 1f, 0f));
        }

        [Test]
        public void FinishedVoiceIsFreeAgain()
        {
            var p = new SfxVoicePicker(1, 1);
            Assert.AreEqual(0, p.Pick(Shot, 1f, 0.2f, 0f));
            Assert.IsFalse(p.IsBusy(0, 0.3f));
            Assert.AreEqual(0, p.Pick(Hit, 0.1f, 0.2f, 0.3f));
        }

        [Test]
        public void QuieterSoundIsDroppedWhenAllVoicesAreLouder()
        {
            var p = new SfxVoicePicker(2, 4);
            p.Pick(Finish, 1f, 1f, 0f);
            p.Pick(Hit, 1f, 1f, 0f);
            Assert.AreEqual(-1, p.Pick(Shot, 0.3f, 0.2f, 0.1f));
        }

        [Test]
        public void LouderSoundStealsTheQuietestVoice()
        {
            var p = new SfxVoicePicker(2, 4);
            p.Pick(Finish, 1f, 1f, 0f);
            p.Pick(Shot, 0.2f, 1f, 0f);
            Assert.AreEqual(1, p.Pick(Hit, 0.6f, 0.2f, 0.1f));
        }

        [Test]
        public void AFadingVoiceCountsAsQuieter()
        {
            var p = new SfxVoicePicker(2, 4);
            p.Pick(Finish, 1f, 1f, 0f);    // at t=0.9 still 0.1 left
            p.Pick(Hit, 0.5f, 10f, 0f);    // at t=0.9 still ~0.46 left
            Assert.AreEqual(0, p.Pick(Shot, 0.3f, 0.2f, 0.9f));
        }

        [Test]
        public void OneClipCannotTakeMoreThanItsCap()
        {
            var p = new SfxVoicePicker(8, 2);
            Assert.AreEqual(0, p.Pick(Shot, 0.5f, 1f, 0f));
            Assert.AreEqual(1, p.Pick(Shot, 0.4f, 1f, 0f));
            // Third shot replaces the quieter of its own two voices, not a free one.
            Assert.AreEqual(1, p.Pick(Shot, 0.45f, 1f, 0.01f));
            // A quieter copy than both is dropped even though six voices are free.
            Assert.AreEqual(-1, p.Pick(Shot, 0.1f, 1f, 0.02f));
            Assert.AreEqual(2, p.Pick(Hit, 0.1f, 1f, 0.02f));
        }

        [Test]
        public void GunfireCannotEvictAFinishOnceTheCapIsReached()
        {
            var p = new SfxVoicePicker(4, 3);
            p.Pick(Finish, 0.8f, 1f, 0f);
            for (int i = 0; i < 20; i++) p.Pick(Shot, 1f, 0.14f, 0.01f * i);
            Assert.IsTrue(p.IsBusy(0, 0.2f), "the finish voice must survive a burst of louder shots");
            Assert.Greater(p.Remaining(0, 0.2f), 0f);
        }
    }
}
