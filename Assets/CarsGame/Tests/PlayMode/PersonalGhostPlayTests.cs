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

        private sealed class RivalSessions : RacingGame.Platform.Mock.StandalonePlatform, IAsyncSessions
        {
            public override IAsyncSessions Sessions => this;
            public List<string> Candidates = new List<string>();
            public Task<Result<IReadOnlyList<string>>> LoadOpponentsAsync(int count, string meta) =>
                Task.FromResult(Result<IReadOnlyList<string>>.Success(Candidates));
            public Task<Result> PushAsync(string meta) => Task.FromResult(Result.Success);
            public void Commit(string payload) { }
        }

        [Serializable] private class Candidate { public string ghost, name; public int laps; }

        [UnityTest, Timeout(120000)]
        public IEnumerator ClosestRivalIsExplainedInRecordsAndSpawnsOnlyOnTheMatchingRoute()
        {
            yield return OpenProfile();
            yield return Start(e1);
            int hash = flow.GetComponent<GhostRecorder>().Record.TrackHash;
            var source = LegacyCircuit(hash, TrackPath.Instance, e1.laps);
            var saved = PlatformGlue.Save.Data;
            saved.personalGhosts.KeepBest("e1", e1.laps, Retimed(source, "Personal", .25), 1);
            saved.progress.bests.Add(new EventBest { eventId = "e1", bestTimeMs = 100000,
                setup = new RecordSetup { captured = true, body = "classic", tier = "easy", trackHash = hash, laps = e1.laps } });
            flow.EnterMenu();
            var menu = flow.GetComponent<MenuScreens>();
            var platform = PlatformRuntime.Current; var sessions = new RivalSessions();
            var initialized = sessions.InitializeAsync(); while (!initialized.IsCompleted) yield return null;
            PlatformRuntime.Install(sessions);
            try
            {
                string Payload(string name, double scale, int laps) => JsonUtility.ToJson(new Candidate { name = name, laps = laps, ghost = Retimed(source, name, scale) });
                sessions.Candidates.Add(Payload("Slow", .3, e1.laps));
                sessions.Candidates.Add(Payload("Closest", .255, 0)); // A real V2 recording with no lap metadata.
                sessions.Candidates.Add(Payload("Wrong laps", .25, e1.laps + 1));
                source.TrackHash++;
                sessions.Candidates.Add(Payload("Wrong track", .25, e1.laps));
                source.TrackHash--;
                menu.Show("leaders");
                GameObject.Find("FindRival").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return null;
                string title = GameObject.Find("RivalStatus").GetComponent<TMPro.TMP_Text>().text;
                StringAssert.Contains(UiFactory.FormatMilliseconds(102000), title);
                StringAssert.Contains("+" + UiFactory.FormatMilliseconds(2000), title);
                GameObject.Find("StartMatchedRace").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.AreEqual(FlowState.Racing, flow.State);
                var visible = Object.FindObjectsByType<GhostCar>(FindObjectsSortMode.None);
                Assert.AreEqual(2, visible.Length);
                Assert.IsTrue(Array.Exists(visible, g => g.Record.PlayerName == "Closest"));
                flow.EnterMenu(); yield return null;
                sessions.Candidates.Clear();
                menu.Show("leaders");
                GameObject.Find("FindRival").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); yield return null;
                Assert.AreEqual(Loc.Get("rival.empty"), GameObject.Find("RivalStatus").GetComponent<TMPro.TMP_Text>().text);
                GameObject.Find("StartMatchedRace").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.AreEqual(FlowState.Racing, flow.State);
                Assert.AreEqual("Personal", Ghost().Record.PlayerName);
                TestContext.WriteLine(title + "; empty match still started with the personal recording");
            }
            finally { PlatformRuntime.Install(platform); }
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
                StringAssert.StartsWith(UiFactory.FormatMilliseconds(Mathf.RoundToInt(moment * 1000f)),
                    GameObject.Find("ReplayTime").GetComponent<TMPro.TMP_Text>().text);
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

        private object StartingCar()
        {
            var player = race.Player;
            var cannon = player.GetComponent<CarCannon>();
            return new
            {
                body = player.GetComponent<CarBody>().BodyId, tier = race.tier.tierId, bots = race.Participants.Count - 1,
                player.Car.maxSpeed, player.Car.motorTorque, player.Car.grip, player.Health.maxHealth,
                cannon.shellDamage, cannon.fireInterval, cannon.AmmoMax,
                player.Weapons.LoadoutFirst, player.Weapons.LoadoutSecond, player.Weapons.secondaryType,
                player.Weapons.SecondaryAmmoMax, player.Weapons.CompactLoadout, seed = RaceDirector.Current.raceSeed
            };
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator DailyStartingCarsMatchAcrossTwoGaragesAndCareerRestoresEachGarage()
        {
            yield return OpenProfile();
            var clock = DailyRace.UnixMsSource;
            try
            {
                int day = 20000;
                while (Core.Daily.DailyChallenge.For(day).Twist != Core.Daily.RaceTwist.Sprint) day++;
                DailyRace.UnixMsSource = () => Core.Daily.DailyChallenge.DayStartUnixMs(day) + 30000;
                var menu = Object.FindAnyObjectByType<MenuScreens>();
                object dailyCar = null;
                foreach (bool rich in new[] { false, true })
                {
                    var profile = new SaveData { updatedAtUnixMs = DailyRace.NowUnixMs };
                    var garage = profile.progress;
                    profile.Local("ghost-local").races = 3;
                    if (rich)
                    {
                        garage.cosmetics.Add("body:flea"); garage.body = "flea";
                        garage.upgrades = new[] { 2, 3, 2 }; garage.weapons = new[] { 2, 2, 2, 2 };
                        garage.buildPreset = 2; garage.equippedPrimary = 1; garage.equippedSecondary = 1; garage.fullArsenal = true;
                        RewardedOffers.GrantTrial(Core.Economy.UpgradeKind.Engine);
                    }
                    store.Write(SaveMigrator.ToJson(profile)); yield return Reload();
                    Assert.AreEqual(garage.body, PlatformGlue.Save.Data.progress.body);
                    menu.Show("events");
                    int index = Array.FindIndex(flow.tiers, t => t.tierId == (rich ? "hard" : "easy"));
                    GameObject.Find("Tier" + index).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    menu.Show("main");
                    GameObject.Find("PlayDaily").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.AreEqual(FlowState.Racing, flow.State);
                    Assert.AreEqual("normal", race.tier.tierId);
                    Assert.AreEqual("classic", race.Player.GetComponent<CarBody>().BodyId);
                    Assert.AreEqual(rich ? "hard" : "easy", PlatformGlue.Save.Data.settings.tier);
                    if (dailyCar == null) dailyCar = StartingCar(); else Assert.AreEqual(dailyCar, StartingCar());
                    flow.RetryRace();
                    Assert.AreEqual(dailyCar, StartingCar());
                    if (rich) Assert.AreEqual((int)Core.Economy.UpgradeKind.Engine, RewardedOffers.TrialUpgrade);

                    flow.EnterMenu();
                    Assert.AreEqual(rich ? "hard" : "easy", RaceSetup.Tier.tierId);
                    yield return StartFromEventList("e1");
                    Assert.AreEqual(rich ? "hard" : "easy", race.tier.tierId);
                    Assert.AreEqual(rich ? "flea" : "classic", race.Player.GetComponent<CarBody>().BodyId);
                    Assert.AreEqual(rich ? PrimaryWeaponType.MachineGun : PrimaryWeaponType.Cannon, race.Player.Weapons.LoadoutFirst);
                    Assert.AreEqual(!rich, race.Player.Weapons.CompactLoadout);
                    if (rich) Assert.AreEqual(-1, RewardedOffers.TrialUpgrade);
                    TestContext.WriteLine("career " + StartingCar());
                    flow.EnterMenu();
                }
                TestContext.WriteLine("daily and both retries " + dailyCar);
            }
            finally { DailyRace.UnixMsSource = clock; RewardedOffers.ConsumeTrial(null); RewardedOffers.ClearLastRaceTrial(); }
        }

        private object DailyCourse()
        {
            var ev = flow.CurrentEvent;
            return new
            {
                day = DailyRace.DayOf(ev), ev.twist, ev.laps, ev.reversed, ev.night, ev.sky, ev.damageMultiplier, ev.pickupCount,
                actualLaps = race.laps, actualReverse = TrackPath.Instance.reversed, seed = RaceDirector.Current.raceSeed
            };
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator YesterdayPracticeRestoresTheExactCourseAndFinishesOutsideTheDailyStandings()
        {
            yield return OpenProfile();
            int day = 20000;
            while (Core.Daily.DailyChallenge.For(day).Twist != Core.Daily.RaceTwist.Sprint) day++;
            var clock = DailyRace.UnixMsSource;
            var mock = (RacingGame.Platform.Mock.EditorMockPlatform)PlatformGlue.Platform;
            bool hadScore = mock.Boards.TryGetValue(DailyRace.BoardName, out long oldScore);
            try
            {
                long now = Core.Daily.DailyChallenge.DayStartUnixMs(day + 1) - 15000;
                DailyRace.UnixMsSource = () => now;
                var menu = Object.FindAnyObjectByType<MenuScreens>();
                var data = PlatformGlue.Save.Data;
                PlatformGlue.Save.Local.races = 3;
                data.progress.daily.finishDays.Add(day);
                data.progress.daily.claims.Add(new DailyClaim { day = day, coins = 150 });
                long todayScore = Core.Daily.DailyChallenge.EncodeScore(day + 1, 100000);
                mock.Boards[DailyRace.BoardName] = todayScore;

                menu.Show("main");
                GameObject.Find("PlayDaily").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                var prior = DailyCourse();
                Assert.AreEqual(day, DailyRace.DayOf(flow.CurrentEvent));
                flow.EnterMenu();
                now += 30000;
                menu.Show("practice");
                long coins = data.Coins; int races = data.Races;
                GameObject.Find("PracticeYesterday").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.AreEqual(prior, DailyCourse());
                while (race.State == RaceState.Countdown) yield return null;
                Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
                float timeout = Time.realtimeSinceStartup + 120f;
                while (flow.State == FlowState.Racing && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.AreEqual(FlowState.Menu, flow.State);
                Assert.AreEqual("practice", menu.Current);
                Assert.AreEqual(day, flow.LastYesterdayDay);
                Assert.Greater(flow.LastYesterdayTimeMs, 0);
                var summary = GameObject.Find("YesterdayPractice").transform.Find("Instruction").GetComponent<TMPro.TMP_Text>();
                StringAssert.Contains(UiFactory.FormatMilliseconds(flow.LastYesterdayTimeMs), summary.text);
                CollectionAssert.AreEqual(new[] { day }, data.progress.daily.finishDays);
                Assert.AreEqual(1, data.progress.daily.claims.Count);
                Assert.AreEqual(races, data.Races); Assert.AreEqual(coins, data.Coins);
                Assert.AreEqual(0, data.progress.bests.Count);
                Assert.AreEqual(todayScore, mock.Boards[DailyRace.BoardName]);

                menu.Show("main");
                GameObject.Find("PlayDaily").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                var today = Core.Daily.DailyChallenge.For(day + 1);
                Assert.AreEqual(day + 1, DailyRace.DayOf(flow.CurrentEvent));
                Assert.AreEqual(today.Seed, RaceDirector.Current.raceSeed);
                Assert.AreEqual(today.Twist, flow.CurrentEvent.twist);
                Assert.AreEqual(today.Reversed, TrackPath.Instance.reversed);
                Assert.AreEqual(today.Laps, race.laps);
                TestContext.WriteLine($"Yesterday {prior}, unranked finish {flow.LastYesterdayTimeMs} ms; today {DailyCourse()}; board {todayScore}, coins {coins}, races {races}");
            }
            finally
            {
                DailyRace.UnixMsSource = clock;
                if (hadScore) mock.Boards[DailyRace.BoardName] = oldScore;
                else mock.Boards.Remove(DailyRace.BoardName);
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator FriendCodeTransfersTheActualSetupAndRejectsDamagedNewerOrUnavailableChallenges()
        {
            yield return OpenProfile();
            var menu = flow.GetComponent<MenuScreens>();
            var first = PlatformGlue.Save.Data.progress;
            first.cosmetics.Add("body:flea"); first.body = "flea"; first.upgrades[0] = 1;
            PlatformGlue.Save.MarkDirty();
            yield return StartFromEventList("tt1");
            yield return DriveToFinish();
            var originalBest = first.bests.Find(b => b.eventId == "tt1");
            var originalSetup = originalBest.setup.Copy();
            Assert.AreEqual("flea", originalSetup.body);
            menu.GoTo("leaders");
            GameObject.Find("CreateChallenge").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            string code = GameObject.Find("ChallengeCode").GetComponent<TMPro.TMP_InputField>().text;
            Assert.IsTrue(code.StartsWith("CG1-")); Assert.Less(code.Length, 120);
            var second = new SaveData { updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            second.progress.upgrades[0] = 3; second.settings.tier = "hard";
            store.Write(SaveMigrator.ToJson(second)); yield return Reload();
            menu.Show("leaders");
            var input = GameObject.Find("ChallengeCode").GetComponent<TMPro.TMP_InputField>();
            var enter = GameObject.Find("StartChallenge").GetComponent<UnityEngine.UI.Button>();
            string Status() => GameObject.Find("ChallengeStatus").GetComponent<TMPro.TMP_Text>().text;
            foreach (var rejected in new[] { (code.Substring(0, 10), "challenge.invalid"),
                ("CG2-" + code.Substring(4), "challenge.version"), (code, "challenge.unavailable") })
            {
                input.text = rejected.Item1; enter.onClick.Invoke();
                Assert.AreEqual(FlowState.Menu, flow.State);
                Assert.AreEqual(Loc.Get(rejected.Item2), Status());
            }
            var garage = PlatformGlue.Save.Data.progress;
            garage.cosmetics.Add("body:flea");
            input.text = code; enter.onClick.Invoke();
            while (race.State == RaceState.Countdown) yield return null;
            Assert.AreEqual(FlowState.Racing, flow.State);
            Assert.AreEqual(originalBest.eventId, flow.CurrentEvent.eventId);
            Assert.AreEqual(originalBest.bestTimeMs, flow.FriendTargetMs);
            Assert.AreEqual(originalSetup.body, race.Player.GetComponent<CarBody>().BodyId);
            Assert.AreEqual(originalSetup.tier, race.tier.tierId);
            yield return DriveToFinish();
            Assert.IsTrue(RecordSetup.Same(originalSetup, garage.recentAttempts[0].setup));
            flow.EnterMenu();
            Assert.AreEqual(0, flow.FriendTargetMs);
            Assert.AreEqual("", garage.body); Assert.AreEqual(3, garage.upgrades[0]); Assert.AreEqual("hard", PlatformGlue.Save.Data.settings.tier);
            TestContext.WriteLine($"Transferred {code.Length} characters: {originalBest.eventId}, {originalSetup.body}, {originalSetup.tier}, {originalBest.bestTimeMs} ms");
        }

        [UnityTest, Timeout(160000)]
        public IEnumerator ThreeDisciplinesGrantOneWeeklyCosmeticAndANewWeekStartsEmpty()
        {
            yield return OpenProfile();
            var clock = DailyRace.UnixMsSource;
            var arena = Object.Instantiate(Array.Find(flow.events, ev => ev.eventId == "a1"));
            arena.matchDuration = 2f; arena.aiCountOverride = 0;
            try
            {
                int day = 20000;
                while ((WeeklyGoals.Week(Core.Daily.DailyChallenge.DayStartUnixMs(day)) & 1) == 0) day++;
                long now = Core.Daily.DailyChallenge.DayStartUnixMs(day) + 60000;
                DailyRace.UnixMsSource = () => now;
                int week = WeeklyGoals.Week(now);
                var progress = PlatformGlue.Save.Data.progress;
                string prize = WeeklyGoals.Reward(week);
                yield return StartFromEventList("tt1"); yield return DriveToFinish(); flow.EnterMenu();
                Assert.AreEqual(1, WeeklyGoals.Mask(progress, week));
                yield return Start(arena);
                while (flow.State == FlowState.Racing) yield return null;
                Assert.AreEqual(3, WeeklyGoals.Mask(progress, week));
                flow.EnterMenu();
                var missionEvent = Array.Find(flow.events, ev => ev.eventId == "m2");
                yield return Start(missionEvent);
                race.EndMission(true); // The train director calls this same finish endpoint on capture.
                Assert.AreEqual(7, WeeklyGoals.Mask(progress, week));
                Assert.AreEqual(1, progress.cosmetics.FindAll(c => c == prize).Count);
                flow.EnterMenu(); yield return Start(missionEvent); race.EndMission(true);
                Assert.AreEqual(1, progress.cosmetics.FindAll(c => c == prize).Count);
                flow.EnterMenu();
                var flush = PlatformGlue.Save.FlushAsync(); while (!flush.IsCompleted) yield return null;
                flush.GetAwaiter().GetResult(); yield return Reload();
                now += 7 * 86400000L;
                flow.GetComponent<MenuScreens>().Show("main");
                var next = PlatformGlue.Save.Data.progress;
                Assert.AreEqual(0, WeeklyGoals.Mask(next, week + 1)); Assert.AreEqual(7, WeeklyGoals.Mask(next, week));
                Assert.AreEqual(1, next.cosmetics.FindAll(c => c == prize).Count);
                for (int i = 0; i < 3; i++)
                    StringAssert.StartsWith("–", GameObject.Find("Weekly" + i).GetComponent<TMPro.TMP_Text>().text);
                TestContext.WriteLine($"week {week}: 1 → 3 → 7, persisted {prize} once; week {week + 1}: 0");
            }
            finally { DailyRace.UnixMsSource = clock; Object.DestroyImmediate(arena); }
        }

        [UnityTest, Timeout(220000)]
        public IEnumerator IdenticalRealDriftsEarnOnlyTheSportClassBadgeAndItSurvivesSwitchingBodies()
        {
            yield return OpenProfile();
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            var progress = PlatformGlue.Save.Data.progress;
            progress.cosmetics.Add("body:flea");
            var menu = flow.GetComponent<MenuScreens>();
            e1.laps = 1;
            foreach (string id in new[] { "flea", "classic", "classic", "classic" })
            {
                menu.Show("garage");
                Assert.IsTrue(menu.SelectBody(id));
                yield return Start(e1);
                var driver = race.Player;
                var car = driver.Car;
                Assert.AreEqual(id, driver.GetComponent<CarBody>().BodyId);
                var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                car.SetInputSource(input);
                var path = TrackPath.Instance;
                path.Pose(path.startLineDistance + 20f, 0, out var start, out var facing);
                car.Teleport(start + Vector3.up * .45f, facing);
                for (int i = 0; i < 50; i++) yield return null;
                Assert.IsTrue(car.IsGrounded, id);
                // Standardise entry speed and road position across the two bodies. Physics supplies the slide and release.
                car.Body.linearVelocity = car.transform.forward * 25f;
                input.Set(1f, .78f, true);
                for (int i = 0; i < 96 && car.DriftStage < 1; i++) yield return null;
                Assert.GreaterOrEqual(car.DriftStage, 1, id + " did not charge: grounded=" + car.IsGrounded + ", speed=" + car.Body.linearVelocity.magnitude);
                input.Set(1f, 0f);
                for (int i = 0; i < 4; i++) yield return null;
                Assert.AreEqual(1, driver.DriftBoosts, id);
                Assert.AreEqual(0, driver.Shortcuts);
                race.QaFinishNow();
                Assert.AreEqual(FlowState.Results, flow.State);
                flow.EnterMenu();
            }
            Assert.AreEqual(3, BodyMastery.Count(progress, Core.Economy.BodyClass.Sport));
            Assert.AreEqual(0, BodyMastery.Count(progress, Core.Economy.BodyClass.Buggy));
            menu.Show("garage"); Assert.IsTrue(menu.SelectBody("flea"));
            var flush = PlatformGlue.Save.FlushAsync(); while (!flush.IsCompleted) yield return null;
            flush.GetAwaiter().GetResult(); yield return Reload();
            menu.Show("garage");
            Assert.AreEqual("flea", PlatformGlue.Save.Data.progress.body);
            string MasteryText() => GameObject.Find("ClassMastery").transform.Find("Achievement").GetComponent<TMPro.TMP_Text>().text;
            GameObject.Find("Tab_Buggy").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            StringAssert.Contains("0/3", MasteryText());
            GameObject.Find("Tab_Sport").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            StringAssert.Contains(Loc.Get("mastery.badge"), MasteryText());
            TestContext.WriteLine("Four physical drift releases: Flea 1, Classic 3. Persisted mastery: Buggy 0, Sport 3.");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator DriverPageKeepsLegacyCupsAndShowsOnlyObservedBodiesAndFinisherGaps()
        {
            yield return OpenProfile();
            var menu = flow.GetComponent<MenuScreens>();
            string Value(string row) => GameObject.Find(row).transform.Find("Value").GetComponent<TMPro.TMP_Text>().text;
            void OpenPage()
            {
                menu.Show("main"); GameObject.Find("driver").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.AreEqual("driver", menu.Current);
            }
            OpenPage();
            foreach (string row in new[] { "DriverBody", "DriverCup", "DriverGap" }) Assert.AreEqual(Loc.Get("driver.empty"), Value(row));
            var old = new SaveData { updatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            var cup = Core.Economy.RaceCups.All[0];
            foreach (string id in cup.Events) old.progress.bests.Add(new EventBest { eventId = id, stars = 3, bestTimeMs = 60000 });
            old.progress.cosmetics.Add("body:flea");
            old.version = 9;
            store.Write(JsonUtility.ToJson(old)); yield return Reload();
            OpenPage();
            Assert.AreEqual(Loc.Get(cup.NameKey), Value("DriverCup"));
            Assert.AreEqual(Loc.Get("driver.empty"), Value("DriverGap"));
            Assert.AreEqual(Loc.Get("driver.empty"), Value("DriverBody"));
            var progress = PlatformGlue.Save.Data.progress;
            progress.body = "flea";
            yield return StartFromEventList("tt1"); yield return DriveToFinish();
            flow.EnterMenu(); OpenPage();
            Assert.AreEqual(Loc.Get("driver.empty"), Value("DriverGap"));
            Assert.AreEqual(Loc.Get("body.flea") + " · 1", Value("DriverBody"));
            progress.body = "classic"; e1.laps = 1; e1.aiCountOverride = 2;
            yield return Start(e1);
            RaceParticipant runner = null;
            foreach (var rival in race.Participants)
            {
                var gun = rival.GetComponent<AiGunner>(); if (gun != null) gun.enabled = false;
                if (rival.isPlayer) continue;
                if (runner == null) runner = rival;
                else rival.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
            }
            float deadline = Time.realtimeSinceStartup + 90f;
            while (!race.HasFinished(runner) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(race.HasFinished(runner));
            Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
            deadline = Time.realtimeSinceStartup + 90f;
            while (flow.State == FlowState.Racing && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(FlowState.Results, flow.State);
            var classified = race.Results.Entries.Find(x => x.Finished && !x.IsPlayer);
            Assert.NotNull(classified); Assert.IsTrue(race.Results.Player.Finished);
            Assert.IsTrue(race.Results.Entries.Exists(x => !x.Finished));
            long delta = Math.Abs((long)Math.Round(race.Results.Player.TotalTime * 1000) - (long)Math.Round(classified.TotalTime * 1000));
            Assert.AreEqual(delta, progress.closestFinish.Difference);
            Assert.AreEqual(classified.Name, progress.closestFinish.rival);
            flow.EnterMenu(); yield return StartFromEventList("tt1"); yield return DriveToFinish(); flow.EnterMenu();
            var flush = PlatformGlue.Save.FlushAsync(); while (!flush.IsCompleted) yield return null;
            flush.GetAwaiter().GetResult(); yield return Reload(); OpenPage();
            StringAssert.Contains(UiFactory.FormatMilliseconds(delta), Value("DriverGap"));
            StringAssert.Contains(UiFactory.Escape(classified.Name), Value("DriverGap"));
            Assert.AreEqual(Loc.Get("body.classic") + " · 2", Value("DriverBody"));
            Assert.AreEqual(Loc.Get(cup.NameKey), Value("DriverCup"));
            TestContext.WriteLine($"Three finishes: Flea 1, Classic 2; closest classified rival {classified.Name}, gap {delta} ms; cup {cup.Id}.");
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator TwoNamedGarageSetsRestoreRealCarsAcrossRestartAndFallbackOnlyForMissingItems()
        {
            yield return OpenProfile();
            var menu = flow.GetComponent<MenuScreens>();
            var p = PlatformGlue.Save.Data.progress;
            p.cosmetics.AddRange(new[] { "body:flea", "paint:snow", "livery:hood" });
            p.weapons[(int)Core.Economy.WeaponKind.MachineGun] = 1;
            p.weapons[(int)Core.Economy.WeaponKind.Mines] = 1;
            p.body = "flea"; p.paint = "snow"; p.livery = "hood"; p.stripe = "rosso";
            p.equippedPrimary = 1; p.reservePrimary = 4; p.equippedSecondary = (int)SecondaryWeaponType.Mines;
            void Click(string key) => GameObject.Find(key).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            void SaveSlot(int index, string name)
            {
                menu.Show("garage");
                GameObject.Find("SetName" + index).GetComponent<TMPro.TMP_InputField>().text = name;
                Click("WriteSet" + index);
            }
            SaveSlot(0, "Ночной дозор");
            p.body = "classic"; p.paint = "rosso"; p.livery = "none"; p.stripe = "";
            p.equippedPrimary = 2; p.reservePrimary = 3; p.equippedSecondary = 0;
            SaveSlot(1, "Day shift");
            var flush = PlatformGlue.Save.FlushAsync(); while (!flush.IsCompleted) yield return null;
            flush.GetAwaiter().GetResult(); yield return Reload();
            p = PlatformGlue.Save.Data.progress;
            for (int i = 0; i < 3; i++)
            {
                int slot = i == 1 ? 1 : 0;
                if (i == 2) p.weapons[(int)Core.Economy.WeaponKind.MachineGun] = 0;
                menu.Show("garage");
                Assert.AreEqual(slot == 0 ? "Ночной дозор" : "Day shift", GameObject.Find("SetName" + slot).GetComponent<TMPro.TMP_InputField>().text);
                Click("UseSet" + slot);
                yield return Start(e1);
                var driver = race.Player;
                Assert.AreEqual(slot == 0 ? "flea" : "classic", driver.GetComponent<CarBody>().BodyId);
                Assert.AreEqual(PlayerCosmetics.PaintColor(slot == 0 ? "snow" : "rosso"), driver.GetComponent<CarPaint>().color);
                var livery = driver.GetComponent<CarLivery>();
                Assert.AreEqual(slot == 0 ? Core.Economy.LiveryPattern.Hood : Core.Economy.LiveryPattern.None, livery != null ? livery.Pattern : Core.Economy.LiveryPattern.None);
                Assert.AreEqual(i == 2 ? 0 : slot == 0 ? 1 : 2, (int)driver.Weapons.LoadoutFirst);
                Assert.AreEqual(slot == 0 ? 4 : 3, (int)driver.Weapons.LoadoutSecond);
                Assert.AreEqual(slot == 0 ? SecondaryWeaponType.Mines : SecondaryWeaponType.None, driver.Weapons.secondaryType);
                flow.EnterMenu();
            }
            Assert.AreEqual(1, p.garageSets[0].primary, "The named original may be equipped again after regaining a missing item.");
            TestContext.WriteLine("Two named sets survived reload. All selections reached the spawned cars; unavailable MG fell back to cannon with AutoCannon, mines, Flea and Snow preserved.");
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator ArsenalComparesRealUpgradedCannonAndMachineGunShotsOnTheTestDrive()
        {
            yield return OpenProfile();
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            var menu = flow.GetComponent<MenuScreens>();
            var p = PlatformGlue.Save.Data.progress;
            p.weapons[(int)Core.Economy.WeaponKind.MachineGun] = 3; p.upgrades[2] = 2;
            p.buildPreset = (int)Core.Economy.CarBuild.Firepower; p.equippedPrimary = 0; p.reservePrimary = 1;
            menu.Show("garage");
            void Click(string id) => GameObject.Find(id).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Click("ChooseSlot1"); Click("ChooseCompare1"); Click("CompareWeapon1");
            string[] shown = new string[2];
            for (int i = 0; i < 2; i++) shown[i] = GameObject.Find("Comparison" + i).transform.Find("Stats").GetComponent<TMPro.TMP_Text>().text;
            Assert.AreNotEqual(shown[0], shown[1]);
            Click("TryLoadout");
            while (race.State == RaceState.Countdown) yield return null;
            var driver = race.Player; var cannon = driver.Car.Cannon; var weapons = driver.Weapons;
            string Num(float x) => x.ToString("0.##", CultureInfo.InvariantCulture);
            for (int i = 0; i < 2; i++)
            {
                var type = (PrimaryWeaponType)i;
                if (i == 1) Assert.IsTrue(weapons.TrySwitchPrimary(type, SimulationDriver.Instance.Tick));
                weapons.ResetHeat();
                int since = SimulationDriver.Instance.Tick;
                while (!cannon.TryFire()) { Assert.Less(SimulationDriver.Instance.Tick - since, 200); yield return null; }
                int first = SimulationDriver.Instance.Tick;
                float injectedHeat = weapons.CurrentHeat;
                yield return null;
                var projectile = Array.Find(Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None), x => x.IsLive && x.Owner == driver.gameObject);
                Assert.NotNull(projectile);
                float damage = projectile.damage;
                while (!cannon.TryFire()) yield return null;
                float interval = (SimulationDriver.Instance.Tick - first) * SimulationDriver.FixedStep;
                StringAssert.Contains(Loc.Format("loadout.stats", "damage", Num(damage), "interval", Num(interval)), shown[i]);
                float hot = weapons.CurrentHeat;
                int before = SimulationDriver.Instance.Tick;
                yield return null;
                float cooling = (hot - weapons.CurrentHeat) / ((SimulationDriver.Instance.Tick - before) * SimulationDriver.FixedStep);
                StringAssert.Contains(Loc.Format("arsenal.thermal", "shot", Num(injectedHeat), "cool", Num(cooling)), shown[i]);
                if (i == 0)
                {
                    int count = 2;
                    while (!cannon.IsReloading) { if (cannon.TryFire()) count++; else yield return null; }
                    int last = SimulationDriver.Instance.Tick;
                    while (cannon.IsReloading) yield return null;
                    float reload = (SimulationDriver.Instance.Tick - last) * SimulationDriver.FixedStep;
                    StringAssert.Contains(Loc.Format("loadout.magazine", "n", count, "seconds", Num(reload)), shown[0]);
                }
                TestContext.WriteLine($"{type}: projectile damage {damage}, interval {interval}s, heat {injectedHeat}, cooling {cooling}/s");
            }
            flow.EnterMenu();
            Assert.AreEqual(0, PlatformGlue.Save.Data.Races); Assert.AreEqual(0, PlatformGlue.Save.Data.Coins);
        }

        [UnityTest, Timeout(200000)]
        public IEnumerator GarageDrillsRepeatOnPhysicalHitsAndRestoreTheOriginalEquipmentAfterLeavingEarly()
        {
            yield return OpenProfile();
            var menu = flow.GetComponent<MenuScreens>();
            var p = PlatformGlue.Save.Data.progress; p.equippedPrimary = (int)PrimaryWeaponType.PlasmaCutter; p.reservePrimary = 2;
            int wallet = (int)PlatformGlue.Save.Data.Coins;
            void Click(string name) => GameObject.Find(name).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            foreach (var lesson in new[] { PracticeLesson.CannonTarget, PracticeLesson.BeamShield, PracticeLesson.PursuitMine })
            {
                menu.Show("garage"); Click("StartDrill" + lesson);
                for (int turn = 0; turn < 2; turn++)
                {
                    while (race.State == RaceState.Countdown) yield return null;
                    Assert.AreEqual(lesson, flow.CurrentPractice); Assert.IsFalse(flow.PracticeCompleted);
                    var target = Array.Find(Object.FindObjectsByType<RaceParticipant>(FindObjectsSortMode.None), x => !x.isPlayer);
                    var driver = race.Player; var start = target.Car.Body.position;
                    if (lesson == PracticeLesson.BeamShield)
                    {
                        Assert.IsTrue(target.Status.HasShield);
                        Assert.AreEqual(PrimaryWeaponType.PlasmaCutter, driver.Weapons.PrimaryType);
                        float hp = target.Health.Health;
                        Assert.IsTrue(driver.Car.Cannon.TryFire());
                        Assert.IsFalse(target.Status.HasShield); Assert.AreEqual(hp, target.Health.Health);
                    }
                    else if (lesson == PracticeLesson.CannonTarget)
                    {
                        while (Vector3.Distance(target.Car.Body.position, start) < .65f && race.RaceTime < 4) yield return null;
                        Assert.Greater(Vector3.Distance(target.Car.Body.position, start), .5f);
                        for (int i = 0; i < 180 && !flow.PracticeCompleted; i++)
                        {
                            driver.Car.Cannon.TryFire(); yield return null;
                        }
                    }
                    else
                    {
                        Assert.Less(Vector3.Dot(target.Car.Body.position - driver.Car.Body.position, driver.transform.forward), 0f);
                        Assert.IsTrue(driver.Weapons.TryFireSecondary(SimulationDriver.Instance.Tick));
                        var accelerate = driver.Car.gameObject.AddComponent<ScriptedDriverInput>();
                        driver.Car.SetInputSource(accelerate); accelerate.Set(1f, 0f);
                        for (int i = 0; i < 220 && !flow.PracticeCompleted; i++) yield return null;
                    }
                    Assert.IsTrue(flow.PracticeCompleted, lesson + " " + turn);
                    Click("RepeatDrill");
                }
                while (race.State == RaceState.Countdown) yield return null;
                Assert.IsFalse(flow.PracticeCompleted);
                Click("ReturnToGarage");
                Assert.AreEqual("garage", menu.Current);
                Assert.AreEqual(wallet, PlatformGlue.Save.Data.Coins); Assert.AreEqual(0, PlatformGlue.Save.Data.Races);
                Assert.AreEqual((int)PrimaryWeaponType.PlasmaCutter, p.equippedPrimary); Assert.AreEqual(2, p.reservePrimary);
                Assert.AreEqual(0, p.equippedSecondary);
            }
            TestContext.WriteLine("Three physical exercises completed twice, then exited early on the third run each. Garage equipment, wallet and career counters preserved.");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator FourPinnedGunsSurviveReloadAndOneEquipsOnlyTheReserveSlot()
        {
            yield return OpenProfile();
            var menu = flow.GetComponent<MenuScreens>();
            var save = PlatformGlue.Save;
            var owned = (int[])save.Data.progress.weapons.Clone();
            long coins = save.Data.Coins;
            void Click(string name) => GameObject.Find(name).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            foreach (int slot in new[] { 0, 1 })
            {
                menu.Show("garage"); Click("ChooseSlot" + slot);
                Click("PinWeapon" + (slot == 0 ? 1 : 4)); Click("PinWeapon" + (slot == 0 ? 3 : 5));
            }
            var flush = save.FlushAsync(); while (!flush.IsCompleted) yield return null;
            flush.GetAwaiter().GetResult(); yield return Reload();
            for (int slot = 0; slot < 2; slot++)
            {
                menu.Show("garage"); Click("ChooseSlot" + slot);
                Assert.NotNull(GameObject.Find("Arsenal_" + (slot == 0 ? 1 : 4)));
                Assert.NotNull(GameObject.Find("Arsenal_" + (slot == 0 ? 3 : 5)));
                Assert.IsNull(GameObject.Find("Arsenal_0"));
                if (slot == 0)
                    Assert.IsFalse(GameObject.Find("EquipSlot_1").GetComponent<UnityEngine.UI.Button>().interactable);
            }
            save = PlatformGlue.Save;
            Assert.AreEqual(0, save.Data.progress.equippedPrimary); Assert.AreEqual(2, save.Data.progress.reservePrimary);
            Assert.AreEqual(coins, save.Data.Coins); CollectionAssert.AreEqual(owned, save.Data.progress.weapons);
            Click("EquipSlot_5");
            Assert.AreEqual(5, save.Data.progress.reservePrimary); Assert.AreEqual(0, save.Data.progress.equippedPrimary);
        }

        [UnityTest, Timeout(220000)]
        public IEnumerator BodySavingsTracksTheActualFinishWalletUntilBuyingOrReceivingTheBody()
        {
            yield return OpenProfile();
            var menu = flow.GetComponent<MenuScreens>();
            var body = Core.Economy.CarBodies.Get("dune");
            void Click(string name) => GameObject.Find(name).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            menu.Show("garage"); menu.PreviewBody(body.Id); Click("SaveForBody");
            var flush = PlatformGlue.Save.FlushAsync(); while (!flush.IsCompleted) yield return null;
            flush.GetAwaiter().GetResult(); yield return Reload();
            var save = PlatformGlue.Save;
            Assert.AreEqual(body.Id, save.Data.progress.bodyGoal);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                yield return StartFromEventList("tt1"); yield return DriveToFinish();
                Assert.AreEqual(FlowState.Results, flow.State);
                long left = Math.Max(0, body.Cost - save.Data.Coins);
                string actual = GameObject.Find("BodySavings").transform.Find("Progress").GetComponent<TMPro.TMP_Text>().text;
                Assert.AreEqual(left == 0 ? Loc.Format("garage.savings_ready", "name", Loc.Get(body.NameKey))
                    : Loc.Format("garage.savings_left", "name", Loc.Get(body.NameKey), "n", left), actual);
                if (attempt == 0) Assert.Greater(left, 0);
                flow.EnterMenu();
                if (left == 0) break;
            }
            Assert.GreaterOrEqual(save.Data.Coins, body.Cost);
            menu.Show("garage"); menu.PreviewBody(body.Id);
            Assert.IsTrue(menu.BuyBody(body.Id)); Assert.IsEmpty(save.Data.progress.bodyGoal);
            menu.PreviewBody("flea"); Click("SaveForBody");
            save.Data.progress.cosmetics.Add("body:flea"); save.MarkDirty();
            Assert.IsEmpty(save.Data.progress.bodyGoal);
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
