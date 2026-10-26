using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class MissionResultsPlayTests
    {
        private GameFlow flow;
        private string before;
        private float capture;

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (before != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(before, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        // EndMission is exercised directly to cover both result paths without waiting for
        // a whole boss encounter. MissionPlayTests separately covers the real encounter.
        [UnityTest, Timeout(180000)]
        public IEnumerator TeamResultsUseCombatRankingAndOneSharedTimeForBothOutcomes()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            before = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var ev = Resources.Load<RaceEventDefinition>("Events/Event_m1");
            foreach (bool victory in new[] { false, true })
            {
                flow.StartRace(ev, flow.tiers[0]);
                while (race.State == RaceState.Countdown) yield return null;
                Assert.AreEqual(6, race.Participants.Count);
                var first = race.Participants[4];
                var second = race.Participants[3];
                first.KillTally.Record(101); first.KillTally.Record(102);
                second.KillTally.Record(101); second.KillTally.Record(102);
                second.Deaths = 1;
                race.EndMission(victory);
                yield return null;
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.AreEqual(0, race.Results.LapsRequired, "Missions do not require circuit laps");
                Assert.AreEqual(first.displayName, race.Results.Entries[0].Name);
                Assert.AreEqual(second.displayName, race.Results.Entries[1].Name);
                Assert.AreEqual(Loc.Get(victory ? "mission.victory" : "mission.defeat"),
                    GameObject.Find("Summary").transform.Find("Detail").GetComponent<TMP_Text>().text);
                foreach (var entry in race.Results.Entries)
                {
                    Assert.AreEqual(victory, entry.Finished);
                    Assert.AreEqual(race.Results.RaceDuration, entry.TotalTime, 0.000001,
                        "Every teammate shares the actual mission duration; placement cannot add fake time");
                    var row = GameObject.Find("Result" + entry.Position).transform;
                    Assert.AreEqual(Loc.Format("results.kills", "k", entry.Kills, "d", entry.Deaths),
                        row.Find("Detail").GetComponent<TMP_Text>().text);
                    if (!victory) Assert.AreEqual(Loc.Get("results.defeat"), row.Find("Time").GetComponent<TMP_Text>().text);
                }
                flow.EnterMenu();
                yield return null;
            }
        }
    }
}
