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
