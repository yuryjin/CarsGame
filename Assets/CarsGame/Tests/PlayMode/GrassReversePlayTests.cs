using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    /// <summary>
    /// A car nosed into a barrier on the verge must be able to back out. The grass drag (3.5 m/s²) used to
    /// outweigh the reverse motor (2.8 m/s²), so reversing on grass crawled at ~0.2 km/h and the AI sat at
    /// the start/finish hairpin exit until its stuck timer respawned it back into the same bend.
    /// </summary>
    public sealed class GrassReversePlayTests
    {
        private float savedCaptureDelta;
        [SetUp] public void Save() { savedCaptureDelta = Time.captureDeltaTime; }
        [TearDown] public void Restore() { Time.captureDeltaTime = savedCaptureDelta; LogAssert.ignoreFailingMessages = false; }

        [UnityTest, Timeout(300000)]
        public IEnumerator CarReversesOffTheGrass()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float wait = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < wait) yield return null;
            yield return null; yield return null;   // GameFlow.Start (EnterMenu) must run before a race starts
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            RaceEventDefinition ev = null;
            foreach (var e in flow.events) if (e != null && e.eventId == "e1") ev = e;
            ev = Object.Instantiate(ev); ev.aiCountOverride = 0; ev.laps = 1; ev.pickupCount = 0;
            flow.StartRace(ev, null);
            var car = race.Player.Car;
            var input = car.gameObject.AddComponent<ScriptedDriverInput>();
            car.SetInputSource(input);
            while (race.State == RaceState.Countdown) yield return null;

            // A flat grass spot beside a straight: walk out from the road until the ground is the Ground layer.
            var path = TrackPath.Instance;
            int groundLayer = LayerMask.NameToLayer("Ground");
            Vector3 spot = default; Quaternion facing = default; bool found = false;
            for (float d = 150f; d < 900f && !found; d += 25f)
                for (float lane = 9f; lane <= 16f && !found; lane += 1f)
                {
                    path.Pose(d, lane, out var p, out var r);
                    if (!Physics.Raycast(p + Vector3.up * 5f, Vector3.down, out var hit, 20f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (hit.collider.gameObject.layer != groundLayer || hit.normal.y < 0.98f) continue;
                    if (Physics.CheckBox(hit.point + Vector3.up * 1.2f, new Vector3(1.5f, 0.8f, 5f), r, ~(1 << groundLayer), QueryTriggerInteraction.Ignore)) continue;
                    spot = hit.point + Vector3.up * 0.5f; facing = r; found = true;
                }
            Assert.IsTrue(found, "no flat, clear grass beside the track");
            car.Teleport(spot, facing);
            input.Set(0f, 0f);
            for (int i = 0; i < 50; i++) yield return null;                 // settle
            Assert.AreEqual(GroundSurface.Ground, car.Surface, "the test car sits on grass");

            Vector3 start = car.transform.position;
            input.Set(-1f, 0f);
            for (int i = 0; i < 100; i++) yield return null;                // 2 s of reverse
            float backed = -Vector3.Dot(car.transform.position - start, car.transform.forward);
            Debug.Log($"[GrassReversePlayTests] reversed {backed:0.00} m in 2 s on {car.Surface}");
            Assert.Greater(backed, 1.5f, "reversing on grass must move the car backwards");
            Time.captureDeltaTime = 0f;
            flow.EnterMenu();
        }
    }
}
