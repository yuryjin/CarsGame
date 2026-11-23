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

        private IEnumerator Boot(bool ordinaryRace = true)
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            launch = Object.FindAnyObjectByType<PerfectStart>();
            if (!ordinaryRace) yield break;
            flow.StartRace(System.Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
            while (RaceClock.Now < race.countdownSeconds - 0.15f) yield return null;
            Assert.AreEqual(RaceState.Countdown, race.State);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TrainingMeasuresEarlyOnTimeAndLatePressesAndResetsOnRetry()
        {
            yield return Boot(false);
            GameObject.Find("Practice").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return null;
            var entry = GameObject.Find("PracticeLaunch");
            Assert.NotNull(entry, "The training menu must offer the launch drill");
            entry.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            string progress = JsonUtility.ToJson(PlatformGlue.Save.Data.progress);
            foreach (float target in new[] { -.50f, 0f, .50f })
            {
                yield return null;
                Assert.AreEqual(RaceState.Countdown, race.State);
                Assert.IsFalse(launch.Awarded);
                var display = GameObject.Find("ReturnToGarage").GetComponentInChildren<TMPro.TMP_Text>();
                StringAssert.Contains(Loc.Get("practice.launch"), display.text);
                double pressedAt;
                if (target == 0f)
                {
                    void PressOnGo()
                    {
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                        InputSystem.Update();
                    }
                    race.OnRaceStarted += PressOnGo;
                    while (race.State == RaceState.Countdown) yield return null;
                    race.OnRaceStarted -= PressOnGo;
                    pressedAt = race.RaceStartTime;
                    yield return null;
                }
                else
                {
                    if (target < 0f)
                        while (RaceClock.Now < race.countdownSeconds + target) yield return null;
                    else
                        while (race.State == RaceState.Countdown || race.RaceTime < target) yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    yield return null;
                    pressedAt = RaceClock.Now;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                while (race.State == RaceState.Countdown) yield return null;
                for (int frame = 0; frame < 5; frame++) yield return null;
                bool hit = target == 0f;
                Assert.AreEqual(hit, launch.Awarded);
                Assert.AreEqual(hit, race.Player.Car.IsBoosting);
                Assert.AreEqual(hit, flow.PracticeCompleted);
                var match = System.Text.RegularExpressions.Regex.Match(display.text, @"[+-]?\d+");
                Assert.IsTrue(match.Success, "Expected the signed timing error on screen: " + display.text);
                int shown = int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture);
                Assert.AreEqual((pressedAt - race.RaceStartTime) * 1000d, shown, 24d);
                if (target < 0f) Assert.Less(shown, -launch.earlySeconds * 1000f);
                if (target > 0f) Assert.Greater(shown, launch.lateSeconds * 1000f);
                StringAssert.Contains(Loc.Format(hit ? "practice.launch.hit" : "practice.launch.miss", "ms", match.Value), display.text);
                Debug.Log($"[LaunchTraining] expected={target:F2}s shown={shown}ms boost={race.Player.Car.IsBoosting}");
                GameObject.Find("PracticeRetry").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            }

            yield return null;
            while (RaceClock.Now < race.countdownSeconds - .15f) yield return null;
            PauseController.Set(PauseReason.User, true);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            for (int frame = 0; frame < 5; frame++) yield return null;
            PauseController.Set(PauseReason.User, false);
            while (RaceClock.Now < race.countdownSeconds + .6f) yield return null;
            Assert.IsFalse(launch.Awarded);
            Assert.IsFalse(race.Player.Car.IsBoosting);
            StringAssert.Contains(Loc.Get("practice.launch"),
                GameObject.Find("ReturnToGarage").GetComponentInChildren<TMPro.TMP_Text>().text);
            Assert.AreEqual(progress, JsonUtility.ToJson(PlatformGlue.Save.Data.progress));
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
