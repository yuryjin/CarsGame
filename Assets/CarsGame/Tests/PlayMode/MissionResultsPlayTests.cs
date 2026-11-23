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
        [UnityTest, Timeout(240000)]
        public IEnumerator PersonalBossContributionFreezesAtVictoryAndDefeat()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            yield return null; yield return null;
            before = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            foreach (bool win in new[] { true, false })
            {
                flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_m1"), flow.tiers[0]);
                while (race.State == RaceState.Countdown) yield return null;
                var mission = MissionDirector.Current;
                var boss = mission.Boss;
                var player = race.Player.gameObject;
                var ally = race.Participants[1];
                var other = race.Participants[2];
                // Separate the two threatened teammates and allow the spawn shield to expire.
                while (ally.Health.IsInvulnerable || other.Health.IsInvulnerable) yield return null;
                Vector3 safe = mission.Map.RoutePoint(mission.Map.RouteLength * .65f);
                mission.Map.GroundPose(safe, Vector3.forward, out var pos, out var rot);
                ally.Car.Teleport(pos, rot);
                mission.Map.GroundPose(safe + Vector3.right * 36f, Vector3.forward, out var far, out rot);
                other.Car.Teleport(far, rot);
                var flak = boss.Parts.FindAll(p => p.Role == BossRole.Flak);
                boss.Core.TakeHit(player, 10f, boss.Core.AimPoint);
                boss.Core.TakeHit(ally.gameObject, 10f, boss.Core.AimPoint);
                flak[0].TakeHit(player, 5000f, flak[0].AimPoint);
                flak[1].TakeHit(ally.gameObject, 5000f, flak[1].AimPoint);
                int tick = SimulationDriver.Instance.Tick;
                foreach (var attacker in new[] { ally.gameObject, player })
                {
                    var plane = PlaneUnit.Spawn(mission.Map, safe + Vector3.back * 60f, safe + Vector3.forward * 100f);
                    // Overlapping visible circles threaten one car. The other warning is still hidden or already released.
                    mission.Strikes.Add(pos, 7f, 30f, 2f, plane.gameObject, tick);
                    mission.Strikes.Add(pos, 7f, 30f, 3f, plane.gameObject, tick);
                    mission.Strikes.Add(far, 7f, 30f, 4f, plane.gameObject, tick, .5f);
                    mission.Strikes.Add(far, 7f, 30f, .2f, plane.gameObject, tick);
                    plane.TakeHit(attacker, 1000f, plane.AimPoint);
                }
                float expectedDamage = 10f;
                if (win)
                {
                    boss.Core.TakeHit(ally.gameObject, (boss.Core.Health - 8f) / boss.Core.DamageTaken, boss.Core.AimPoint);
                    expectedDamage += boss.Core.Health;
                    boss.Core.TakeHit(player, 90000f, boss.Core.AimPoint);
                }
                else
                {
                    for (int step = 0; step < 45000 && !boss.ReachedTown; step++) boss.Step(tick + step, true);
                    Assert.IsTrue(boss.ReachedTown);
                }
                mission.SimulationStep(tick);
                Assert.AreEqual(win ? MissionDirector.Outcome.Victory : MissionDirector.Outcome.Defeat, mission.Result);
                var frozen = mission.FinalContribution;
                Assert.That(frozen.CoreDamage, Is.EqualTo(expectedDamage).Within(.01f));
                Assert.AreEqual(1, frozen.Stations);
                Assert.AreEqual(1, frozen.AlliesSaved);
                var latePlane = PlaneUnit.Spawn(mission.Map, safe + Vector3.back * 60f, safe + Vector3.forward * 100f);
                mission.Strikes.Add(far, 8f, 30f, 2f, latePlane.gameObject, tick);
                Assert.IsTrue(latePlane.TakeHit(player, 1000f, latePlane.AimPoint));
                for (int i = 0; i < 450 && flow.State != FlowState.Results; i++) yield return null;
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.AreEqual(frozen, mission.FinalContribution);
                string rendered = GameObject.Find("PersonalStats").GetComponent<TMP_Text>().text;
                Assert.AreEqual(Loc.Format("mission.contribution", "d", Mathf.RoundToInt(expectedDamage), "s", 1, "a", 1), rendered);
                Debug.Log($"[PersonalContribution] {mission.Result} actualCore={frozen.CoreDamage:F1} stations={frozen.Stations} allies={frozen.AlliesSaved}");
                flow.EnterMenu();
                yield return null;
            }
        }
    }
}
