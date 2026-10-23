using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class RecoveryIntentPlayTests
    {
        private float capture;
        private int frameRate;
        private string saveBefore;
        private GameFlow flow;
        private RaceEventDefinition practice;

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; frameRate = Application.targetFrameRate; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (saveBefore != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saveBefore, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (practice != null) Object.DestroyImmediate(practice);
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
            Application.targetFrameRate = frameRate;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DeliberateStopsDoNotResetButBlockedAccelerationStillRecovers()
        {
            // The onboarding timeout deliberately uses unscaled real time, not simulation ticks.
            Time.captureDeltaTime = 0f;
            Application.targetFrameRate = 60;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saveBefore = JsonUtility.ToJson(PlatformGlue.Save.Data);
            foreach (var entry in PlatformGlue.Save.Data.progress.ledger) entry.races = 0;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 0; practice.pickupCount = 0;
            flow.StartRace(practice, null);
            var car = race.Player.Car;
            race.Player.offTrackResetSeconds = 999f; // Isolate the onboarding recovery from the separate off-road rule.
            var input = car.gameObject.AddComponent<ScriptedDriverInput>();
            input.Set(0f, 0f, true);
            car.SetInputSource(input);
            while (race.State == RaceState.Countdown) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            // Hold position on the road, then vary only the driver's intent. This simulates being
            // stationary without depending on a particular wall collision or track decoration.
            car.Body.constraints = RigidbodyConstraints.FreezeAll;
            int before = race.Player.Resets;
            yield return new WaitForSecondsRealtime(5.2f);
            Assert.AreEqual(before, race.Player.Resets, "Holding the brake is a deliberate stop, not being stuck");
            input.Set(0f, 0f);
            yield return new WaitForSecondsRealtime(5.2f);
            Assert.AreEqual(before, race.Player.Resets, "An idle manual-throttle driver must retain control");
            input.Set(1f, 0f);
            yield return new WaitForSecondsRealtime(5.2f);
            Assert.AreEqual(before + 1, race.Player.Resets, "A newcomer trying to accelerate while blocked still gets one recovery");
            yield return new WaitForSecondsRealtime(5.2f);
            Assert.AreEqual(before + 1, race.Player.Resets, "Recovery must stay latched while the car remains stationary");
        }
    }
}
