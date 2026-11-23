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
        [UnityTest, Timeout(240000)]
        public IEnumerator WellWavesAdvanceFromOppositeSidesAndRetryRestoresTheSettlement()
        {
            Object.FindAnyObjectByType<MenuScreens>().Show("events");
            GameObject.Find("Start_m4").GetComponent<Button>().onClick.Invoke();
            foreach (bool defend in new[] { true, false })
            {
                while (race.State == RaceState.Countdown) yield return null;
                var mission = SettlementMission.Current;
                var well = mission.Objective;
                var car = race.Player.Car;
                var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(input);
                Assert.AreEqual(1, mission.Wave);
                Assert.AreEqual(well.MaxHealth, well.Health);
                Assert.IsTrue(mission.WarningActive);
                Assert.IsEmpty(mission.Attackers);
                Assert.Less(mission.WaveOrigin.x, well.transform.position.x);
                input.Set(0f, 0f, true);
                if (!defend)
                {
                    mission.Map.GroundPose(well.transform.position + Vector3.forward * 91f, Vector3.forward, out var away, out var facing);
                    car.Teleport(away, facing);
                }
                bool eastWarning = false, rocket = false, backing = false;
                int stopped = 0, reverseTicks = 0;
                for (int tick = 0; tick < 4800 && flow.State == FlowState.Racing; tick++)
                {
                    if (mission.Wave == 2 && mission.WarningActive)
                    {
                        Assert.Greater(mission.WaveOrigin.x, well.transform.position.x);
                        eastWarning = true;
                    }
                    var foe = Nearby(mission, car.transform.position);
                    if (defend)
                    {
                        foreach (var enemy in mission.Attackers) if (enemy != null && enemy.Kind == UnitKind.RocketTrooper) rocket = true;
                        Vector3 at = foe != null ? foe.transform.position : mission.WaveOrigin;
                        Vector3 relative = car.transform.position - well.transform.position;
                        if (relative.x * (at.x - well.transform.position.x) < 0f && Mathf.Abs(relative.z) < 10f)
                            at = well.transform.position + Vector3.forward * 17f;
                        Vector3 aim = at - car.transform.position;
                        aim.y = 0f;
                        float angle = Vector3.SignedAngle(car.transform.forward, aim, Vector3.up);
                        if (foe != null && aim.magnitude < 16f && Mathf.Abs(angle) > 52f) backing = true;
                        if (Mathf.Abs(angle) < 14f || aim.magnitude > 29f) backing = false;
                        if (Mathf.Abs(car.SpeedKmh) < 3f && foe != null) stopped++; else stopped = 0;
                        if (stopped >= 34) { reverseTicks = 65; stopped = 0; }
                        if (reverseTicks > 0)
                        {
                            reverseTicks--;
                            input.Set(-1f, -.75f);
                        }
                        else if (backing)
                        {
                            float steer = Mathf.Clamp(angle / 23f, -1f, 1f);
                            bool rollingForward = Vector3.Dot(car.Body.linearVelocity, car.transform.forward) > 0f;
                            input.Set(-1f, rollingForward ? steer : -steer);
                        }
                        else Pursue(car, input, at, foe != null && aim.magnitude < 61f && Mathf.Abs(angle) < 13f);
                        if (tick % 180 == 0) Debug.Log($"[WellDrive] tick={tick} wave={mission.Wave} speed={car.SpeedKmh:F0} at={relative} hp={well.Health:F0} kills={race.Player.Kills} range={aim.magnitude:F0} yaw={angle:F0} shots={RaceDirector.Current.ShotsFired}");
                    }
                    if (!defend && tick % 600 == 0) Debug.Log($"[WellIdle] frame={tick} sim={SimulationDriver.Instance.Tick} running={MissionUnit.Running} attackers={mission.Attackers.Count} hp={well.Health:F0}");
                    yield return null;
                }
                Debug.Log($"[LastWell] defend={defend} wave={mission.Wave} hp={well.Health:F0} kills={race.Player.Kills} eastWarning={eastWarning}");
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.AreEqual(defend, race.Results.Player.Finished);
                Assert.AreEqual(Loc.Get("events.m4.name." + (defend ? "win" : "loss")), GameObject.Find("Summary").transform.Find("Detail").GetComponent<TMP_Text>().text);
                if (defend) { Assert.IsTrue(eastWarning && rocket); Assert.AreEqual(5, race.Player.Kills); }
                else Assert.IsFalse(well.Alive);
                if (defend)
                {
                    var buttons = GameObject.Find("Actions").GetComponentsInChildren<Button>();
                    var retry = System.Array.Find(buttons, b => b.GetComponentInChildren<TMP_Text>().text.Contains(Loc.Get("results.retry")));
                    Assert.NotNull(retry);
                    retry.onClick.Invoke();
                    yield return null;
                }
            }
        }
    }
}
