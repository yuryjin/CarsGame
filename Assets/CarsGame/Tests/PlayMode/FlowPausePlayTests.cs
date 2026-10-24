using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class FlowPausePlayTests
    {
        private GameFlow flow;
        private RaceManager race;

        [TearDown]
        public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            PauseController.ResetAll();
        }

        private IEnumerator Boot()
        {
            PauseController.ResetAll();
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            flow.StartRace(System.Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
            Assert.AreEqual(RaceState.Countdown, race.State);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator MenuAndRetryClearUserPauseButPreservePlatformReasons()
        {
            yield return Boot();
            const PauseReason external = PauseReason.Hidden | PauseReason.HostPause | PauseReason.Ad;
            PauseController.Set(external | PauseReason.User, true);
            flow.EnterMenu();
            Assert.AreEqual(external, PauseController.Reasons, "Leaving a race does not own platform/visibility pause reasons.");
            Assert.IsTrue(RaceClock.Paused);
            Assert.IsTrue(AudioListener.pause);
            Assert.AreEqual(0f, Time.timeScale);
            flow.RetryRace();
            Assert.AreEqual(RaceState.Countdown, race.State);
            Assert.AreEqual(external, PauseController.Reasons);
            double time = RaceClock.Now;
            for (int i = 0; i < 8; i++) yield return null;
            Assert.AreEqual(time, RaceClock.Now, "A new countdown cannot advance under a platform pause.");
            PauseController.Set(PauseReason.Hidden, false);
            Assert.IsTrue(PauseController.IsPaused, "Showing the tab must not dismiss an ongoing ad or host pause.");
            PauseController.Set(PauseReason.Ad | PauseReason.HostPause, false);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(RaceClock.Paused);
            float until = Time.realtimeSinceStartup + 10f;
            while (race.State == RaceState.Countdown && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(RaceState.Racing, race.State, "Countdown must resume after its final pause owner releases it.");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RaceClockResetPreservesAnActiveVisibilityPause()
        {
            yield return Boot();
            PauseController.Set(PauseReason.Hidden, true);
            race.StartRace();
            Assert.AreEqual(PauseReason.Hidden, PauseController.Reasons);
            Assert.IsTrue(RaceClock.Paused, "Resetting race time must not independently unpause its clock.");
            double time = RaceClock.Now;
            for (int i = 0; i < 8; i++) yield return null;
            Assert.AreEqual(time, RaceClock.Now);
        }
    }
}
