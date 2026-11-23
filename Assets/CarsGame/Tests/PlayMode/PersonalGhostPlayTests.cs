using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using RacingGame.Core.Ghost;
using RacingGame.Core.Platform;
using RacingGame.Core.Save;
using RacingGame.Platform;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class PersonalGhostPlayTests
    {
        private sealed class Cloud : ICloudSave
        {
            public string Json;
            public Task<Result<string>> LoadAsync() => Task.FromResult(Result<string>.Success(Json));
            public void Queue(string json) => Json = json;
            public Task<Result> FlushAsync() => Task.FromResult(Result.Success);
        }

        private SaveService original;
        private string key;
        private PlayerPrefsSaveStore store;
        private float capture;
        private GameFlow flow;
        private RaceManager race;
        private RaceEventDefinition e1, e2;

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }

        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (original != null) SetService(original);
            if (e1 != null) Object.DestroyImmediate(e1);
            if (e2 != null) Object.DestroyImmediate(e2);
            if (key != null)
            {
                foreach (var suffix in new[] { "", ".backup", ".damaged" }) PlayerPrefs.DeleteKey(key + suffix);
                PlayerPrefs.Save();
            }
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        private static void SetService(SaveService service) => typeof(PlatformGlue).GetProperty("Save").SetValue(null, service);

        private IEnumerator Reload(ICloudSave cloud = null)
        {
            var next = new SaveService(store, () => cloud, () => Time.realtimeSinceStartup, "ghost-local");
            var loading = next.LoadAsync();
            while (!loading.IsCompleted) yield return null;
            loading.GetAwaiter().GetResult();
            SetService(next);
        }

        private RaceEventDefinition Solo(string id)
        {
            var ev = Object.Instantiate(Array.Find(flow.events, e => e.eventId == id));
            ev.aiCountOverride = 0;
            ev.pickupCount = 0;
            return ev;
        }

        private IEnumerator Start(RaceEventDefinition ev)
        {
            flow.StartRace(ev, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            Assert.AreEqual(RaceState.Racing, race.State, ev.eventId);
        }

        private IEnumerator DriveToFinish(float delayAtStart = 0)
        {
            while (race.RaceTime < delayAtStart) yield return null;
            Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
            while (race.State == RaceState.Racing && race.RaceTime < 385) yield return null;
            Assert.AreEqual(FlowState.Results, flow.State, "The solo driver must complete the real gates");
            Assert.IsTrue(race.Results.Player.Finished);
            Assert.AreEqual(race.laps, race.Results.Player.Laps);
        }

        private IEnumerator StartFromEventList(string id)
        {
            Object.FindAnyObjectByType<MenuScreens>().Show("events");
            var button = GameObject.Find("Start_" + id).GetComponent<UnityEngine.UI.Button>();
            Assert.IsTrue(button.interactable, id);
            button.onClick.Invoke();
            while (race.State == RaceState.Countdown) yield return null;
            Assert.AreEqual(RaceState.Racing, race.State);
            Assert.AreEqual(id, flow.CurrentEvent.eventId);
        }

        private static GhostRecord LegacyCircuit(int hash, TrackPath path, int laps)
        {
            var legacy = new GhostRecord { TrackHash = hash, PlayerName = "Legacy driver" };
            for (int i = 0; i <= 800; i++)
            {
                float distance = path.startLineDistance + Mathf.Lerp(-5f, laps * path.Length + 1f, i / 800f);
                legacy.AddPose(path.Position(distance), Quaternion.identity, i * .5, 0, 0);
            }
            return legacy;
        }

        private static string Retimed(GhostRecord source, string name, double scale)
        {
            var record = new GhostRecord { PlayerName = name, TrackHash = source.TrackHash };
            foreach (var sample in source.Samples)
                record.AddPose(sample.Position, sample.Rotation, sample.TimeMilliseconds * scale / 1000d, sample.Steer, sample.Flags);
            return record.ToBase64();
        }

        private static GhostCar Ghost() => Object.FindAnyObjectByType<GhostCar>();

        private IEnumerator OpenProfile()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep * 2;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            original = PlatformGlue.Save;
            key = "qa.personalghost." + Guid.NewGuid().ToString("N");
            store = new PlayerPrefsSaveStore(key);
            yield return Reload();
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            e1 = Solo("e1"); e2 = Solo("e2");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator LegacyGhostThenE1E2AndSlowerE1KeepBothWinnersAfterCloudMergeAndRestart()
        {
            yield return OpenProfile();
            yield return Start(e1);
            int h1 = flow.GetComponent<GhostRecorder>().Record.TrackHash;
            var legacy = LegacyCircuit(h1, TrackPath.Instance, e1.laps);
            Assert.NotNull(GhostSectorTimes.Measure(TrackPath.Instance, legacy, e1.laps));
            flow.EnterMenu();
            store.Write("{\"version\":6,\"bestGhost\":\"" + legacy.ToBase64() + "\"}");
            yield return Reload();
            e1.laps--;
            yield return Start(e1);
            Assert.IsNull(Ghost(), "An old two-lap replay cannot appear during a one-lap race");
            flow.EnterMenu(); e1.laps++;
            yield return Start(e1);
            Assert.AreEqual("Legacy driver", Ghost().Record.PlayerName);
            yield return DriveToFinish();
            var first = PlatformGlue.Save.Data.personalGhosts.Find("e1", h1, e1.laps);
            Assert.Less(first.DurationMilliseconds, legacy.DurationMilliseconds);
            Assert.AreEqual((int)(race.Results.Player.TotalTime * 1000), first.DurationMilliseconds);
            flow.EnterMenu();
            yield return Start(e2);
            int h2 = flow.GetComponent<GhostRecorder>().Record.TrackHash;
            Assert.AreNotEqual(h1, h2);
            Assert.IsNull(Ghost(), "E1 must not appear on reverse E2");
            yield return DriveToFinish();
            var second = PlatformGlue.Save.Data.personalGhosts.Find("e2", h2, e2.laps);
            Assert.AreEqual((int)(race.Results.Player.TotalTime * 1000), second.DurationMilliseconds);
            flow.EnterMenu();
            yield return Start(e1);
            Assert.AreEqual(first.DurationMilliseconds, Ghost().Record.DurationMilliseconds);
            yield return DriveToFinish(35);
            Assert.Greater(race.Results.Player.TotalTime * 1000, first.DurationMilliseconds);
            Assert.AreEqual(first.DurationMilliseconds, PlatformGlue.Save.Data.personalGhosts.Find("e1", h1, e1.laps).DurationMilliseconds);
            TestContext.WriteLine($"e1 {first.DurationMilliseconds} ms, e2 {second.DurationMilliseconds} ms, e1 retry {race.Results.Player.TotalTime:F3} s");

            flow.EnterMenu();
            var other = new SaveData { deviceId = "ghost-remote" };
            other.personalGhosts.KeepBest("e1", e1.laps, Retimed(first, "Slower cloud E1", 1.15), 1);
            other.personalGhosts.KeepBest("e2", e2.laps, Retimed(second, "Faster cloud E2", .9), 2);
            var cloud = new Cloud { Json = SaveMigrator.ToJson(other) };
            yield return Reload(cloud);
            Assert.IsTrue(PlatformGlue.Save.CloudSynced);
            yield return Reload(); // A fresh service reads the merged profile from the local store.
            yield return Start(e2);
            Assert.AreEqual("Faster cloud E2", Ghost().Record.PlayerName);
            Assert.Less(Ghost().Record.DurationMilliseconds, second.DurationMilliseconds);
            flow.EnterMenu();
            yield return Start(e1);
            Assert.AreEqual(first.DurationMilliseconds, Ghost().Record.DurationMilliseconds);
            Assert.AreEqual(first.PlayerName, Ghost().Record.PlayerName);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator FirstTimedRunShowsTheTrainingLapAndStoresItWithoutAPreviousGhost()
        {
            yield return OpenProfile();
            e1.laps = 2;
            yield return Start(e1);
            Assert.IsNull(Ghost());
            yield return DriveToFinish();
            var replay = flow.GetComponent<GhostRecorder>().Record;
            int[] sectors = GhostSectorTimes.MeasureLaps(TrackPath.Instance, replay, e1.laps).BestDurations();
            CollectionAssert.AreEqual(sectors, flow.LastTrainingSectors.milliseconds);
            Assert.That(sectors, Has.All.GreaterThan(0));
            var card = GameObject.Find("TrainingSectors");
            Assert.IsNotNull(card);
            int sum = sectors[0] + sectors[1] + sectors[2];
            string heading = card.transform.Find("SectorTitle").GetComponent<TMPro.TMP_Text>().text;
            Assert.AreEqual(Loc.Format("results.sector.training", "t", UiFactory.FormatMilliseconds(sum)), heading);
            for (int i = 0; i < 3; i++)
                StringAssert.EndsWith(UiFactory.FormatMilliseconds(sectors[i]), card.transform.Find("Sector" + (i + 1)).GetComponent<TMPro.TMP_Text>().text);
            var flush = PlatformGlue.Save.FlushAsync();
            while (!flush.IsCompleted) yield return null;
            flush.GetAwaiter().GetResult();
            yield return Reload();
            Assert.AreEqual(sum, PlatformGlue.Save.Data.progress.bests.Find(b => b.eventId == e1.eventId).sectors[0].LapMilliseconds);
            TestContext.WriteLine(heading + " = " + string.Join(" + ", sectors) + " ms");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator PersonalReplaySeeksAcrossARecordedRecoveryAndLeavesTheProfileUntouched()
        {
            yield return OpenProfile();
            yield return StartFromEventList("tt1");
            while (race.RaceTime < 1.5f) yield return null;
            var car = race.Player.Car;
            car.Teleport(car.transform.position + car.transform.forward * 7f + Vector3.up * .5f,
                car.transform.rotation * Quaternion.Euler(0f, 18f, 0f));
            yield return DriveToFinish();
            int hash = flow.GetComponent<GhostRecorder>().Record.TrackHash;
            var record = PlatformGlue.Save.Data.personalGhosts.Find("tt1", hash, 1);
            int cut = record.Samples.FindIndex(p => (p.Flags & GhostRecord.TeleportFlag) != 0);
            Assert.Greater(cut, 0);
            var after = record.Samples[cut];
            var before = record.Samples[cut - 1];
            Assert.Greater(Vector3.Distance(before.Position, after.Position), 4f);
            var menu = Object.FindAnyObjectByType<MenuScreens>();
            menu.GoTo("leaders");
            var save = PlatformGlue.Save.Data;
            string profile = SaveMigrator.ToJson(save);
            GameObject.Find("Replay_Local_tt1").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.IsTrue(flow.ViewingReplay);
            Assert.AreEqual(FlowState.Menu, flow.State);
            Assert.AreEqual(RaceState.Idle, race.State);
            var viewer = Ghost();
            Assert.AreEqual(viewer.transform, Camera.main.GetComponent<CarCameraFollow>().target);
            var toggle = GameObject.Find("ReplayToggle").GetComponent<UnityEngine.UI.Button>();
            var seek = GameObject.Find("ReplaySeek").GetComponent<UnityEngine.UI.Slider>();
            toggle.onClick.Invoke();
            Assert.IsTrue(flow.ReplayPaused);
            foreach (float moment in new[] { after.TimeSeconds - .001f, after.TimeSeconds, after.TimeSeconds + .02f, after.TimeSeconds - .001f })
            {
                seek.value = moment;
                Assert.IsTrue(record.EvaluatePose(moment, out var position, out var rotation));
                Assert.Less(Vector3.Distance(position, viewer.transform.position), .001f);
                Assert.Less(Quaternion.Angle(rotation, viewer.transform.rotation), .1f);
                yield return null; yield return null;
                Assert.AreEqual(moment, flow.ReplayTime, .0001f);
                Assert.Less(Vector3.Distance(position, viewer.transform.position), .001f);
            }
            toggle.onClick.Invoke();
            yield return null; yield return null;
            Assert.Greater(flow.ReplayTime, after.TimeSeconds - .001f);
            seek.value = record.Duration;
            Assert.IsTrue(flow.ReplayPaused);
            toggle.onClick.Invoke();
            Assert.AreEqual(0f, flow.ReplayTime);
            Assert.IsFalse(flow.ReplayPaused);
            GameObject.Find("Panel_replay").GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual("leaders", menu.Current);
            Assert.IsFalse(flow.ViewingReplay);
            Assert.AreEqual(profile, SaveMigrator.ToJson(save));
            TestContext.WriteLine($"Cut at {after.TimeMilliseconds} ms, distance {Vector3.Distance(before.Position, after.Position):F2} m; " +
                $"races {save.Races}, coins {save.Coins}, best {record.DurationMilliseconds} ms unchanged");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator SixFinishesKeepTheLastFiveWithTheirActualCarAndDifficultyAfterRestartAndAbort()
        {
            yield return OpenProfile();
            var actualBodies = new string[6];
            var actualTiers = new string[6];
            var actualTimes = new long[6];
            var menu = Object.FindAnyObjectByType<MenuScreens>();
            for (int i = 0; i < 6; i++)
            {
                if (i == 3)
                {
                    PlatformGlue.Save.Data.progress.cosmetics.Add("body:flea");
                    PlatformGlue.Save.Data.progress.body = "flea";
                    menu.Show("events");
                    int hard = Array.FindIndex(flow.tiers, tier => tier.tierId == "hard");
                    GameObject.Find("Tier" + hard).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                }
                yield return StartFromEventList("tt1");
                actualBodies[i] = race.Player.GetComponent<CarBody>().BodyId;
                actualTiers[i] = race.tier.tierId;
                yield return DriveToFinish();
                actualTimes[i] = (long)(race.Results.Player.TotalTime * 1000d);
                menu.GoTo("events");
            }
            Assert.AreNotEqual(actualBodies[2], actualBodies[3]);
            Assert.AreNotEqual(actualTiers[2], actualTiers[3]);
            var flush = PlatformGlue.Save.FlushAsync();
            while (!flush.IsCompleted) yield return null;
            flush.GetAwaiter().GetResult();
            yield return Reload();
            yield return StartFromEventList("tt1");
            flow.EnterMenu();
            yield return null;
            menu.Show("leaders");
            var history = PlatformGlue.Save.Data.progress.recentAttempts;
            Assert.AreEqual(5, history.Count);
            for (int i = 0; i < 5; i++)
            {
                int run = 5 - i;
                Assert.AreEqual(actualTimes[run], history[i].timeMs);
                Assert.AreEqual(actualBodies[run], history[i].setup.body);
                Assert.AreEqual(actualTiers[run], history[i].setup.tier);
                Assert.IsTrue(history[i].id.EndsWith("/" + (run + 1)));
                var card = GameObject.Find("RecentAttempt" + i).transform;
                string condition = card.Find("RecordSetup").GetComponent<TMPro.TMP_Text>().text;
                StringAssert.Contains(Loc.Get("difficulty." + actualTiers[run]), condition);
                StringAssert.Contains(Loc.Get("body." + actualBodies[run]), condition);
                var comparable = RecentAttempts.PreviousComparable(history, i);
                bool matches = i != 2 && i < 4;
                Assert.AreEqual(matches, comparable != null);
                if (matches) Assert.IsTrue(RecordSetup.Same(comparable.setup, history[i].setup));
                string shown = card.Find("Time").GetComponent<TMPro.TMP_Text>().text;
                StringAssert.StartsWith(UiFactory.FormatMilliseconds(actualTimes[run]), shown);
                Assert.AreEqual(matches, shown.Contains("\n"));
            }
            TestContext.WriteLine("Newest first: " + string.Join(", ", history.ConvertAll(a => $"{a.id} {a.timeMs} ms {a.setup.body}/{a.setup.tier}")));
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator SoloTimeTrialReplacesItsOwnGhostAndReturnsToAnArmedCareerField()
        {
            yield return OpenProfile();
            yield return StartFromEventList("tt1");
            Assert.AreEqual(1, race.Participants.Count);
            Assert.AreEqual(1, race.laps);
            Assert.IsNull(Ghost());
            Assert.IsEmpty(Object.FindObjectsByType<Pickup>());
            Assert.IsEmpty(Object.FindObjectsByType<MineHazard>());
            var car = race.Player.Car;
            Assert.IsTrue(car.Cannon.locked);
            Assert.AreEqual(SecondaryWeaponType.None, race.Player.Weapons.secondaryType);
            var command = car.gameObject.AddComponent<ScriptedDriverInput>();
            command.fire = true; car.SetInputSource(command);
            int ammo = car.Cannon.Ammo;
            for (int i = 0; i < 15; i++) yield return null;
            Assert.Greater(command.SamplesTaken, 0);
            Assert.AreEqual(ammo, car.Cannon.Ammo);
            Assert.AreEqual(0, RaceDirector.Instance.ShotsFired);
            Assert.AreNotEqual("0:00.000", GameObject.Find("Timer").GetComponent<TMPro.TMP_Text>().text);
            yield return DriveToFinish(28);
            var best = PlatformGlue.Save.Data.progress.bests.Find(b => b.eventId == "tt1");
            long originalTime = best.bestTimeMs;
            int hash = flow.GetComponent<GhostRecorder>().Record.TrackHash;

            flow.EnterMenu();
            yield return StartFromEventList("tt1");
            Assert.AreEqual(originalTime, Ghost().Record.DurationMilliseconds);
            yield return DriveToFinish();
            Assert.Less(best.bestTimeMs, originalTime);
            Assert.AreEqual(best.bestTimeMs, PlatformGlue.Save.Data.personalGhosts.Find("tt1", hash, 1).DurationMilliseconds);
            TestContext.WriteLine($"tt1 PB {originalTime} → {best.bestTimeMs} ms");

            flow.EnterMenu();
            yield return StartFromEventList("e1");
            Assert.AreEqual(RaceSetup.Tier.aiCount + 1, race.Participants.Count);
            Assert.Greater(race.Participants.Count, 1);
            car = race.Player.Car;
            Assert.IsFalse(car.Cannon.locked);
            ammo = car.Cannon.Ammo;
            command = car.gameObject.AddComponent<ScriptedDriverInput>();
            command.fire = true; car.SetInputSource(command);
            for (int i = 0; i < 30 && car.Cannon.Ammo == ammo; i++) yield return null;
            Assert.Less(car.Cannon.Ammo, ammo);
            Assert.Greater(RaceDirector.Instance.ShotsFired, 0);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator LiveSectorLabelsMatchEveryLapAndTheResultsOnBothDirections()
        {
            yield return OpenProfile();
            PlatformGlue.Save.Data.progress.unlockedEvents.Add(e2.eventId);
            foreach (var ev in new[] { e1, e2 })
            {
                bool reverse = ev.reversed;
                ev.aiCountOverride = 1; // keep a rival behind to inspect the finish HUD
                yield return Start(ev);
                int hash = flow.GetComponent<GhostRecorder>().Record.TrackHash;
                var path = TrackPath.Instance;
                var reference = LegacyCircuit(hash, path, ev.laps);
                string payload = reverse ? Retimed(reference, "Faster", .003) : reference.ToBase64();
                flow.EnterMenu();
                PlatformGlue.Save.Data.personalGhosts.KeepBest(ev.eventId, ev.laps, payload, 1);
                yield return Start(ev);
                foreach (var participant in race.Participants)
                    if (!participant.isPlayer)
                    {
                        participant.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                        var gunner = participant.GetComponent<AiGunner>();
                        if (gunner != null) gunner.enabled = false;
                    }
                var label = Array.Find(Object.FindAnyObjectByType<HudScreen>().GetComponentsInChildren<TMPro.TMP_Text>(true),
                    t => t.name == "SectorDelta");
                Assert.IsFalse(label.gameObject.activeInHierarchy);
                var shown = new List<string>();
                Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
                while (race.State == RaceState.Racing && race.RaceTime < 180)
                {
                    yield return null;
                    if (label.gameObject.activeInHierarchy && (shown.Count == 0 || shown[shown.Count - 1] != label.text))
                        shown.Add(label.text);
                }
                Assert.AreEqual(RaceState.Finishing, race.State);
                var baseline = GhostSectorTimes.MeasureLaps(path, GhostRecord.FromBase64(payload), ev.laps);
                var actual = GhostSectorTimes.MeasureLaps(path, flow.GetComponent<GhostRecorder>().Record, ev.laps);
                Assert.NotNull(actual);
                Assert.AreEqual(3 * ev.laps, shown.Count, ev.eventId + " " + string.Join(" / ", shown));
                var totals = new int[3];
                for (int i = 0; i < shown.Count; i++)
                {
                    int delta = actual.Duration(i) - baseline.Duration(i);
                    totals[i % 3] += delta;
                    Assert.AreEqual(reverse ? 1 : -1, Math.Sign(delta));
                    int place = reverse ? 2 - i % 3 : i % 3;
                    string sector = Loc.Get(place == 0 ? "results.sector.bridge" : place == 1 ? "results.sector.tower" : "results.sector.pits");
                    string digits = (Mathf.Abs(Mathf.RoundToInt(delta / 100f)) / 10m).ToString("0.0", CultureInfo.GetCultureInfo(Loc.Language));
                    string signed = (delta < 0 ? "−" : "+") + digits;
                    Assert.AreEqual(sector + "  " + Loc.Format("results.sector.delta", "t", signed), shown[i]);
                }
                CollectionAssert.AreEqual(totals, flow.LastSectorDeltaMs);
                TestContext.WriteLine(ev.eventId + " " + string.Join(" / ", shown) + " → " + string.Join(", ", totals) + " ms");
                flow.EnterMenu();
            }
        }
    }
}
