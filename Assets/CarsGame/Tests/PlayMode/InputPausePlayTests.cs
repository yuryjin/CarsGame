using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class InputPausePlayTests
    {
        private GameObject stage;
        private Keyboard keyboard;
        private Mouse mouse;
        private PlayerDriverInput input;
        private InputSettings.BackgroundBehavior background;
        private InputSettings.EditorInputBehaviorInPlayMode editorInput;

        [SetUp]
        public void Setup()
        {
            PauseController.ResetAll();
            background = InputSystem.settings.backgroundBehavior;
            editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            stage = new GameObject("InputPauseStage");
            stage.AddComponent<CarCannon>();
            stage.AddComponent<CarWeapons>();
            input = stage.AddComponent<PlayerDriverInput>();
        }

        [TearDown]
        public void Cleanup()
        {
            if (stage != null) Object.DestroyImmediate(stage);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            PauseController.ResetAll();
        }

        [UnityTest]
        public IEnumerator ResetPressedWhilePausedDoesNotRecoverTheCarAfterResume()
        {
            yield return null;
            PauseController.Set(PauseReason.User, true);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
            PauseController.Set(PauseReason.User, false);
            yield return null;
            Assert.IsFalse(input.Sample(1).ResetEdge, "R used in a pause menu must not queue a driving recovery.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
            yield return null;
            Assert.IsTrue(input.Sample(2).ResetEdge, "A new deliberate press after resume must still work.");
            Assert.IsFalse(input.Sample(3).ResetEdge, "Each reset edge is consumed once.");
        }

        [UnityTest]
        public IEnumerator PausingDiscardsAnUnconsumedResetAndReturnsNeutralDrivingInput()
        {
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R, Key.W, Key.LeftCtrl));
            yield return null; yield return null;
            Assert.Greater(input.RawThrottle, 0f, "Control: the real action map received the accelerator.");
            PauseController.Set(PauseReason.User, true);
            var paused = input.Sample(1);
            Assert.IsFalse(paused.ResetEdge, "An edge buffered before pause must not survive the pause boundary.");
            Assert.AreEqual(0f, paused.Throttle);
            Assert.IsFalse(paused.Fire);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            PauseController.Set(PauseReason.User, false);
            yield return null;
            Assert.IsFalse(input.Sample(2).ResetEdge);
        }
    }
}
