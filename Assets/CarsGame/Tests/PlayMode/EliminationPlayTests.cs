using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class EliminationPlayTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator APhysicalTieUsesGridOrderAndOnlyTheSurvivorWins()
        {
            float capture = Time.captureDeltaTime;
            GameFlow flow = null;
            string previous = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                while (!PlatformGlue.IsReady) yield return null;
                yield return null; yield return null;
                previous = JsonUtility.ToJson(PlatformGlue.Save.Data);
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>();
                Object.FindAnyObjectByType<MenuScreens>().Show("events");
                var card = GameObject.Find("elimination");
                string stats = card.transform.Find("Stats").GetComponent<TMP_Text>().text;
                StringAssert.Contains(Loc.Format("events.laps", "n", 3), stats);
                StringAssert.Contains(Loc.Format("events.rivals", "n", 3), stats);
                StringAssert.Contains(Loc.Get("format.elimination.rule"), stats);
                card.transform.Find("Start_elimination").GetComponent<Button>().onClick.Invoke();
                while (race.State == RaceState.Countdown) yield return null;
                var cars = race.Participants.ToArray();
                var path = race.Player.Progress.Path;
                var tangent = path.Spline.Tangent(path.startLineDistance);
                var lateral = Vector3.Cross(Vector3.up, tangent).normalized;
                int results = 0;
                race.OnRaceFinished += _ => results++;
                for (int i = 0; i < cars.Length; i++)
                {
                    var car = cars[i].Car;
                    car.Teleport(path.Position(path.startLineDistance) + lateral * ((i - 1.5f) * 2.85f)
                        - tangent * .16f + Vector3.up * .42f, Quaternion.LookRotation(tangent));
                    car.Body.constraints = RigidbodyConstraints.FreezeAll;
                    car.SetInputSource(car.gameObject.AddComponent<ScriptedDriverInput>());
                }
                for (int i = 0; i < 5; i++) yield return null;
                foreach (var p in cars)
                {
                    PhotoFinishPlayTests.ArmLap(p.Progress);
                    p.Car.Body.constraints = RigidbodyConstraints.FreezeRotation;
                    p.Car.Body.linearVelocity = tangent * 23f;
                }
                for (int i = 0; i < 20 && race.EliminationStage == 0; i++) yield return null;
                Assert.AreEqual(1, cars.Select(p => p.Progress.LastCrossingTick).Distinct().Count());
                Assert.AreEqual(1, race.EliminationStage);
                Assert.AreSame(cars.OrderByDescending(p => p.Car.CarId).First(), race.LatestElimination);
                Assert.AreSame(race.Player, race.LatestElimination);
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.IsTrue(race.Results.Player.Eliminated);
                Debug.Log($"[Elimination] same tick {cars[0].Progress.LastCrossingTick}, grid ids {string.Join(",", cars.Select(p => p.Car.CarId))}, out {race.LatestElimination.Car.CarId}");

                flow.RetryRace();
                while (race.State == RaceState.Countdown) yield return null;
                Assert.AreEqual(0, race.EliminationStage);
                var retired = new System.Collections.Generic.List<string>();
                race.OnStageEliminated += p => retired.Add(p.displayName);
                int stall = 0;
                foreach (var p in race.Participants)
                    if (!p.isPlayer)
                    {
                        path.Pose(path.startLineDistance - 55f - stall++ * 12f, path.halfWidth - 1.45f, out var parked, out var rotation);
                        p.Car.Teleport(parked + Vector3.up * .45f, rotation);
                        p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                        p.GetComponent<AiGunner>().enabled = false;
                    }
                Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
                float deadline = Time.realtimeSinceStartup + 200f;
                int tick = 0;
                while (flow.State == FlowState.Racing && Time.realtimeSinceStartup < deadline)
                {
                    if (tick++ % 1000 == 0) Debug.Log($"[EliminationLap] tick={tick} laps={race.Player.Progress.LapsCompleted}, gates={race.Player.Progress.GatesPassedThisLap} stage={race.EliminationStage} position={race.Player.Progress.Distance:F1} speed={race.Player.Car.SpeedKmh:F1}");
                    yield return null;
                }
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.AreEqual(3, retired.Distinct().Count());
                Assert.AreEqual(3, race.EliminationStage);
                Assert.AreEqual(3, race.Player.Progress.LapsCompleted);
                Assert.AreEqual(1, race.Results.Entries.Count(e => e.Finished));
                Assert.IsTrue(race.Results.Player.Finished);
                Assert.AreEqual(1, race.Results.Player.Position);
                Assert.AreEqual(2, results);
                Debug.Log($"[Elimination] three natural laps, retired {string.Join(", ", retired)}, one survivor");
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
