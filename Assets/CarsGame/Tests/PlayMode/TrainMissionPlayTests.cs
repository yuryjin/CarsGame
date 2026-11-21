using System.Collections;
using NUnit.Framework;
using RacingGame.Core.Economy;
using RacingGame.Core.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class TrainMissionPlayTests
    {
        private GameFlow flow;
        private RaceManager race;
        private Transform trophy;
        private string profile;
        private float capture;
        private int startTick;
        private float Elapsed => SimulationDriver.TicksToSeconds(SimulationDriver.Instance.Tick - startTick);

        [UnitySetUp]
        public IEnumerator OpenEvent()
        {
            capture = Time.captureDeltaTime;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            var save = PlatformGlue.Save;
            profile = JsonUtility.ToJson(save.Data);
            save.Data.progress = new Core.Save.ProgressData();
            Object.FindAnyObjectByType<MenuScreens>().Show("main");
            Click("ChooseRace");
            int normal = System.Array.FindIndex(System.Array.FindAll(flow.tiers, t => t != null), t => t.tierId == "normal");
            Click("Tier" + normal);
            Click("Start_m2");
            Assert.AreEqual("normal", RaceSetup.TierId);
            Assert.AreEqual(FlowState.Racing, flow.State);
            Assert.AreEqual(RaceState.Countdown, race.State);
            Assert.AreEqual(1, race.Participants.Count);
            Assert.AreEqual(CarBodies.DefaultId, race.Player.GetComponent<CarBody>().BodyId);
            Assert.AreSame(TrainMission.Current, race.spawner.Site);
            Assert.IsNull(MissionDirector.Current);
            trophy = TrainMission.Current.Route.Trophy;
            Assert.IsTrue(trophy.IsChildOf(TrainMission.Current.Route.Tail));
            Vector3 start = TrainMission.Current.Route.Locomotive.position;
            yield return null;
            Assert.AreEqual(start, TrainMission.Current.Route.Locomotive.position);
            yield return WaitForStart();
        }

        [TearDown]
        public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (profile != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(profile, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        private static void Click(string name)
        {
            var button = GameObject.Find(name)?.GetComponent<Button>();
            Assert.IsNotNull(button, name);
            Assert.IsTrue(button.interactable, name);
            button.onClick.Invoke();
        }

        private IEnumerator WaitForStart()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (race.State == RaceState.Countdown && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(RaceState.Racing, race.State, $"clock={RaceClock.Now:F2}, pause={PauseController.Reasons}");
            startTick = SimulationDriver.Instance.Tick;
        }

        private static void RetryFromResults()
        {
            foreach (var button in Object.FindObjectsByType<Button>())
            {
                if (!button.name.StartsWith("ResultAction")) continue;
                var label = button.GetComponentInChildren<TMP_Text>();
                if (label == null || !label.text.StartsWith(Loc.Get("results.retry"))) continue;
                button.onClick.Invoke();
                return;
            }
            Assert.Fail("Retry is missing from the results actions");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator SwitchChangesTrackAndParkedTrainWaitsForThePlayer()
        {
            var mission = TrainMission.Current;
            var car = race.Player.Car;
            var drive = new TrainRouteTestDriver(car, mission.Route, true);
            car.SetInputSource(drive);
            for (int i = 0; i < 4500 && !mission.Diverted; i++) yield return null;
            Assert.IsTrue(mission.Diverted);
            Assert.IsFalse(mission.Route.PassedSwitch);
            float switchTime = Elapsed;
            var stop = car.gameObject.AddComponent<ScriptedDriverInput>();
            stop.Set(0f, 0f, true);
            car.SetInputSource(stop);
            for (int i = 0; i < 5000 && !mission.Route.AtStation; i++) yield return null;
            Assert.IsTrue(mission.Route.AtStation);
            Assert.IsFalse(mission.Route.InStationZone(car.transform.position));
            Assert.AreEqual(TrainOutcome.Running, mission.Outcome);
            Assert.AreEqual(0, PlatformGlue.Save.Local.races);
            car.SetInputSource(drive);
            for (int i = 0; i < 2200 && race.State == RaceState.Racing; i++) yield return null;
            AssertAward(TrainOutcome.CapturedAtStation);
            Debug.Log($"[TrainChase] switch={switchTime:F2}s collection={Elapsed:F2}s reward={flow.LastReward}");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator MovingTransferPausesBreaksAndPaysOnceAfterAFullRetry()
        {
            var mission = TrainMission.Current;
            var car = race.Player.Car;
            var drive = new TrainEscortPlayTests.ChaseDriver(mission, armed: true);
            car.SetInputSource(drive);
            for (int i = 0; i < 4100 && mission.CaptureProgress < 0.15f; i++) yield return null;
            float held = mission.CaptureProgress;
            Assert.That(held, Is.InRange(0.15f, 0.9f));
            Assert.IsTrue(mission.Escort.Released);
            Vector3 tail = mission.Route.Tail.position;
            double elapsed = race.RaceTime;
            PauseController.Set(PauseReason.User, true);
            for (int i = 0; i < 12; i++) yield return null;
            Assert.AreEqual(tail, mission.Route.Tail.position);
            Assert.AreEqual(held, mission.CaptureProgress);
            Assert.AreEqual(elapsed, race.RaceTime, 0.001);
            PauseController.Set(PauseReason.User, false);

            drive.DropBack = true;
            for (int i = 0; i < 180 && mission.CaptureProgress > 0; i++) yield return null;
            Assert.AreEqual(0f, mission.CaptureProgress);
            Assert.AreEqual(TrainOutcome.Running, mission.Outcome);
            drive.DropBack = false;
            int restartedAt = SimulationDriver.Instance.Tick;
            for (int i = 0; i < 4500 && race.State == RaceState.Racing; i++) yield return null;
            AssertAward(TrainOutcome.CapturedOnTrain);
            Assert.GreaterOrEqual(SimulationDriver.TicksToSeconds(SimulationDriver.Instance.Tick - restartedAt), TrainMission.HoldSeconds);
            Debug.Log($"[TrainChase] moving transfer={Elapsed:F2}s hold={mission.CaptureProgress:P0} kills={race.Results.Player.Kills} reward={flow.LastReward}");
            for (int i = 0; i < 8; i++) yield return null;
            AssertAward(TrainOutcome.CapturedOnTrain);
        }

        private void AssertAward(TrainOutcome expected)
        {
            Assert.AreEqual(expected, TrainMission.Current.Outcome);
            Assert.AreEqual(FlowState.Results, flow.State);
            Assert.IsTrue(race.Results.Player.Finished);
            Assert.AreEqual(1, race.Results.Entries.Count);
            Assert.AreSame(trophy, TrainMission.Current.Route.Trophy);
            Assert.IsTrue(trophy.IsChildOf(race.Player.transform));
            Assert.Greater(flow.LastReward, 0);
            var save = PlatformGlue.Save;
            Assert.AreEqual(flow.LastReward, save.Local.coinsEarned);
            Assert.AreEqual(1, save.Local.races);
            Assert.AreEqual(1, save.Local.wins);
            var best = save.Data.progress.bests.Find(b => b.eventId == TrainMission.EventId);
            Assert.Greater(best.bestTimeMs, 0);
            Assert.AreEqual(flow.CurrentEvent.StarMask(race.Results.Player), best.stars);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TrainEscapesOnceThenRetryAndOtherMapsStartClean()
        {
            var mission = TrainMission.Current;
            var save = PlatformGlue.Save;
            int finishes = 0;
            race.OnRaceFinished += _ => finishes++;
            for (int i = 0; i < 6000 && flow.State == FlowState.Racing; i++) yield return null;
            Assert.AreEqual(TrainOutcome.Escaped, mission.Outcome);
            Assert.IsTrue(trophy.IsChildOf(mission.Route.Tail));
            Assert.AreEqual(FlowState.Results, flow.State);
            Assert.IsFalse(race.Results.Player.Finished);
            Assert.AreEqual(0, flow.LastReward);
            Assert.AreEqual(0, save.Local.coinsEarned);
            Assert.AreEqual(1, save.Local.races);
            Assert.AreEqual(0, save.Data.progress.bests.Find(b => b.eventId == TrainMission.EventId).bestTimeMs);
            yield return null;
            Assert.AreEqual(1, finishes);
            Debug.Log($"[TrainChase] border={Elapsed:F2}s reward={flow.LastReward}");

            RetryFromResults();
            var retry = TrainMission.Current;
            Assert.AreNotSame(mission, retry);
            Assert.IsFalse(mission.gameObject.activeSelf);
            Assert.AreEqual(TrainOutcome.Running, retry.Outcome);
            Assert.AreEqual(0f, retry.CaptureProgress);
            yield return null;
            Assert.IsTrue(mission == null);

            var tier = RaceSetup.Tier;
            flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_m1"), tier);
            Assert.IsNull(TrainMission.Current);
            Assert.IsNotNull(MissionDirector.Current);
            Assert.AreEqual(6, race.Participants.Count);
            flow.StartRace(System.Array.Find(flow.events, ev => ev != null && ev.eventId == "e1"), tier);
            yield return null;
            Assert.IsNull(MissionDirector.Current);
            Assert.IsNull(race.spawner.Site);
            Assert.IsNotNull(GameObject.Find("RaceTrack/Track_Road"));
            Assert.IsTrue(race.Player.Progress.enabled);
            Assert.AreEqual(1, finishes);
        }
    }
}
