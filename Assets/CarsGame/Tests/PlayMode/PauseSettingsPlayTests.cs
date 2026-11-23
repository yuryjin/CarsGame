using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class PauseSettingsPlayTests
    {
        private GameFlow flow;
        private string saved;
        private float capture;
        private Keyboard keyboard;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;

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
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            PauseController.ResetAll(); Time.captureDeltaTime = capture;
        }

        private static Button Button(string name) => System.Array.Find(Object.FindObjectsByType<Button>(), b => b.name == name);

        [UnityTest, Timeout(180000)]
        public IEnumerator ReboundKeySurvivesRestartAndMatchesDrivingInstructions()
        {
            IEnumerator Frames(int count = 3) { while (count-- > 0) yield return null; }
            IEnumerator Boot()
            {
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float deadline = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.IsTrue(PlatformGlue.IsReady);
                yield return Frames();
                flow = Object.FindAnyObjectByType<GameFlow>();
            }
            IEnumerator KeyPress(Key key)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return Frames();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Frames();
            }
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return Boot();
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            keyboard = InputSystem.AddDevice<Keyboard>();
            var menus = flow.GetComponent<MenuScreens>();
            var map = InputSystem.actions.FindActionMap("Driving");
            menus.Show("settings"); yield return null;
            Button("EditDrivingKeys").onClick.Invoke(); yield return null;
            Button("RestoreDrivingKeys").onClick.Invoke(); yield return null;
            string original = Loc.Get("lesson.drift.keys");
            string previousBindings = PlatformGlue.Save.Data.settings.drivingKeyBindings;
            int listed = 0;
            foreach (var action in map.actions)
            for (int i = 0; i < action.bindings.Count; i++)
                if (action.bindings[i].path.StartsWith("<Keyboard>/"))
                {
                    Assert.NotNull(Button("Key" + action.name + i));
                    listed++;
                }
            Assert.Greater(listed, 30);
            Button("KeyBrake0").onClick.Invoke(); yield return null;
            yield return KeyPress(Key.R);
            Assert.AreEqual("capture-key", menus.Current);
            StringAssert.Contains(Loc.Get("touch.reset"), GameObject.Find("KeyPrompt").GetComponent<TMPro.TMP_Text>().text);
            yield return KeyPress(Key.Escape);
            Assert.AreEqual("keys", menus.Current);
            Assert.AreEqual(previousBindings, PlatformGlue.Save.Data.settings.drivingKeyBindings);
            Assert.AreEqual(original, Loc.Get("lesson.drift.keys"));
            Button("KeyBrake0").onClick.Invoke(); yield return null;
            yield return KeyPress(Key.K);
            Assert.AreEqual("keys", menus.Current);
            var brake = map.FindAction("Brake");
            Assert.AreEqual("<Keyboard>/k", brake.bindings[0].effectivePath);
            var flush = PlatformGlue.Save.FlushAsync(); while (!flush.IsCompleted) yield return null;
            Assert.IsFalse(flush.IsFaulted);
            map.RemoveAllBindingOverrides();
            typeof(PlatformGlue).GetProperty(nameof(PlatformGlue.Save)).SetValue(null, null);
            yield return Boot();
            menus = flow.GetComponent<MenuScreens>();
            brake = InputSystem.actions.FindActionMap("Driving").FindAction("Brake");
            Assert.AreEqual("<Keyboard>/k", brake.bindings[0].effectivePath);
            string visibleKey = brake.GetBindingDisplayString(0);
            string updated = original.Replace(Loc.Get("keys.default_brake"), visibleKey);
            Assert.AreNotEqual(original, updated);
            menus.Show("practice"); yield return null;
            Assert.AreEqual(updated, GameObject.Find("LessonDrift").transform.Find("Instruction").GetComponent<TMPro.TMP_Text>().text);
            flow.StartPractice(PracticeLesson.Drift);
            var race = flow.GetComponent<RaceManager>();
            while (race.State == RaceState.Countdown) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); yield return Frames(6);
            Assert.IsFalse(race.Player.Car.LastCommand.Brake);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K)); yield return Frames(6);
            Assert.IsTrue(race.Player.Car.LastCommand.Brake);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Frames();
            Button("PauseButton").onClick.Invoke(); yield return null;
            Button("Hint").onClick.Invoke(); yield return null;
            string help = GameObject.Find("Lessons").GetComponent<TMPro.TMP_Text>().text;
            StringAssert.Contains(updated, help);
            StringAssert.Contains(Loc.Get("help.drive.keys"), help);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CompactLayoutMovesTheLiveModelSwitchIntoPause()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            PlatformGlue.Save.Data.settings.compactHud = false;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            flow.StartRace(System.Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            var car = race.Player.GetComponent<CarBody>();
            var model = Button("RaceModelStyle");
            Assert.NotNull(model);
            var parent = model.transform.parent;
            model.onClick.Invoke();
            void CheckModel()
            {
                var mesh = car.transform.Find("Tripo_" + car.Class);
                Assert.AreEqual(!PlatformGlue.ClassicCarModels, mesh != null && mesh.gameObject.activeInHierarchy);
                var original = car.transform.Find("ClassicHull") ?? car.transform.Find("Car_Visuals");
                Assert.AreEqual(PlatformGlue.ClassicCarModels, original.gameObject.activeInHierarchy);
            }
            CheckModel();
            Button("PauseButton").onClick.Invoke(); yield return null;
            Button("PauseSettings").onClick.Invoke(); yield return null;
            Button("CompactHudOn").onClick.Invoke(); yield return null;
            Button("Back").onClick.Invoke(); yield return null;
            Assert.AreSame(model, Button("RaceModelStyle"));
            Assert.AreEqual("PauseCard", model.transform.parent.name);
            Assert.IsTrue(model.IsInteractable());
            model.onClick.Invoke();
            CheckModel();
            Button("Resume").onClick.Invoke(); yield return null;
            Assert.IsNull(Button("RaceModelStyle"));
            CheckModel();
            Button("PauseButton").onClick.Invoke(); yield return null;
            Button("PauseSettings").onClick.Invoke(); yield return null;
            Button("CompactHudOff").onClick.Invoke(); yield return null;
            Button("Back").onClick.Invoke(); yield return null;
            Button("Resume").onClick.Invoke(); yield return null;
            Assert.AreSame(model, Button("RaceModelStyle"));
            Assert.AreSame(parent, model.transform.parent);
            CheckModel();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SettingsBackAndEscapeKeepTheSameRaceAndEveryExternalPause()
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
            var menus = Object.FindAnyObjectByType<MenuScreens>();
            flow.StartRace(System.Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            var car = race.Player.Car;
            string progress = JsonUtility.ToJson(PlatformGlue.Save.Data.progress);
            Button("PauseButton").onClick.Invoke(); yield return null;
            double time = race.RaceTime;
            Vector3 position = car.transform.position;
            Button("PauseSettings").onClick.Invoke(); yield return null;
            Assert.IsTrue(menus.RaceSettingsOpen);
            Assert.AreEqual("settings", menus.Current);
            Assert.AreEqual(FlowState.Racing, flow.State);
            yield return null;
            Assert.IsFalse(Button("Resume").IsInteractable(), "Keyboard/gamepad navigation cannot reach the covered pause controls");
            Assert.IsFalse(Button("Restart").IsInteractable());
            Assert.IsFalse(Button("MainMenu").IsInteractable());
            var music = System.Array.Find(Object.FindObjectsByType<Slider>(), s => s.name == "MusicVolume");
            music.value = 72f;
            Button("TouchLargeOn").onClick.Invoke(); yield return null;
            Button("TouchLeftOn").onClick.Invoke(); yield return null;
            Button("AutoThrottleOn").onClick.Invoke(); yield return null;
            Assert.IsTrue(car.GetComponent<PlayerDriverInput>().autoThrottle);
            Button("AutoThrottleOff").onClick.Invoke(); yield return null;
            Assert.IsFalse(car.GetComponent<PlayerDriverInput>().autoThrottle);
            bool shake = PlatformGlue.Save.Data.settings.cameraShake;
            Button("DynamicCameraOff").onClick.Invoke(); yield return null;
            Assert.IsFalse(PlatformGlue.Save.Data.settings.dynamicCamera);
            Assert.AreEqual(shake, PlatformGlue.Save.Data.settings.cameraShake);
            for (int frame = 0; frame < 20; frame++) yield return null;
            Assert.AreEqual(time, race.RaceTime, 0.00001);
            Assert.AreEqual(position, car.transform.position);
            Assert.AreEqual(progress, JsonUtility.ToJson(PlatformGlue.Save.Data.progress));

            Button("Back").onClick.Invoke(); yield return null;
            Assert.IsFalse(menus.RaceSettingsOpen);
            Assert.AreEqual("", menus.Current);
            Assert.IsTrue(PauseController.IsPaused);
            Assert.NotNull(Button("Resume"));
            Assert.IsTrue(Button("Resume").IsInteractable(), "Closing settings restores the pause controls");
            Button("PauseSettings").onClick.Invoke(); yield return null;
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            for (int frame = 0; frame < 4; frame++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.IsFalse(menus.RaceSettingsOpen, "Escape closes the settings layer");
            Assert.IsTrue(PauseController.IsPaused, "The same Escape cannot also resume the underlying race");
            Assert.AreEqual(time, race.RaceTime, 0.00001);

            Button("PauseSettings").onClick.Invoke(); yield return null;
            PauseController.Set(PauseReason.Hidden, true);
            Button("Back").onClick.Invoke(); yield return null;
            Button("Resume").onClick.Invoke(); yield return null;
            Assert.IsTrue(PauseController.IsPaused, "Back and Resume cannot clear a host visibility pause");
            PauseController.Set(PauseReason.Hidden, false);
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.IsFalse(PauseController.IsPaused);
            Assert.AreSame(car, race.Player.Car, "Settings must not respawn or restart the field");
            Assert.Greater(race.RaceTime, time);
            Assert.AreEqual(0.72f, PlatformGlue.Save.Data.settings.music, 0.0001f);
            Assert.IsTrue(PlatformGlue.Save.Data.settings.touchLargeButtons);
            Assert.IsTrue(PlatformGlue.Save.Data.settings.touchLeftHanded);
            for (int frame = 0; frame < 60; frame++) yield return null;
            var camera = Camera.main;
            Assert.AreEqual(camera.GetComponent<CarCameraFollow>().steadyFov, camera.fieldOfView, .05f,
                "The changed view takes effect in the same race after resume");
        }
    }
}
