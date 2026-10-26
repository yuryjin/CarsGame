using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public sealed class TouchPreferencesPlayTests
    {
        private float capture;
        private string saved;
        private GameFlow flow;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;
        private Touchscreen syntheticTouch;
        private Keyboard syntheticKeyboard;

        [SetUp] public void Setup()
        {
            capture = Time.captureDeltaTime;
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (syntheticTouch != null) InputSystem.RemoveDevice(syntheticTouch);
            if (syntheticKeyboard != null) InputSystem.RemoveDevice(syntheticKeyboard);
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            Time.captureDeltaTime = capture;
            PauseController.ResetAll();
        }

        private static Button Button(string name) => System.Array.Find(Object.FindObjectsByType<Button>(), b => b.name == name);
        private static Bounds Bounds(RectTransform rect) => RectTransformUtility.CalculateRelativeRectTransformBounds(rect.root, rect);

        [UnityTest, Timeout(30000)]
        public IEnumerator MouseAndMouseEmulatedTouchesRecoverWhenTheUiLosesPointerUp()
        {
            syntheticTouch = InputSystem.AddDevice<Touchscreen>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var target = new GameObject("MouseReleaseTest");
            try
            {
                var button = target.AddComponent<TouchButton>();
                button.controlPath = "<Gamepad>/dpad/left";
                Vector2 point = new Vector2(-100f, -100f);
                foreach (bool emulatedTouch in new[] { false, true })
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
                    if (emulatedTouch)
                        InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = 1,
                            phase = UnityEngine.InputSystem.TouchPhase.Began, position = point });
                    yield return null; yield return null;
                    button.OnPointerDown(new ExtendedPointerEventData(EventSystem.current) {
                        pointerId = -1, device = mouse, position = point, button = PointerEventData.InputButton.Left });
                    for (int i = 0; i < 3; i++) yield return null;
                    Assert.IsTrue(((ButtonControl)button.control).isPressed);
                    if (emulatedTouch)
                        InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = 1,
                            phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point });
                    else InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                    // No PointerUp. The emulated Mouse deliberately stays pressed after the finger lifts.
                    for (int i = 0; i < 3; i++) yield return null;
                    Assert.IsFalse(((ButtonControl)button.control).isPressed,
                        emulatedTouch ? "The physical finger wins over a stale emulated Mouse" : "Physical mouse release recovers the control");
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
                    yield return null; yield return null;
                }
            }
            finally { Object.DestroyImmediate(target); InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator PhysicalTouchReleaseRecoversAMissingPointerUpWithoutReleasingAnotherFinger()
        {
            syntheticTouch = InputSystem.AddDevice<Touchscreen>();
            var target = new GameObject("TouchReleaseTest");
            try
            {
                var button = target.AddComponent<TouchButton>();
                button.controlPath = "<Gamepad>/dpad/left";
                var first = new ExtendedPointerEventData(EventSystem.current) { pointerId = 1001, touchId = 1, device = syntheticTouch };
                var second = new ExtendedPointerEventData(EventSystem.current) { pointerId = 1002, touchId = 2, device = syntheticTouch };
                // Off-screen physical touches let this test deliberately omit the UI release callback.
                InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = 1,
                    phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(-100f, -100f) });
                InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = 2,
                    phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(-200f, -100f) });
                yield return null; yield return null;
                button.OnPointerDown(first); button.OnPointerDown(second);
                for (int i = 0; i < 3; i++) yield return null;
                Assert.IsTrue(((ButtonControl)button.control).isPressed);
                InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = 1,
                    phase = UnityEngine.InputSystem.TouchPhase.Ended, position = new Vector2(-100f, -100f) });
                button.OnPointerUp(first);
                for (int i = 0; i < 3; i++) yield return null;
                Assert.IsTrue(((ButtonControl)button.control).isPressed, "The other finger still holds the same button");
                InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = 2,
                    phase = UnityEngine.InputSystem.TouchPhase.Ended, position = new Vector2(-200f, -100f) });
                // Intentionally no button.OnPointerUp(second).
                for (int i = 0; i < 3; i++) yield return null;
                Assert.IsFalse(((ButtonControl)button.control).isPressed, "The virtual control must follow the ended physical touch");
            }
            finally { Object.DestroyImmediate(target); }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RealTouchCanRestoreControlsAfterKeyboardDrivingWithoutReload()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var controls = Object.FindAnyObjectByType<TouchControlsPresenter>();
            controls.forceShow = false;
            syntheticTouch = InputSystem.AddDevice<Touchscreen>();
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>();
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
            while (race.State == RaceState.Countdown) yield return null;
            race.Player.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.W));
                for (int i = 0; i < 5; i++) yield return null;
                Assert.IsFalse(TouchControlsPresenter.IsShown, "Driving with the keyboard hides touch controls");
                var driver = race.Player.GetComponent<PlayerDriverInput>();
                Assert.Greater(race.Player.Car.LastCommand.Throttle, 0f,
                    $"cycle={cycle} raw={driver.RawThrottle} actionMap={driver.UsingActionMap} key={syntheticKeyboard.wKey.isPressed} current={Keyboard.current == syntheticKeyboard} pause={PauseController.IsPaused} carInput={race.Player.Car.inputEnabled}");
                InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = cycle + 1,
                    phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(Screen.width * .5f, Screen.height * .5f) });
                yield return null; yield return null;
                Assert.IsTrue(TouchControlsPresenter.IsShown, "A new real touch must restore the controls");
                Assert.NotNull(Button("Right"));
                InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = cycle + 1,
                    phase = UnityEngine.InputSystem.TouchPhase.Ended, position = new Vector2(Screen.width * .5f, Screen.height * .5f) });
                yield return null; yield return null;
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FirstTouchOnPauseAfterKeyboardDrivingStillPauses()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            Object.FindAnyObjectByType<TouchControlsPresenter>().forceShow = false;
            syntheticTouch = InputSystem.AddDevice<Touchscreen>();
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>();
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
            while (race.State == RaceState.Countdown) yield return null;
            race.Player.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.W));
                for (int i = 0; i < 5; i++) yield return null;
                Assert.IsFalse(TouchControlsPresenter.IsShown);
                InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
                yield return null;
                var rect = (RectTransform)Button("PauseButton").transform;
                Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = cycle + 1,
                    phase = UnityEngine.InputSystem.TouchPhase.Began, position = point });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = cycle + 1,
                    phase = UnityEngine.InputSystem.TouchPhase.Ended, position = point });
                for (int i = 0; i < 3; i++) yield return null;
                Assert.IsTrue(PauseController.IsPaused, "The first touch must activate the visible pause button, even as the input layout changes");
                Assert.NotNull(Button("Resume"));
                InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
                for (int i = 0; i < 3; i++) yield return null;
                Assert.IsFalse(PauseController.IsPaused);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator MirroredAndLargeControlsKeepCorrectInputsAndClearTheSpeedometer()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var touch = Object.FindAnyObjectByType<TouchControlsPresenter>();
            touch.forceShow = true;
            syntheticTouch = InputSystem.AddDevice<Touchscreen>();
            float normalWidth = 0f;
            foreach (var prefs in new[] { (large: false, left: false), (large: true, left: false), (large: true, left: true), (large: false, left: true) })
            {
                bool large = prefs.large, left = prefs.left;
                var settings = PlatformGlue.Save.Data.settings;
                settings.touchLargeButtons = large; settings.touchLeftHanded = left;
                Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
                while (race.State == RaceState.Countdown) yield return null;
                race.Player.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                for (int i = 0; i < 5; i++) yield return null;
                var leftButton = (RectTransform)Button("Left").transform;
                var rightButton = (RectTransform)Button("Right").transform;
                var fireButton = (RectTransform)Button("Fire").transform;
                var dial = (RectTransform)GameObject.Find("Speedometer").transform;
                Assert.Less(leftButton.position.x, rightButton.position.x, "Arrow order must not reverse with handedness");
                Assert.AreEqual(left, fireButton.position.x < leftButton.position.x);
                Assert.AreEqual(left, dial.position.x > fireButton.position.x);
                var speed = Bounds(dial);
                Assert.GreaterOrEqual(speed.min.y, Bounds(leftButton).max.y + 1f, "Speed display clears the steering buttons");
                if (!large) normalWidth = leftButton.rect.width;
                else Assert.GreaterOrEqual(leftButton.rect.width, normalWidth * 1.24f);
                foreach (var name in new[] { "Brake", "SwapWeapon", "Reset", "PauseButton" })
                {
                    var rect = (RectTransform)Button(name).transform;
                    Assert.GreaterOrEqual(rect.rect.height * rect.lossyScale.y, 43.9f, name);
                }
                var reset = (RectTransform)Button("Reset").transform;
                var pause = (RectTransform)Button("PauseButton").transform;
                Assert.Less(reset.position.y, pause.position.y, "Recovery stays below the stationary pause action");
                var resetCorners = new Vector3[4]; var pauseCorners = new Vector3[4];
                reset.GetWorldCorners(resetCorners); pause.GetWorldCorners(pauseCorners);
                Assert.LessOrEqual(resetCorners[1].y + 7.9f, pauseCorners[0].y, "Separate adjacent touch targets");
                foreach (var name in new[] { "Left", "Right", "Fire" })
                {
                    var button = Button(name);
                    var pointer = new PointerEventData(EventSystem.current) { position = button.transform.position };
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                    for (int i = 0; i < 5; i++) yield return null;
                    if (name == "Fire") Assert.IsTrue(race.Player.Car.LastCommand.Fire);
                    else Assert.AreEqual(name == "Left" ? -1f : 1f, race.Player.Car.LastCommand.Steer, 0.01f);
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                    for (int i = 0; i < 3; i++) yield return null;
                }
                foreach (var name in new[] { "Left", "Right" })
                {
                    Vector2 steerPoint = RectTransformUtility.WorldToScreenPoint(null, Button(name).transform.position);
                    Vector2 firePoint = RectTransformUtility.WorldToScreenPoint(null, fireButton.position);
                    InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Began, position = steerPoint });
                    InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = 2,
                        phase = UnityEngine.InputSystem.TouchPhase.Began, position = firePoint });
                    for (int i = 0; i < 8; i++) yield return null;
                    Assert.AreEqual(name == "Left" ? -1f : 1f, race.Player.Car.LastCommand.Steer, 0.01f,
                        $"Actual two-finger {name}: large={large}, left={left}");
                    Assert.IsTrue(race.Player.Car.LastCommand.Fire, "The second finger fires while steering");
                    InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = 1,
                        phase = UnityEngine.InputSystem.TouchPhase.Ended, position = steerPoint });
                    InputSystem.QueueStateEvent(syntheticTouch, new TouchState { touchId = 2,
                        phase = UnityEngine.InputSystem.TouchPhase.Ended, position = firePoint });
                    for (int i = 0; i < 8; i++) yield return null;
                    Assert.LessOrEqual(Mathf.Abs(race.Player.Car.LastCommand.Steer), 0.351f, "Both fingers release before the next direction");
                    Assert.IsFalse(race.Player.Car.LastCommand.Fire);
                }
                var copy = SaveMigrator.FromJson(SaveMigrator.ToJson(PlatformGlue.Save.Data));
                Assert.AreEqual(left, copy.settings.touchLeftHanded);
                Assert.AreEqual(large, copy.settings.touchLargeButtons);
                flow.EnterMenu();
                for (int i = 0; i < 5; i++) yield return null;
            }
        }
    }
}
