using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using RacingGame.Core.Platform;
using RacingGame.Platform;
using RacingGame.Platform.Mock;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class BoardSelectionPlayTests
    {
        private sealed class Boards : StandalonePlatform, ILeaderboards
        {
            public override ILeaderboards Leaderboards => this;
            public readonly List<string> Requests = new List<string>();
            public TaskCompletionSource<Result<IReadOnlyList<LeaderboardEntry>>> Delayed;
            public Task<Result> SubmitAsync(string board, long score, string extraData = null) => Task.FromResult(Result.Success);
            public Task<Result<IReadOnlyList<LeaderboardEntry>>> GetAsync(string board, int top = 10, bool includeUser = true, int aroundUser = 3)
            {
                Requests.Add(board);
                if (board == "best_e1" && Delayed != null) return Delayed.Task;
                var values = board == "best_e1" ? new[] { new LeaderboardEntry(1, "driver", "Turner", 12450, null) }
                    : board == Core.Sim.ArenaScore.Board ? new[] { new LeaderboardEntry(1, "ace", "Ace", 314, null) }
                    : Array.Empty<LeaderboardEntry>();
                return Task.FromResult(Result<IReadOnlyList<LeaderboardEntry>>.Success(values));
            }
        }

        private static void Click(string name) => GameObject.Find(name).GetComponent<Button>().onClick.Invoke();
        private static string Text(string group, string label) => GameObject.Find(group).transform.Find(label).GetComponent<TMP_Text>().text;

        [UnityTest, Timeout(90000)]
        public IEnumerator BoardPickerRequestsTheSelectedEventAndIgnoresThePreviousResponse()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            while (!PlatformGlue.IsReady) yield return null;
            yield return null; yield return null;
            var original = PlatformRuntime.Current;
            var boards = new Boards(); var ready = boards.InitializeAsync(); while (!ready.IsCompleted) yield return null;
            PlatformRuntime.Install(boards);
            try
            {
                var flow = UnityEngine.Object.FindAnyObjectByType<GameFlow>();
                var menu = flow.GetComponent<MenuScreens>();
                menu.Show("leaders");
                Click("ChooseBoardEvent"); Click("BoardEvent_e1");
                Assert.AreEqual("Turner", Text("Cloud1", "Name"));
                Assert.AreEqual("best_e1", boards.Requests[boards.Requests.Count - 1]);
                StringAssert.Contains(Loc.Get(Array.Find(flow.events, e => e.eventId == "e1").nameKey), Text("CloudTitle", "Title"));
                boards.Delayed = new TaskCompletionSource<Result<IReadOnlyList<LeaderboardEntry>>>();
                Click("ChooseBoardEvent"); Click("BoardEvent_e1");
                StringAssert.Contains(Loc.Get("common.loading"), Text("CloudStatus", "Status"));
                Click("ChooseBoardEvent"); Click("BoardEvent_tt1");
                Assert.AreEqual("best_tt1", boards.Requests[boards.Requests.Count - 1]);
                Assert.AreEqual(Loc.Get("lb.empty"), Text("CloudStatus", "Status"));
                StringAssert.Contains(Loc.Get("events.tt1.name"), Text("CloudTitle", "Title"));
                boards.Delayed.SetResult(Result<IReadOnlyList<LeaderboardEntry>>.Success(new[] { new LeaderboardEntry(1, "late", "Old result", 9000, null) }));
                yield return null; yield return null;
                Assert.IsNull(GameObject.Find("Cloud1"));
                Assert.AreEqual(Loc.Get("lb.empty"), Text("CloudStatus", "Status"));
                Click("ChooseBoardEvent"); Click("BoardEvent_a1");
                Assert.AreEqual(Core.Sim.ArenaScore.Board, boards.Requests[boards.Requests.Count - 1]);
                Assert.AreEqual(Loc.Format("arena.points", "n", 314), Text("Cloud1", "Time"));
                TestContext.WriteLine(string.Join(" → ", boards.Requests) + " / " + Text("Cloud1", "Time"));
            }
            finally { PlatformRuntime.Install(original); }
        }
    }
}
