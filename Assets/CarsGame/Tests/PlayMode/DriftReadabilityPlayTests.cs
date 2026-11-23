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
        public IEnumerator SlipstreamMeterChargesBehindAMovingCarFadesOnPassingAndStaysOffForSoloNitro()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            Assert.IsTrue(flow.StartTestDrive("classic"));
            while (race.State == RaceState.Countdown) yield return null;

            var path = TrackPath.Instance;
            float straight = 0f, bestSpeed = -1f;
            for (float d = 0f; d < path.Length; d += 20f)
            {
                float speed = path.Profile.MinSpeedAhead(d, 75f);
                if (speed > bestSpeed) { bestSpeed = speed; straight = d; }
            }
            var player = race.Player.Car;
            var local = Place(player, straight);
            var rival = race.Participants[0].isPlayer ? race.Participants[1].Car : race.Participants[0].Car;
            var ahead = Place(rival, straight + 13f);
            var label = System.Array.Find(Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include),
                t => t.name == "Nitro" && t.transform.parent.name == "Speedometer");
            Assert.NotNull(label);
            for (int i = 0; i < 140 && player.DraftCharge01 < .6f; i++) yield return null;
            yield return null;
            Assert.GreaterOrEqual(player.DraftCharge01, .6f, "Two cars driving in the same lane must fill the meter");
            Assert.IsTrue(player.IsBoosting);
            Assert.IsTrue(label.gameObject.activeInHierarchy);
            Assert.AreEqual(Loc.Get("hud.slipstream"), label.text);
            float peak = player.DraftCharge01;
            local.Set(1f, .55f); ahead.Set(0f, -.55f, true);
            bool sawDecay = false;
            for (int i = 0; i < 180 && player.DraftCharge01 > 0f; i++)
            {
                yield return null;
                if (player.DraftCharge01 > .05f && player.DraftCharge01 < peak - .1f) sawDecay = true;
            }
            Assert.IsTrue(sawDecay, "Leaving the wake bleeds off its stored charge");
            Assert.AreEqual(0f, player.DraftCharge01);
            flow.EnterMenu();
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
            while (race.State == RaceState.Countdown) yield return null;
            Assert.AreEqual(1, race.Participants.Count);
            player = race.Player.Car;
            player.ApplyBoost(1f);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(player.IsBoosting);
            Assert.AreEqual(0f, player.DraftCharge01);
            Assert.IsTrue(label.gameObject.activeInHierarchy);
            Assert.AreEqual(Loc.Get("hud.nitro"), label.text);
            Debug.Log($"[Slipstream] peak={peak:F2}, pass bleeds charge to zero, solo boost={player.IsBoosting}");

            ScriptedDriverInput Place(CarController car, float distance)
            {
                path.Pose(distance, 0f, out var position, out var rotation);
                car.Teleport(position + Vector3.up * .5f, rotation);
                car.Body.linearVelocity = car.transform.forward * 14f;
                var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(input);
                input.Set(.55f, 0f);
                return input;
            }
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
