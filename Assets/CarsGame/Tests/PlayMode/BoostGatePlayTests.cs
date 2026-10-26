using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class BoostGatePlayTests
    {
        private GameFlow flow;
        private GameObject aperture;
        private float capture;
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (aperture != null) Object.DestroyImmediate(aperture);
            PauseController.ResetAll(); Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FlatPadsRewardThePaintedWidthOnTheFloorAndCeiling()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            foreach (bool ceiling in new[] { false, true }) foreach (float lane in new[] { 0f, 1.7f, -1.7f, 2.5f, -2.5f })
            {
                Assert.IsTrue(flow.StartPractice(PracticeLesson.WallRide));
                while (race.State == RaceState.Countdown) yield return null;
                var map = Object.FindAnyObjectByType<StarRingTrack>();
                var pad = map.transform.Find(ceiling ? "CeilingBoostPad" : "BoostPad");
                var car = race.Player.Car;
                var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(input); input.Set(1f, 0f);
                // Prepare an approach along the surface. Natural ceiling entry has its own practice test.
                car.Teleport(pad.position - pad.up * 1.3f + pad.right * lane - pad.forward * 14f, pad.rotation);
                car.Body.linearVelocity = pad.forward * 22f;
                Physics.SyncTransforms();
                bool boosted = false, crossed = false;
                float closestZ = float.PositiveInfinity;
                Vector3 crossing = Vector3.zero;
                for (int tick = 0; tick < 100; tick++)
                {
                    yield return new WaitForFixedUpdate();
                    Vector3 local = pad.InverseTransformPoint(car.Body.worldCenterOfMass);
                    if (Mathf.Abs(local.z) < closestZ) { closestZ = Mathf.Abs(local.z); crossing = local; }
                    boosted |= car.IsBoosting;
                    if (local.z > 5f) { crossed = true; break; }
                }
                Debug.Log($"[PadWidth] ceiling={ceiling} lane={lane} crossing={crossing} boosted={boosted}");
                Assert.IsTrue(crossed, "The physical approach crosses the pad");
                Assert.AreEqual(lane, crossing.x, 0.25f, "The approach keeps its intended lane");
                Assert.AreEqual(Mathf.Abs(lane) < StarRingTrack.PadHalfWidth, boosted,
                    $"The visible strip defines the useful width, ceiling={ceiling}, lane={lane}");
                flow.EnterMenu(); yield return null; yield return null;
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator MovingIntoTheApertureAfterTheInitialOverlapStillAwardsOneBoost()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
            while (race.State == RaceState.Countdown) yield return null;
            var participant = race.Player;
            var car = participant.Car;
            // Isolate the actual trigger contacts from road recovery and car steering forces.
            participant.enabled = false; participant.Progress.enabled = false; car.enabled = false;
            car.inputEnabled = true;
            car.Teleport(new Vector3(3f, 1000f, -8f), Quaternion.identity);
            car.Body.useGravity = false;
            aperture = new GameObject("OffsetBoostAperture");
            aperture.transform.position = car.Body.worldCenterOfMass + new Vector3(-3f, 0f, 8f);
            var trigger = aperture.AddComponent<BoxCollider>();
            trigger.isTrigger = true; trigger.size = new Vector3(10f, 6f, 12f);
            var gate = aperture.AddComponent<BoostGate>();
            gate.radius = 2f; gate.ceiling = true;
            Physics.SyncTransforms();
            car.Body.linearVelocity = Vector3.forward * 4f;
            for (int tick = 0; tick < 60; tick++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(0, participant.CeilingBoosts, "Initial overlap is deliberately outside the circular aperture");
            Vector3 local = aperture.transform.InverseTransformPoint(car.Body.worldCenterOfMass);
            Assert.AreEqual(0f, local.y, 0.01f, "The fixture stays at the aperture height");
            Assert.AreEqual(3f, local.x, 0.01f);
            Assert.That(local.z, Is.InRange(-4f, 0f), "All of the compound car is already inside the trigger");
            car.Body.linearVelocity = new Vector3(-6f, 0f, 4f);
            for (int tick = 0; tick < 25 && participant.CeilingBoosts == 0; tick++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(1, participant.CeilingBoosts, "Centering during an existing overlap must activate the boost");
            Assert.Greater(car.Body.linearVelocity.z, 4f, "The normal physical boost impulse is applied");
            car.Body.linearVelocity = Vector3.zero;
            for (int tick = 0; tick < SimulationDriver.SecondsToTicks(BoostGate.BoostSeconds) + 10; tick++)
                yield return new WaitForFixedUpdate();
            car.Body.linearVelocity = Vector3.forward * 4f;
            for (int tick = 0; tick < 5; tick++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(1, participant.CeilingBoosts, "Remaining in one passage cannot farm boosts after the cooldown");

            car.Body.position = aperture.transform.position + Vector3.back * 12f;
            car.Body.linearVelocity = Vector3.zero;
            for (int tick = 0; tick < 5; tick++) yield return new WaitForFixedUpdate();
            car.Body.position = aperture.transform.position;
            car.Body.linearVelocity = Vector3.forward * 4f;
            for (int tick = 0; tick < 5; tick++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(2, participant.CeilingBoosts, "Leaving and returning starts a new passage");
        }
    }
}
