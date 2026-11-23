using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using RacingGame.Core.Ads;
using RacingGame.Core.Platform;
using RacingGame.Core.Save;
using RacingGame.Platform;
using RacingGame.Platform.Mock;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class LeaderboardSubmissionPlayTests
    {
        private sealed class DelayedPlatform : StandalonePlatform, ILeaderboards, IAds
        {
            public override IAds Ads => this;
            public override ILeaderboards Leaderboards => this;
            public bool IsRewardedAvailable => false;
            public int Interstitials, Flushes;
            public readonly List<string> Writes = new List<string>();
            public TaskCompletionSource<Result> NextWrite, NextFlush;
            public string QueuedSave;

            public void SignIn(string id) => Player = new PlayerInfo(id, id, "", true);
            public Task<Result> SubmitAsync(string board, long score, string extraData = null)
            {
                Writes.Add(Player.Id + "/" + board);
                var pending = NextWrite;
                NextWrite = null;
                return pending != null ? pending.Task : Task.FromResult(Result.Success);
            }

            public Task<Result<IReadOnlyList<LeaderboardEntry>>> GetAsync(string board, int top = 10, bool includeUser = true, int aroundUser = 3) =>
                Task.FromResult(Result<IReadOnlyList<LeaderboardEntry>>.Success(Array.Empty<LeaderboardEntry>()));
            public Task<AdResult> ShowInterstitialAsync() { Interstitials++; return Task.FromResult(AdResult.Shown); }
            public Task<Result<bool>> ShowRewardedAsync() => Task.FromResult(Result<bool>.Success(false));
            public override void Queue(string json) { QueuedSave = json; base.Queue(json); }
            public override Task<Result> FlushAsync()
            {
                Flushes++;
                var pending = NextFlush;
                NextFlush = null;
                return pending != null ? pending.Task : Task.FromResult(Result.Success);
            }
        }

        private DelayedPlatform platform;
        private IPlatformServices original;
        private AdPacer originalPacer;
        private string saved;
        private GameFlow flow;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            while (!PlatformGlue.IsReady) yield return null;
            yield return null;
            yield return null;
            flow = UnityEngine.Object.FindAnyObjectByType<GameFlow>();
            original = PlatformRuntime.Current;
            originalPacer = PlatformGlue.Pacer;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            platform = new DelayedPlatform();
            var initialized = platform.InitializeAsync();
            while (!initialized.IsCompleted) yield return null;
            platform.SignIn("first");
            PlatformRuntime.Install(platform);
            var pacer = new AdPacer(Time.realtimeSinceStartup - 180f);
            pacer.OnRaceFinished();
            typeof(PlatformGlue).GetProperty(nameof(PlatformGlue.Pacer)).SetValue(null, pacer);
            var rows = PlatformGlue.Save.Data.progress.bests;
            rows.Clear();
            rows.Add(new EventBest { eventId = "e1", bestTimeMs = 65000 });
            rows.Add(new EventBest { eventId = "tt1", bestTimeMs = 61000 });
        }

        [TearDown]
        public void TearDown()
        {
            if (flow != null) flow.EnterMenu();
            if (original != null) PlatformRuntime.Install(original);
            if (originalPacer != null) typeof(PlatformGlue).GetProperty(nameof(PlatformGlue.Pacer)).SetValue(null, originalPacer);
            if (saved != null) { JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data); PlatformGlue.Save.MarkDirty(); }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator RacingAgainBeforeTheLastScoreReplySkipsThePreviousInterstitial()
        {
            var race = flow.GetComponent<RaceManager>();
            var ev = Array.Find(flow.events, item => item.eventId == "e1");
            var normal = Array.Find(flow.tiers, item => item.tierId == "normal");
            race.countdownSeconds = .2f;
            var pending = platform.NextWrite = new TaskCompletionSource<Result>();
            int previousRaces = PlatformGlue.Save.Data.Races;
            flow.StartRace(ev, normal);
            while (race.State == RaceState.Countdown) yield return null;
            Assert.AreEqual(RaceState.Racing, race.State);
            race.QaFinishNow();
            var deadline = Time.realtimeSinceStartup + 5f;
            while (platform.Writes.Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.Greater(platform.Writes.Count, 0);
            Assert.Greater(platform.Flushes, 0, "progress must already be flushed while the board is pending");
            Assert.AreEqual(previousRaces + 1, JsonUtility.FromJson<SaveData>(platform.QueuedSave).Races,
                "the saved payload includes this finish before the score is confirmed");
            Assert.AreEqual(FlowState.Results, flow.State);
            flow.StartRace(ev, normal);
            Assert.AreEqual(RaceState.Countdown, race.State);
            pending.SetResult(Result.Success);
            for (int frame = 0; frame < 8; frame++) yield return null;
            Assert.AreEqual(0, platform.Interstitials, "the previous results must not interrupt the next race");
            while (race.State == RaceState.Countdown) yield return null;
            Assert.AreEqual(RaceState.Racing, race.State);
            race.QaFinishNow();
            deadline = Time.realtimeSinceStartup + 5f;
            while (platform.Interstitials == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(1, platform.Interstitials, "the current results may still show their eligible ad");
            TestContext.WriteLine($"progress flushes={platform.Flushes}, final interstitials={platform.Interstitials}");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator SwitchingAccountsDuringTheSaveOrScoreReplyLeavesThePreviousFinishWithItsOwner()
        {
            var race = flow.GetComponent<RaceManager>();
            race.countdownSeconds = .2f;
            var ev = Array.Find(flow.events, item => item.eventId == "e1");
            var normal = Array.Find(flow.tiers, item => item.tierId == "normal");
            flow.StartRace(ev, normal);
            while (race.State == RaceState.Countdown) yield return null;
            var flush = platform.NextFlush = new TaskCompletionSource<Result>();
            race.QaFinishNow();
            Assert.Greater(platform.Flushes, 0);
            Assert.IsEmpty(platform.Writes);
            platform.SignIn("second");
            flush.SetResult(Result.Success);
            for (int frame = 0; frame < 8; frame++) yield return null;
            Assert.IsEmpty(platform.Writes, "a finish belonging to the previous identity cannot submit its records after the save returns");
            var batch = PlatformGlue.SubmitAllBestsAsync();
            while (!batch.IsCompleted) yield return null;
            CollectionAssert.Contains(platform.Writes, "second/best_tt1");

            flow.StartRace(ev, normal);
            while (race.State == RaceState.Countdown) yield return null;
            var score = platform.NextWrite = new TaskCompletionSource<Result>();
            race.QaFinishNow();
            Assert.IsNull(platform.NextWrite, "the SDK received the second account's score request");
            platform.SignIn("third");
            score.SetResult(Result.Success);
            for (int frame = 0; frame < 8; frame++) yield return null;
            Assert.AreEqual(FlowState.Results, flow.State);
            Assert.AreEqual(0, platform.Interstitials, "a delayed ad cannot pass from the previous account to the current results");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ChangingRecordsAndAccountsDuringSubmissionKeepsEachBatchWithItsOwner()
        {
            var pending = platform.NextWrite = new TaskCompletionSource<Result>();
            var batch = PlatformGlue.SubmitAllBestsAsync();
            PlatformGlue.Save.Data.progress.bests.Add(new EventBest { eventId = "e2", bestTimeMs = 42000 });
            pending.SetResult(Result.Success);
            while (!batch.IsCompleted) yield return null;
            Assert.IsFalse(batch.IsFaulted, batch.Exception?.ToString());
            CollectionAssert.AreEqual(new[] { "first/best_e1", "first/best_tt1" }, platform.Writes);

            platform.Writes.Clear();
            pending = platform.NextWrite = new TaskCompletionSource<Result>();
            batch = PlatformGlue.SubmitAllBestsAsync();
            platform.SignIn("second");
            pending.SetResult(Result.Success);
            while (!batch.IsCompleted) yield return null;
            CollectionAssert.AreEqual(new[] { "first/best_e1" }, platform.Writes);
            batch = PlatformGlue.SubmitAllBestsAsync();
            while (!batch.IsCompleted) yield return null;
            CollectionAssert.AreEqual(new[] { "first/best_e1", "second/best_e1", "second/best_tt1", "second/best_e2" }, platform.Writes);
            TestContext.WriteLine(string.Join(", ", platform.Writes));
        }
    }
}
