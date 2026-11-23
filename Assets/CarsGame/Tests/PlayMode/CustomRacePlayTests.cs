using System.Collections;
using System.Linq;
using NUnit.Framework;
using RacingGame.Core.Daily;
using RacingGame.Core.Ghost;
using RacingGame.Core.Save;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class CustomRacePlayTests
    {
        private static void Choice(string row, string direction) =>
            GameObject.Find(row).transform.Find(direction).GetComponent<Button>().onClick.Invoke();

        [UnityTest, Timeout(180000)]
        public IEnumerator TheSelectedMountainSideRoadReachesTheCustomRace()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            while (!PlatformGlue.IsReady) yield return null;
            yield return null; yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            try
            {
                var course = flow.events.First(e => e != null && e.mountainFork == MountainFork.Quarry);
                Assert.IsTrue(Loc.TryGet(course.nameKey, out var courseName));
                Object.FindAnyObjectByType<MenuScreens>().Show("events");
                GameObject.Find("ConfigureCustom").GetComponent<Button>().onClick.Invoke();
                string expected = Loc.Get("format.custom.course") + " · " + courseName;
                for (int i = 0; i < flow.events.Length; i++)
                {
                    var caption = GameObject.Find("Course").transform.Find("Value").GetComponent<TMP_Text>().text;
                    if (caption == expected) break;
                    Choice("Course", "Next");
                }
                Assert.AreEqual(expected, GameObject.Find("Course").transform.Find("Value").GetComponent<TMP_Text>().text);
                GameObject.Find("StartCustom").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(flow.CurrentEvent.IsCustom);
                Assert.AreEqual(course.mountainFork, flow.CurrentEvent.mountainFork);
                Assert.AreEqual(course.mountainFork, TrackPath.Instance.Fork.Course);
            }
            finally { flow.EnterMenu(); }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TwoCustomFieldsUseTheirOwnRulesAndKeepTheCareerRecord()
        {
            float capture = Time.captureDeltaTime;
            GameFlow flow = null; string previous = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                while (!PlatformGlue.IsReady) yield return null;
                yield return null; yield return null;
                var save = PlatformGlue.Save; previous = JsonUtility.ToJson(save.Data);
                var career = save.Data.progress.bests.Find(x => x.eventId == "e1");
                if (career == null) { career = new EventBest { eventId = "e1" }; save.Data.progress.bests.Add(career); }
                career.bestTimeMs = 128750; career.stars = 5;
                string record = JsonUtility.ToJson(career);
                var menu = Object.FindAnyObjectByType<MenuScreens>(); flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>(); var debug = Object.FindAnyObjectByType<DebugBus>();
                var first = System.Array.Find(flow.events, e => e.eventId == "e1");
                flow.StartRace(first, flow.tiers[0]);
                while (race.State == RaceState.Countdown) yield return null;
                var recorder = flow.GetComponent<GhostRecorder>();
                float deadline = Time.realtimeSinceStartup + 15f;
                while (recorder.Record.Samples.Count < 12 && Time.realtimeSinceStartup < deadline) yield return null;
                flow.EnterMenu();
                Assert.GreaterOrEqual(recorder.Record.Samples.Count, 12);
                var previousBest = new GhostRecord { TrackHash = recorder.Record.TrackHash, PlayerName = "Career" };
                previousBest.AddPose(Vector3.zero, Quaternion.identity, 0, 0, 0);
                previousBest.AddPose(Vector3.forward, Quaternion.identity, career.bestTimeMs / 1000d, 0, 0);
                save.Data.personalGhosts = new PersonalGhosts();
                Assert.IsTrue(save.Data.personalGhosts.KeepBest("e1", first.laps, previousBest.ToBase64(), 1));
                string archive = JsonUtility.ToJson(save.Data.personalGhosts);
                menu.Show("events"); GameObject.Find("ConfigureCustom").GetComponent<Button>().onClick.Invoke();
                Choice("Rivals", "Previous"); Choice("Rivals", "Previous");
                Choice("Rule", "Previous");
                Assert.AreEqual(Loc.Get("format.custom.solo_bomb"), GameObject.Find("Note").transform.Find("Text").GetComponent<TMP_Text>().text);
                Assert.IsFalse(GameObject.Find("StartCustom").GetComponent<Button>().interactable);
                Choice("Rule", "Next"); Choice("Rule", "Next");
                string course = GameObject.Find("Course").transform.Find("Value").GetComponent<TMP_Text>().text;
                for (int run = 1; run <= 2; run++)
                {
                    int rivals = run - 1;
                    Assert.AreEqual(Loc.Format("events.laps", "n", run), GameObject.Find("Laps").transform.Find("Value").GetComponent<TMP_Text>().text);
                    Assert.AreEqual(Loc.Format("events.rivals", "n", rivals), GameObject.Find("Rivals").transform.Find("Value").GetComponent<TMP_Text>().text);
                    Assert.IsTrue(GameObject.Find("StartCustom").GetComponent<Button>().interactable);
                    GameObject.Find("StartCustom").GetComponent<Button>().onClick.Invoke();
                    while (race.State == RaceState.Countdown) yield return null;
                    Assert.IsTrue(flow.CurrentEvent.IsCustom); Assert.AreEqual(run, race.laps);
                    Assert.AreEqual(rivals + 1, race.Participants.Count);
                    if (run == 1)
                    {
                        Assert.AreEqual(RaceTwist.MinesOnly, flow.CurrentEvent.twist); Assert.IsTrue(race.Player.Car.Cannon.locked);
                        Assert.AreEqual(EventRules.MinesOnlyAmmo, race.Player.Weapons.secondaryAmmoMax);
                    }
                    else
                    {
                        Assert.AreEqual(RaceTwist.OneLife, flow.CurrentEvent.twist);
                        Assert.IsTrue(race.Participants.All(p => !p.Health.respawnOnDeath));
                        var rival = race.Participants.First(p => !p.isPlayer); rival.GetComponent<AiGunner>().enabled = false;
                        var path = race.Player.Progress.Path;
                        path.Pose(path.startLineDistance - 75f, path.halfWidth - 1.8f, out var behind, out var facing);
                        rival.Car.Teleport(behind + Vector3.up * .45f, facing);
                        rival.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                    }
                    debug.Execute("autopilot", "on");
                    float until = Time.realtimeSinceStartup + 160f;
                    while (flow.State == FlowState.Racing && !race.CanShowResults && Time.realtimeSinceStartup < until) yield return null;
                    if (race.CanShowResults) race.ShowResultsAfterFinish();
                    Debug.Log($"[CustomResult] run={run} state={flow.State} laps={race.Player.Progress.LapsCompleted} deaths={race.Player.Deaths} position={race.PositionOf(race.Player)} speed={race.Player.Car.SpeedKmh:F1}");
                    Assert.AreEqual(FlowState.Results, flow.State); Assert.IsTrue(race.Results.Player.Finished);
                    Assert.AreEqual(run, race.Results.Player.Laps);
                    Assert.AreEqual(record, JsonUtility.ToJson(save.Data.progress.bests.Find(x => x.eventId == "e1")));
                    Assert.AreEqual(archive, JsonUtility.ToJson(save.Data.personalGhosts));
                    Assert.IsFalse(save.Data.progress.bests.Exists(x => x.eventId == "custom"));
                    Debug.Log($"[CustomRace] course={course}, laps={run}, rivals={rivals}, twist={flow.CurrentEvent.twist}, full laps={race.Results.Player.Laps}, career={career.bestTimeMs}");
                    if (run == 1)
                    {
                        GameObject.Find("EditCustom").GetComponent<Button>().onClick.Invoke(); yield return null;
                        Choice("Laps","Next"); Choice("Rivals","Next"); Choice("Rule","Next");
                    }
                }
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
