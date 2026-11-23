using System.Collections;
using NUnit.Framework;
using TMPro;
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

        [UnityTest, Timeout(120000)]
        public IEnumerator RearViewPreservesSteeringAndMinesAndClearsOnPauseOrFocusLoss()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            syntheticKeyboard = InputSystem.AddDevice<Keyboard>();
            flow.StartRace(System.Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            var car = race.Player.Car;
            var driver = car.GetComponent<PlayerDriverInput>();
            var camera = Camera.main;
            car.Weapons.secondaryType = SecondaryWeaponType.Mines; car.Weapons.RefillSecondaryAmmo();
            var rival = System.Linq.Enumerable.First(race.Participants, p => !p.isPlayer).Car;
            rival.inputEnabled = false; rival.Body.constraints = RigidbodyConstraints.FreezeAll;
            rival.Teleport(car.transform.position - car.transform.forward * 18f, car.transform.rotation);
            car.Body.linearVelocity = car.transform.forward * 11f;
            Vector3 start = car.transform.forward;
            float ViewDirection() => Vector3.Dot(camera.transform.forward, car.transform.forward);
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.V, Key.W, Key.D, Key.E));
            for (int frame = 0; frame < 15; frame++) yield return null;
            Assert.IsTrue(driver.RearViewHeld);
            Assert.Less(ViewDirection(), -.72f);
            Vector3 point = camera.WorldToViewportPoint(rival.transform.position + Vector3.up * .6f);
            Assert.Greater(point.z, 0f); Assert.That(point.x, Is.InRange(0f, 1f)); Assert.That(point.y, Is.InRange(0f, 1f));
            Assert.Greater(car.LastCommand.Steer, .8f);
            Assert.Greater(Vector3.Angle(start, car.transform.forward), 1f);
            Assert.IsTrue(System.Linq.Enumerable.Any(RaceDirector.Instance.LiveMineHazards, mine => mine.Owner == car.gameObject));
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null; yield return null;
            Assert.Greater(ViewDirection(), .72f);
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.V));
            yield return null; yield return null;
            Assert.IsTrue(driver.RearViewHeld);
            PauseController.Set(RacingGame.Core.Platform.PauseReason.User, true);
            yield return null; yield return null;
            Assert.IsFalse(driver.RearViewHeld); Assert.Greater(ViewDirection(), .72f);
            PauseController.Set(RacingGame.Core.Platform.PauseReason.User, false);
            yield return null; yield return null;
            Assert.IsFalse(driver.RearViewHeld, "Holding the old press cannot re-open the rear camera after pause");
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null; yield return null;
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState(Key.V));
            yield return null; yield return null;
            Assert.IsTrue(driver.RearViewHeld);
            driver.SendMessage("OnApplicationFocus", false);
            InputSystem.QueueStateEvent(syntheticKeyboard, new KeyboardState());
            yield return null; yield return null;
            driver.SendMessage("OnApplicationFocus", true);
            yield return null; yield return null;
            Assert.IsFalse(driver.RearViewHeld); Assert.Greater(ViewDirection(), .72f);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LargerTextAndTouchTargetsCanBeChosenIndependently()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            var settings = PlatformGlue.Save.Data.settings;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            Object.FindAnyObjectByType<TouchControlsPresenter>().forceShow = true;
            float Size(string name)
            {
                var label = GameObject.Find(name).GetComponent<TMP_Text>();
                label.ForceMeshUpdate();
                Assert.IsFalse(label.isTextTruncated, name + " " + label.text);
                Assert.Greater(label.textInfo.characterCount, 0, name);
                return label.textInfo.characterInfo[0].pointSize * label.rectTransform.lossyScale.y;
            }
            foreach (string lang in new[] { "de", "fr", "uk" })
            {
                PlatformGlue.SetLanguageOverride(lang);
                while (Loc.Language != lang && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(lang, Loc.Language);
                settings.largeText = settings.touchLargeButtons = false;
                flow.StartRace(System.Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
                while (race.State == RaceState.Countdown) yield return null;
                Button("LiveGoals").onClick.Invoke(); yield return null;
                float baseAmmo = Size("Ammo"), baseGoals = Size("Goals");
                var target = (RectTransform)Button("Left").transform;
                var standard = target.rect.size;
                Button("PauseButton").onClick.Invoke(); yield return null;
                Button("PauseSettings").onClick.Invoke(); yield return null;
                Button("LargeTextOn").onClick.Invoke(); yield return null;
                Button("Back").onClick.Invoke(); yield return null;
                Button("Resume").onClick.Invoke(); yield return null;
                Assert.AreEqual(standard, target.rect.size);
                Assert.Greater(Size("Ammo"), baseAmmo * 1.1f);
                Assert.Greater(Size("Goals"), baseGoals * 1.2f);
                Button("PauseButton").onClick.Invoke(); yield return null;
                Button("PauseSettings").onClick.Invoke(); yield return null;
                Button("LargeTextOff").onClick.Invoke(); yield return null;
                Button("TouchLargeOn").onClick.Invoke(); yield return null;
                Button("Back").onClick.Invoke(); yield return null;
                Button("Resume").onClick.Invoke(); yield return null;
                Assert.Greater(target.rect.width, standard.x * 1.2f);
                Assert.AreEqual(baseAmmo, Size("Ammo"), 0.2f);
                Assert.AreEqual(baseGoals, Size("Goals"), 0.2f);
                settings.largeText = true;
                race.QaFinishNow(); yield return null; yield return null;
                Size("StarDesc"); Size("StarStatus"); Size("Reward");
                for (int i = 0; i < 3; i++) Assert.IsTrue(Button("ResultAction" + i).IsInteractable());
                flow.EnterMenu(); yield return null;
            }
        }

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
