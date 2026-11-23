using System.Collections;
using System.Linq;
using NUnit.Framework;
using RacingGame.Core.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class RelayPlayTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator AFinishWithoutAnExchangeIsClassifiedUnfinished()
        {
            float capture = Time.captureDeltaTime;
            GameFlow flow = null; string previous = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                while (!PlatformGlue.IsReady) yield return null;
                yield return null; yield return null;
                previous = JsonUtility.ToJson(PlatformGlue.Save.Data);
                flow = Object.FindAnyObjectByType<GameFlow>(); var race = Object.FindAnyObjectByType<RaceManager>();
                var ev = System.Array.Find(flow.events, e => e.eventId == "relay");
                flow.StartRace(ev, flow.tiers[0]);
                while (race.State == RaceState.Countdown) yield return null;
                var player = race.Player; var path = player.Progress.Path;
                path.Pose(path.startLineDistance - .2f, 0f, out var point, out var rotation);
                player.Car.Teleport(point + Vector3.up * .45f, rotation);
                player.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                for (int i = 0; i < 4; i++) yield return null;
                PhotoFinishPlayTests.ArmLap(player.Progress);
                player.Car.Body.constraints = RigidbodyConstraints.FreezeRotation;
                player.Car.Body.linearVelocity = rotation * Vector3.forward * 20f;
                for (int i = 0; i < 70 && flow.State == FlowState.Racing; i++) yield return null;
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.IsFalse(race.Results.Player.Finished);
                Assert.AreEqual(Loc.Get("format.relay.second") + "  0/1", GameObject.Find("Summary").transform.Find("Detail").GetComponent<TMP_Text>().text);
            }
            finally
            {
                if (flow != null) flow.EnterMenu();
                if (previous != null) { JsonUtility.FromJsonOverwrite(previous, PlatformGlue.Save.Data); PlatformGlue.Save.MarkDirty(); }
                Time.captureDeltaTime = capture;
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheGreenGateExchangesTheSelectedBodiesExactlyOnce()
        {
            float capture = Time.captureDeltaTime;
            string previous = null; GameFlow flow = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                while (!PlatformGlue.IsReady) yield return null;
                yield return null; yield return null;
                var save = PlatformGlue.Save; previous = JsonUtility.ToJson(save.Data);
                var progress = save.Data.progress; progress.body = CarBodies.DefaultId;
                progress.cosmetics.RemoveAll(k => k.StartsWith("body:"));
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>(); var debug = Object.FindAnyObjectByType<DebugBus>();
                var menu = Object.FindAnyObjectByType<MenuScreens>(); menu.Show("events");
                GameObject.Find("Start_relay").GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(2, RelayRace.Available().Count);
                GameObject.Find("RelayFirst").transform.Find("Next").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(GameObject.Find("RelayFirst").transform.Find("Body").GetComponent<TMP_Text>().text.Contains(Loc.Get("body.dune")));
                GameObject.Find("StartRelay").GetComponent<Button>().onClick.Invoke();
                while (race.State == RaceState.Countdown) yield return null;
                var runner = race.Player; var car = runner.Car; var body = car.GetComponent<CarBody>();
                var relay = Object.FindAnyObjectByType<RelayRace>();
                int outcomes = 0; race.OnRaceFinished += _ => outcomes++;
                Assert.AreEqual("dune", body.BodyId);
                var clock = race.RaceStartTime; var counter = runner.Progress.Counter; var path = runner.Progress.Path;
                float firstMass = car.Body.mass;
                debug.Execute("autopilot", "on");
                int gateBefore = 0; double before = 0d; float until = Time.realtimeSinceStartup + 175f;
                while (!relay.Exchanged && race.State == RaceState.Racing && Time.realtimeSinceStartup < until)
                {
                    gateBefore = runner.Progress.GatesPassedThisLap; before = race.RaceTime; yield return null;
                }
                Assert.IsTrue(relay.Exchanged);
                Assert.AreEqual("classic", body.BodyId); Assert.AreSame(runner, race.Player);
                Assert.AreEqual(clock, race.RaceStartTime); Assert.GreaterOrEqual(race.RaceTime, before);
                Assert.AreSame(counter, runner.Progress.Counter);
                Assert.That(runner.Progress.GatesPassedThisLap, Is.InRange(gateBefore, gateBefore + 1));
                Assert.That(car.Body.mass, Is.EqualTo(firstMass / CarBodies.Get("dune").Stats.Mass).Within(.2f));
                Assert.AreSame(body, car.GetComponent<CarBody>()); Assert.AreEqual(5, car.GetComponentsInChildren<Collider>().Count(c => !c.isTrigger));
                path.Pose(relay.GateDistance - 9f, 0f, out var back, out var rot);
                car.Teleport(back + Vector3.up * .4f, rot); car.Body.linearVelocity = rot * Vector3.forward * 13f;
                for (int i = 0; i < 100; i++) yield return null;
                Assert.Greater(path.SignedDelta(relay.GateDistance, runner.Progress.Distance), 4f);
                Assert.AreEqual("classic", body.BodyId);
                while (flow.State == FlowState.Racing && Time.realtimeSinceStartup < until) yield return null;
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.IsTrue(race.Results.Player.Finished); Assert.AreEqual(1, race.Results.Player.Laps); Assert.AreEqual(1, outcomes);
                Assert.AreEqual(CarBodies.DefaultId, progress.body); Assert.IsFalse(CarBodies.Owns(progress.cosmetics, RelayRace.Loaner));
                Debug.Log($"[Relay] dune → classic, gate {gateBefore}/{counter.GateCount}, clock {before:F3}s, repeated crossing once, finish {race.Results.Player.TotalTime:F3}s, saved equipment {progress.body}");
                flow.EnterMenu(); menu.Show("events"); GameObject.Find("Start_e1").GetComponent<Button>().onClick.Invoke();
                Assert.AreEqual(progress.body, race.Player.GetComponent<CarBody>().BodyId);
            }
            finally
            {
                if (flow != null) flow.EnterMenu();
                if (previous != null) { JsonUtility.FromJsonOverwrite(previous, PlatformGlue.Save.Data); PlatformGlue.Save.MarkDirty(); }
                Time.captureDeltaTime = capture;
            }
        }
    }
}
