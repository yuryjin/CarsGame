using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class GamepadNavigationPlayTests
    {
        private Gamepad pad;
        private GameFlow flow;
        private string saved;
        private float capture;
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
            if (pad != null) InputSystem.RemoveDevice(pad);
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            PauseController.ResetAll(); Time.captureDeltaTime = capture;
        }

        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            for (int i = 0; i < 3; i++) yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            for (int i = 0; i < 3; i++) yield return null;
        }

        // Use the same public navigation graph a person explores, then send actual device events.
        // Never call Button.onClick or assign focus to the destination.
        private IEnumerator Navigate(string name)
        {
            var start = EventSystem.current.currentSelectedGameObject?.GetComponent<Selectable>();
            Assert.NotNull(start, "The screen must establish an initial gamepad focus");
            var queue = new Queue<Selectable>(); queue.Enqueue(start);
            var routes = new Dictionary<Selectable, List<GamepadButton>> { [start] = new List<GamepadButton>() };
            Selectable target = null;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (current.name == name) { target = current; break; }
                var neighbours = new[] { current.FindSelectableOnUp(), current.FindSelectableOnDown(), current.FindSelectableOnLeft(), current.FindSelectableOnRight() };
                var buttons = new[] { GamepadButton.DpadUp, GamepadButton.DpadDown, GamepadButton.DpadLeft, GamepadButton.DpadRight };
                for (int i = 0; i < neighbours.Length; i++)
                {
                    var next = neighbours[i];
                    if (next == null || !next.IsInteractable() || routes.ContainsKey(next)) continue;
                    routes[next] = new List<GamepadButton>(routes[current]) { buttons[i] };
                    queue.Enqueue(next);
                }
            }
            Assert.NotNull(target, $"{name} must be reachable from {start.name}");
            foreach (var direction in routes[target]) yield return Press(direction);
            Assert.AreEqual(name, EventSystem.current.currentSelectedGameObject?.name, "Real device input follows navigation");
            var scroll = target.GetComponentInParent<ScrollRect>();
            if (scroll != null)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, target.transform);
                Assert.GreaterOrEqual(bounds.min.y, scroll.viewport.rect.yMin - 2f, "Focused control scrolls into view");
                Assert.LessOrEqual(bounds.max.y, scroll.viewport.rect.yMax + 2f, "Focused control stays below the header");
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator GamepadCanNavigateSettingsPracticeAndNestedPauseWithoutMouseInput()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var menus = Object.FindAnyObjectByType<MenuScreens>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            pad = InputSystem.AddDevice<Gamepad>();
            yield return null;
            yield return Navigate("settings"); yield return Press(GamepadButton.South);
            Assert.AreEqual("settings", menus.Current);
            yield return Navigate("MusicVolume");
            var slider = EventSystem.current.currentSelectedGameObject.GetComponent<Slider>();
            float volume = slider.value;
            yield return Press(volume > 0f ? GamepadButton.DpadLeft : GamepadButton.DpadRight);
            Assert.AreEqual(volume + (volume > 0f ? -1f : 1f), slider.value, "D-pad changes volume by one percent");
            yield return Press(GamepadButton.East);
            Assert.AreEqual("main", menus.Current);
            yield return Navigate("Practice"); yield return Press(GamepadButton.South);
            Assert.AreEqual("practice", menus.Current);
            yield return Navigate("PracticeDrift"); yield return Press(GamepadButton.South);
            Assert.IsTrue(flow.IsPractice);
            while (race.State == RaceState.Countdown) yield return null;
            yield return Press(GamepadButton.Start);
            Assert.IsTrue(PauseController.IsPaused);
            yield return Navigate("PauseSettings"); yield return Press(GamepadButton.South);
            Assert.IsTrue(menus.RaceSettingsOpen);
            yield return Navigate("MusicVolume");
            yield return Press(GamepadButton.East);
            Assert.IsFalse(menus.RaceSettingsOpen);
            Assert.IsTrue(PauseController.IsPaused);
            Assert.AreEqual("Resume", EventSystem.current.currentSelectedGameObject?.name);
            yield return Press(GamepadButton.South);
            Assert.IsFalse(PauseController.IsPaused);
            yield return Press(GamepadButton.Start);
            yield return Navigate("MainMenu"); yield return Press(GamepadButton.South);
            Assert.AreEqual(FlowState.Menu, flow.State);
            Assert.AreEqual("practice", menus.Current);
        }
    }
}
