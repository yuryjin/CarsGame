using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class DriftMarkPlayTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator OnlySlidingWheelsLayTemporaryRubberOnTheRoad()
        {
            float clock = Time.captureDeltaTime;
            GameFlow flow = null;
            RaceEventDefinition ev = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float timeout = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
                yield return null; yield return null;
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>();
                ev = Object.Instantiate(System.Array.Find(flow.events, e => e != null && e.eventId == "e1"));
                ev.aiCountOverride = 0; ev.pickupCount = 0; ev.laps = 3;
                flow.StartRace(ev, flow.tiers[0]);
                while (race.State == RaceState.Countdown) yield return null;
                var car = race.Player.Car;
                var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(input);
                var marks = Object.FindAnyObjectByType<DriftTireMarks>();
                var p = car.transform.position; var rotation = car.transform.rotation;
                int peak = 0;
                for (int lap = 0; lap < 7; lap++)
                {
                    car.Teleport(p, rotation);
                    input.Set(1f, 0f);
                    for (int i = 0; i < 150; i++) yield return null;
                    input.Set(1f, .85f, lap > 0);
                    for (int i = 0; i < 90; i++)
                    {
                        yield return null;
                        if (lap == 0) Assert.AreEqual(0, marks.Count, "Same turn, brake released");
                        peak = Mathf.Max(peak, marks.Count);
                        Assert.LessOrEqual(marks.Count, DriftTireMarks.Limit);
                    }
                    if (lap > 0) Assert.Greater(peak, 0);
                    var trail = marks.transform.Find("DriftTireMarks");
                    if (trail != null)
                    {
                        Assert.AreEqual(1, trail.GetComponents<MeshFilter>().Length);
                        var m = trail.GetComponent<MeshFilter>().sharedMesh;
                        Assert.AreEqual(DriftTireMarks.Limit * 4, m.vertexCount);
                        foreach (var v in m.vertices)
                            if (v != Vector3.zero)
                                Assert.IsTrue(Physics.Raycast(trail.TransformPoint(v) + Vector3.up * .035f, Vector3.down, .15f,
                                    1 << LayerMask.NameToLayer("Track")), "Every live tire edge sits on the physical road");
                    }
                    input.Set(0f, 0f, true);
                    for (int i = 0; i < 8; i++) yield return null;
                }
                input.Set(0f, 0f, true);
                for (int i = 0; i < 280; i++) yield return null;
                Assert.AreEqual(0, marks.Count);
                var owned = marks.transform.Find("DriftTireMarks").GetComponent<MeshFilter>().sharedMesh;
                car.Teleport(p, rotation);
                Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
                float deadline = Time.realtimeSinceStartup + 180f;
                while (race.Player.Progress.LapsCompleted < 2 && Time.realtimeSinceStartup < deadline)
                {
                    Assert.LessOrEqual(marks.Count, DriftTireMarks.Limit);
                    yield return null;
                }
                Assert.AreEqual(2, race.Player.Progress.LapsCompleted, "Two complete laps with the same bounded mesh");
                Assert.AreSame(owned, marks.transform.Find("DriftTireMarks").GetComponent<MeshFilter>().sharedMesh);
                flow.EnterMenu(); yield return null;
                Assert.IsTrue(marks == null);
                Debug.Log($"[DriftRubber] seven entries, peak segments={peak}/{DriftTireMarks.Limit}, one mesh retained over two full laps, expired and removed");
            }
            finally { if (flow != null) flow.EnterMenu(); if (ev != null) Object.Destroy(ev); Time.captureDeltaTime = clock; }
        }
    }
}
