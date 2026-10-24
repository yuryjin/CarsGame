using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class PerfectStartPausePlayTests
    {
        private Keyboard keyboard;
        private GameFlow flow;
        private RaceManager race;
        private PerfectStart launch;
        private float capture;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [SetUp] public void Setup()
        {
            capture = Time.captureDeltaTime;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            PauseController.ResetAll();
        }

        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            InputSystem.RemoveDevice(keyboard);
            Time.captureDeltaTime = capture;
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            PauseController.ResetAll();
        }

        private IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            launch = Object.FindAnyObjectByType<PerfectStart>();
            flow.StartRace(System.Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
            while (RaceClock.Now < race.countdownSeconds - 0.15f) yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ThrottleHeldDuringPauseCannotBecomeAPerfectLaunchOnResume()
        {
            yield return Boot();
            PauseController.Set(PauseReason.User, true);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            for (int i = 0; i < 10; i++) yield return null;
            PauseController.Set(PauseReason.User, false);
            while (RaceClock.Now < race.countdownSeconds + 0.6f) yield return null;
            Assert.IsFalse(launch.Awarded, "Holding gas in the pause menu is not a timed press at GO.");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator AFreshThrottlePressAfterResumeStillEarnsAPerfectLaunch()
        {
            yield return Boot();
            PauseController.Set(PauseReason.User, true);
            for (int i = 0; i < 5; i++) yield return null;
            PauseController.Set(PauseReason.User, false);
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            while (RaceClock.Now < race.countdownSeconds + 0.6f) yield return null;
            Assert.IsTrue(launch.Awarded, "A deliberate post-resume press inside the launch window still counts.");
        }
    }
}
