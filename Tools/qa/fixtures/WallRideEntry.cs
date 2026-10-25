// Copy only into an isolated PlayMode assembly. Exercises natural entry from the ordinary floor.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class WallRideEntry
    {
        private GameFlow flow;
        private RaceEventDefinition practice;
        private float capture;
        [Serializable] private sealed class Case { public string evt, body; public int attachTick; public float gravityDot; }
        [Serializable] private sealed class Report { public List<Case> cases = new List<Case>(); }
        private readonly Report report = new Report();
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; PauseController.ResetAll(); }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (practice != null) Object.DestroyImmediate(practice);
            Time.captureDeltaTime = capture; PauseController.ResetAll();
            string output = Environment.GetEnvironmentVariable("CG_WALL_ENTRY_REPORT");
            if (!string.IsNullOrEmpty(output)) File.WriteAllText(output, JsonUtility.ToJson(report, true));
        }
        [UnityTest, Timeout(600000)]
        public IEnumerator CarsCanSteerFromTheOrdinaryFloorOntoTunnelWalls()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            foreach (string evt in new[] { "e19", "e20" })
            foreach (string body in new[] { "classic", "bulwark" })
            {
                practice = Object.Instantiate(Array.Find(flow.events, e => e.eventId == evt));
                practice.aiCountOverride = 0; practice.pickupCount = 0;
                race.spawner.playerBodyId = body;
                flow.StartRace(practice, flow.tiers[0]);
                while (race.State == RaceState.Countdown) yield return null;
                var car = race.Player.Car;
                Assert.AreEqual(body, car.GetComponent<CarBody>().BodyId);
                race.Player.Health.SetInvulnerable(30f);
                var input = car.gameObject.AddComponent<ScriptedDriverInput>(); car.SetInputSource(input);
                var star = Object.FindAnyObjectByType<StarRingTrack>();
                var waste = Object.FindAnyObjectByType<WastelandTrack>();
                float start = star != null ? star.Layout.TunnelStart : waste.Layout.TunnelStart;
                float end = star != null ? star.Layout.TunnelEnd : waste.Layout.TunnelEnd;
                float d = (start + end) * .5f - 20f;
                var path = TrackPath.Instance;
                car.Teleport(path.Position(d) + Vector3.up * .04f, Quaternion.LookRotation(path.Tangent(d), Vector3.up));
                car.Body.linearVelocity = path.Tangent(d) * 25f;
                Physics.SyncTransforms();
                Assert.IsTrue(Physics.Raycast(car.transform.position + Vector3.up * .5f, Vector3.down, out var floor, 2f, 1 << LayerMask.NameToLayer("Track")));
                Assert.IsNull(floor.collider.GetComponent<GravitySurface>(), "Begin on the ordinary central floor, not on a pre-positioned wall.");
                input.Set(1f, 1f);
                var measured = new Case { evt = evt, body = body, attachTick = -1, gravityDot = 1f };
                for (int tick = 0; tick < 200; tick++)
                {
                    yield return new WaitForFixedUpdate();
                    float dot = Vector3.Dot(car.GravityUp, Vector3.up);
                    measured.gravityDot = Mathf.Min(measured.gravityDot, dot);
                    if (car.WallRiding && dot < .95f) { measured.attachTick = tick; break; }
                }
                report.cases.Add(measured);
                Debug.Log($"[WallEntry] {evt}/{body} attach={measured.attachTick} gravityDot={measured.gravityDot}");
                Assert.GreaterOrEqual(measured.attachTick, 0, "Steering from the floor must still engage wall riding: " + evt + "/" + body);
                flow.EnterMenu(); Object.Destroy(practice); practice = null;
                yield return null; yield return null;
            }
        }
    }
}
