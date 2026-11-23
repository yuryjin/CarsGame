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
