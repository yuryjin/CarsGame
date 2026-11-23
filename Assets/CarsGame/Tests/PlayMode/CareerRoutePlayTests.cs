using System.Collections;
using System.Linq;
using NUnit.Framework;
using RacingGame.Core.Save;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class CareerRoutePlayTests
    {
        private GameFlow flow;
        private RaceManager race;
        private MenuScreens menu;
        private string profile;
        private float capture;

        [UnitySetUp]
        public IEnumerator Open()
        {
            capture = Time.captureDeltaTime;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            menu = Object.FindAnyObjectByType<MenuScreens>();
            profile = JsonUtility.ToJson(PlatformGlue.Save.Data);
            PlatformGlue.Save.Data.progress = new ProgressData();
            menu.Show("main");
        }

        [TearDown]
        public void Restore()
        {
            if (flow != null) flow.EnterMenu();
            if (profile != null)
            {
                JsonUtility.FromJsonOverwrite(profile, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        private RaceEventDefinition Event(string id) => flow.events.Single(e => e.eventId == id);
        private static Button Button(string name) => GameObject.Find(name).GetComponent<Button>();

        private IEnumerator Go()
        {
            while (race.State == RaceState.Countdown) yield return null;
            Assert.AreEqual(RaceState.Racing, race.State);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ContinueLeadsFromDrivingSchoolAcrossTheBridgeToRaceFive()
        {
            var data = PlatformGlue.Save.Data;
            Button("Play").onClick.Invoke();
            for (int i = 1; i <= 4; i++)
            {
                yield return Go();
                Assert.AreEqual("e" + i, flow.CurrentEvent.eventId);
                Assert.AreEqual(i == 4, Object.FindAnyObjectByType<WastelandTrack>() != null);
                if (i == 4) Assert.AreEqual(2, race.laps);
                // Finish without earning the skill stars; the cup must not send this driver back to e1.
                race.Player.Deaths = 1;
                race.QaFinishNow();
                yield return null;
                Assert.AreEqual(FlowState.Results, flow.State);
                Assert.Contains("e" + (i + 1), data.progress.unlockedEvents);
                Assert.AreSame(Event("e" + (i + 1)), flow.NextEvent(flow.CurrentEvent));
                Button("ResultAction0").onClick.Invoke();
                yield return null;
            }
            Assert.AreSame(Event("e5"), flow.CurrentEvent);
            flow.EnterMenu();

            var legacy = SaveMigrator.FromJson("{\"version\":4,\"progress\":{\"unlockedEvents\":[\"e2\",\"e3\",\"e4\",\"e5\"],\"bests\":[{\"eventId\":\"e4\",\"bestTimeMs\":311360,\"stars\":5}]}}");
            data.progress = legacy.progress;
            menu.Show("events");
            Assert.IsTrue(Button("Start_e5").interactable);
            Button("Start_e4").onClick.Invoke();
            yield return Go();
            Assert.AreEqual(5, data.progress.bests.Single(b => b.eventId == "e4").stars);
            Assert.AreEqual(311360, data.progress.bests.Single(b => b.eventId == "e4").bestTimeMs);
            Assert.IsNotNull(Object.FindAnyObjectByType<WastelandTrack>());
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FourthPlaceCanOpenRaceTwoWithTwoDifferentCareerStars()
        {
            var data = PlatformGlue.Save.Data;
            var e1 = Event("e1");
            var e2 = Event("e2");
            for (int attempt = 0; attempt < 4; attempt++)
            {
                flow.StartRace(e1, flow.tiers.Single(t => t.tierId == "normal"));
                yield return Go();
                // Control only the arrival order and the two goal counters; payout and unlock run normally.
                var rivals = race.Participants.Where(p => !p.isPlayer).Take(3).ToArray();
                Assert.AreEqual(3, rivals.Length);
                for (int slot = 0; slot < rivals.Length; slot++)
                {
                    var rival = rivals[slot];
                    var progress = rival.Progress;
                    var path = progress.Path;
                    path.Pose(path.startLineDistance - 1f, (slot - 1) * 2.5f, out var at, out var facing);
                    rival.Car.Teleport(at + Vector3.up * .43f, facing);
                    progress.Counter.ApplyStanding(progress.Distance, race.laps - 1, 0, true);
                    rival.Car.Body.linearVelocity = facing * Vector3.forward * 16f;
                    for (int tick = 0; tick < 80 && !race.HasFinished(rival); tick++) yield return new WaitForFixedUpdate();
                    Assert.IsTrue(race.HasFinished(rival), rival.displayName + " crossed the line before the player");
                }
                race.Player.Deaths = attempt == 1 ? 0 : 1;
                if (attempt >= 2)
                    for (int n = 0; n < e1.RequiredSkillCount; n++) race.Player.KillTally.Record(500 + n);
                race.QaFinishNow();
                yield return null;
                Assert.AreEqual(4, race.Results.Player.Position);
                Assert.AreEqual(Mathf.Min(attempt, 2), data.TotalStars);
                Assert.AreEqual(attempt >= 2, flow.IsUnlocked(e2));
                Assert.AreEqual(attempt == 2, flow.LastUnlocked.Contains(e2));
                flow.EnterMenu();
                menu.Show("events");
                Assert.AreEqual(attempt >= 2, Button("Start_e2").interactable);
                if (attempt < 2)
                {
                    var status = Button("Start_e2").transform.parent.Find("Status").GetComponent<TMP_Text>();
                    Assert.AreEqual(Loc.Format("events.locked_stars", "name", Loc.Get(e1.nameKey), "n", 2 - attempt), status.text);
                }
            }
            Assert.AreEqual(1, data.progress.unlockedEvents.Count(id => id == "e2"));
            data.progress.unlockedEvents.Clear();
            Assert.IsTrue(flow.IsUnlocked(e2), "A loaded profile can use stars earned before this update.");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator DriftAndBridgeStarsMatchTheLiveCountersOnTheirCareerRoutes()
        {
            var progress = PlatformGlue.Save.Data.progress;
            foreach (string id in new[] { "e7", "e10" })
            {
                var ev = Event(id);
                progress.unlockedEvents.Add(id);
                var best = new EventBest { eventId = id, stars = 2, bestTimeMs = 180000 };
                progress.bests.Add(best);
                for (int count = ev.RequiredSkillCount - 1; count <= ev.RequiredSkillCount; count++)
                {
                    flow.StartRace(ev, flow.tiers.Single(t => t.tierId == "normal"));
                    yield return Go();
                    var car = race.Player.Car;
                    var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                    car.SetInputSource(input);
                    for (int n = 0; n < count; n++)
                    {
                        if (ev.skillGoal == SkillGoal.Shortcuts)
                            yield return TrackForkPlayTests.DriveBranch(race, TrackPath.Instance.Fork, input, ev.reversed);
                        else
                        {
                            // Prepare the approach on this route, then earn the boost with steering and brake.
                            var path = TrackPath.Instance;
                            path.Pose(path.startLineDistance + 100f, 0f, out var position, out var rotation);
                            car.Teleport(position + Vector3.up * .5f, rotation);
                            input.Set(0f, 0f);
                            for (int i = 0; i < 20; i++) yield return null;
                            car.Body.linearVelocity = car.transform.forward * 20f;
                            input.Set(1f, 1f, true);
                            for (int i = 0; i < 120 && car.DriftStage < 1; i++) yield return null;
                            Assert.Greater(car.DriftStage, 0);
                            input.Set(1f, 0f);
                            yield return null;
                        }
                    }
                    Button("LiveGoals").onClick.Invoke();
                    yield return new WaitForSeconds(.3f);
                    string expected = StarGoalText.Description(ev, 1) + " (" + count + "/" + ev.RequiredSkillCount + ")";
                    StringAssert.Contains(expected, GameObject.Find("Goals").GetComponent<TMP_Text>().text);
                    race.QaFinishNow();
                    yield return null;
                    Assert.AreEqual(count, ev.SkillCount(race.Results.Player));
                    Assert.AreEqual(count == ev.RequiredSkillCount ? 2 : 0, flow.LastStarsEarned & 2);
                    Assert.AreEqual(2, best.stars & 2);
                    flow.EnterMenu();
                }
            }
        }
    }
}
