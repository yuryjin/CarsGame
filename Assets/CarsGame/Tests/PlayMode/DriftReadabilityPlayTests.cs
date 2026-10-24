using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class DriftReadabilityPlayTests
    {
        private float capture;
        private GameFlow flow;
        private RaceEventDefinition practice;

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; PauseController.ResetAll(); }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (practice != null) Object.DestroyImmediate(practice);
            Time.captureDeltaTime = capture;
            PauseController.ResetAll();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ActualDriftKeepsItsChargeAndReleaseInstructionsReadableAtPhoneScale()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 0; practice.pickupCount = 0; practice.laps = 1;
            flow.StartRace(practice, flow.tiers[0]);
            var car = race.Player.Car;
            var input = car.gameObject.AddComponent<ScriptedDriverInput>();
            car.SetInputSource(input);
            var canvas = System.Array.Find(Object.FindObjectsByType<Canvas>(), c => c.name == "SpeedFx");
            Assert.NotNull(canvas);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f / 3f; // 640×360 relative to the authored 1920×1080 canvas.
            Canvas.ForceUpdateCanvases();
            while (race.State == RaceState.Countdown) yield return null;
            input.Set(1f, 0f);
            for (int i = 0; i < 150; i++) yield return null;
            input.Set(1f, 1f, true);
            for (int i = 0; i < 80; i++) yield return null;
            Assert.IsTrue(car.IsDrifting, "Readability is checked during an actual physics drift.");
            Assert.GreaterOrEqual(car.DriftStage, 1);
            var label = System.Array.Find(canvas.GetComponentsInChildren<TMP_Text>(), t => t.name == "Action");
            Assert.NotNull(label);
            label.ForceMeshUpdate();
            float pixels = label.fontSize * label.transform.lossyScale.y;
            Debug.Log($"[DriftReadability] charge font={pixels:0.00}px text={label.text}");
            Assert.GreaterOrEqual(pixels, 13.9f, "The instruction must remain readable at phone scale.");
            Assert.IsFalse(label.isTextTruncated);
            Assert.LessOrEqual(label.textInfo.lineCount, 2);
            int stage = car.DriftStage;
            input.Set(1f, 0f);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.Greater(race.Player.DriftBoosts, 0);
            Assert.IsTrue(label.gameObject.activeInHierarchy);
            Assert.AreEqual(Loc.Format("drift.released", "n", stage), label.text);
            Assert.GreaterOrEqual(label.fontSize * label.transform.lossyScale.y, 13.9f);
        }
    }
}
