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
        private Touchscreen touch;
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
            if (touch != null) InputSystem.RemoveDevice(touch);
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

        [UnityTest, Timeout(180000)]
        public IEnumerator TouchLaunchRequiresATimedPressAndDisappearsAfterTheStart()
        {
            yield return Boot();
            touch = InputSystem.AddDevice<Touchscreen>();
            Object.FindAnyObjectByType<TouchControlsPresenter>().forceShow = true;
            // Timed tap, early hold, and no tap: auto-throttle must never award the skill.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                flow.StartRace(System.Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
                yield return null; yield return null;
                var button = System.Array.Find(Object.FindObjectsByType<UnityEngine.UI.Button>(), b => b.name == "Launch");
                Assert.NotNull(button, "Touch players need an explicit throttle press during the countdown.");
                var rect = (RectTransform)button.transform;
                Assert.GreaterOrEqual(rect.rect.height * rect.lossyScale.y, 43.9f);
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                if (attempt == 0)
                    while (RaceClock.Now < race.countdownSeconds - 0.15f) yield return null;
                if (attempt < 2)
                {
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = attempt + 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Began, position = point });
                    yield return null; yield return null;
                    Assert.Greater(race.Player.GetComponent<PlayerDriverInput>().RawThrottle, 0.9f);
                }
                while (RaceClock.Now < race.countdownSeconds + 0.6f) yield return null;
                Assert.AreEqual(attempt == 0, launch.Awarded, "Only a fresh press near GO earns the boost.");
                Assert.IsFalse(button.gameObject.activeInHierarchy, "The launch action must leave the driving view.");
                if (attempt < 2)
                    InputSystem.QueueStateEvent(touch, new TouchState { touchId = attempt + 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point });
                yield return null; yield return null;
                Assert.Less(race.Player.GetComponent<PlayerDriverInput>().RawThrottle, 0.01f);
            }
        }
    }
}
