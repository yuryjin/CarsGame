using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class SettlementMissionPlayTests
    {
        private GameFlow flow;
        private RaceManager race;
        private float capture;
        private string saved;
        [SetUp] public void SaveClock() { capture = Time.captureDeltaTime; }

        [UnitySetUp]
        public IEnumerator Load()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
        }

        [TearDown] public void Restore()
        {
            if (flow != null) flow.EnterMenu();
            if (saved != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            Time.captureDeltaTime = capture;
        }

        private static void Pursue(CarController car, ScriptedDriverInput input, Vector3 point, bool fire)
        {
            Vector3 aim = point - car.transform.position; aim.y = 0f;
            float angle = Vector3.SignedAngle(car.transform.forward, aim, Vector3.up);
            float speed = Mathf.Lerp(77f, 24f, Mathf.Clamp01(Mathf.Abs(angle) / 85f));
            input.Set(car.SpeedKmh < speed ? 1f : 0f, Mathf.Clamp(angle / 28f - car.Body.angularVelocity.y * .22f, -1f, 1f),
                car.SpeedKmh > speed + 15f, fire);
        }

        private static TrooperUnit Nearby(SettlementMission mission, Vector3 point)
        {
            TrooperUnit nearest = null;
            float best = float.MaxValue;
            foreach (var enemy in mission.Attackers)
            {
                if (enemy == null || !enemy.Targetable) continue;
                float distance = (point - enemy.transform.position).sqrMagnitude;
                if (distance >= best) continue;
                nearest = enemy; best = distance;
            }
            return nearest;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator EscortCanClearAmbushersAndReachingTownAloneCannotSaveTheTanker()
        {
            Object.FindAnyObjectByType<MenuScreens>().Show("events");
            GameObject.Find("Start_m3").GetComponent<Button>().onClick.Invoke();
            foreach (bool defend in new[] { true, false })
            {
                while (race.State == RaceState.Countdown) yield return null;
                var mission = SettlementMission.Current;
                var car = race.Player.Car;
                var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(input);
                Assert.NotNull(GameObject.Find("TankerDestination"));
                Assert.AreEqual(mission.Objective.MaxHealth, mission.Objective.Health);
                Assert.IsFalse(mission.Objective.TakeHit(car.gameObject, 100f, mission.Objective.AimPoint));
                float startRange = mission.Remaining;
                if (!defend)
                {
                    mission.Map.GroundPose(mission.Destination, Vector3.forward, out var pos, out var rot);
                    car.Teleport(pos, rot);
                    input.Set(0f, 0f, true);
                }
                bool sawAmbush = false;
                int hits = 0;
                void Damaged(MissionUnit unit, GameObject source, float amount)
                {
                    if (unit == mission.Objective && Teams.Of(source) == Team.Enemy) hits++;
                }
                MissionUnit.Damaged += Damaged;
                try
                {
                    for (int i = 0; i < 2100 && flow.State == FlowState.Racing; i++)
                    {
                        var enemy = Nearby(mission, car.transform.position);
                        if (defend) Pursue(car, input, enemy == null ? mission.Destination : enemy.transform.position, enemy != null);
                        if (defend && i % 110 == 0 && enemy != null)
                            Debug.Log($"[ConvoyDrive] tick={i} speed={car.SpeedKmh:F0} targetDist={Vector3.Distance(car.transform.position,enemy.AimPoint):F1} height={car.transform.position.y:F1} enemyHP={enemy.Health:F0} shells={RaceDirector.Current.ShotsFired}");
                        sawAmbush |= mission.Threatened;
                        yield return null;
                    }
                }
                finally { MissionUnit.Damaged -= Damaged; }
                Debug.Log($"[WaterConvoy] defend={defend} health={mission.Objective.Health:F0} distanceLeft={mission.Remaining:F0}/{startRange:F0} kills={race.Player.Kills} hits={hits} sawAmbush={sawAmbush}");
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.AreEqual(defend, race.Results.Player.Finished);
                Assert.IsTrue(sawAmbush);
                Assert.AreEqual(Loc.Get("events.m3.name." + (defend ? "win" : "loss")), GameObject.Find("Summary").transform.Find("Detail").GetComponent<TMP_Text>().text);
                if (defend)
                {
                    Assert.GreaterOrEqual(race.Player.Kills, 1);
                    Assert.AreEqual(0f, mission.Remaining);
                    flow.RetryRace();
                    yield return null;
                }
                else
                {
                    Assert.Greater(hits, 0);
                    Assert.Greater(mission.Remaining, 2f);
                    Assert.Less(Vector3.Distance(car.transform.position, mission.Destination), 6f);
                }
            }
        }
    }
}
