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
    public sealed class PursuitPlayTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator TheFugitiveEscapesOverTheBridgeOrIsCaughtAfterRetry()
        {
            float capture = Time.captureDeltaTime;
            string previous = null;
            GameFlow flow = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                while (!PlatformGlue.IsReady) yield return null;
                yield return null; yield return null;
                previous = JsonUtility.ToJson(PlatformGlue.Save.Data);
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>();
                var debug = Object.FindAnyObjectByType<DebugBus>();
                Object.FindAnyObjectByType<MenuScreens>().Show("events");
                var card = GameObject.Find("pursuit");
                string stats = card.transform.Find("Stats").GetComponent<TMP_Text>().text;
                StringAssert.Contains(Loc.Format("events.laps", "n", 1), stats);
                StringAssert.Contains(Loc.Format("events.rivals", "n", 2), stats);
                StringAssert.Contains(Loc.Get("format.pursuit.rule"), stats);
                card.transform.Find("Start_pursuit").GetComponent<Button>().onClick.Invoke();
                int results = 0;
                race.OnRaceFinished += _ => results++;
                foreach (bool escape in new[] { true, false })
                {
                    while (race.State == RaceState.Countdown) yield return null;
                    Assert.NotNull(Object.FindAnyObjectByType<PursuitRace>());
                    Assert.AreEqual(2, race.Participants.Count(p => !p.isPlayer && p.GetComponent<AiDriver>().enabled));
                    var player = race.Player;
                    float closest = float.MaxValue;
                    void SampleDistance()
                    {
                        foreach (var h in race.Participants)
                            if (!h.isPlayer) closest = Mathf.Min(closest, Vector3.Distance(player.transform.position, h.transform.position));
                    }
                    SampleDistance(); Assert.Greater(closest, PursuitRace.CaptureMetres * 3f);
                    foreach (var hunter in race.Participants)
                        if (!hunter.isPlayer) Assert.Greater(player.Progress.Path.SignedDelta(hunter.Progress.Distance, player.Progress.Distance), 35f);
                    float until = Time.realtimeSinceStartup + 175f;
                    if (escape)
                    {
                        debug.Execute("autopilot", "on");
                        var escapeDriver = player.GetComponent<AiDriver>();
                        escapeDriver.baseLaneOffset = 2.2f;
                        escapeDriver.speedMultiplier = .52f;
                        var fork = player.Progress.Path.Fork;
                        var entrance = fork.Position(0f);
                        while (Vector3.Distance(player.transform.position, entrance) > 6f && race.State == RaceState.Racing && Time.realtimeSinceStartup < until)
                        { SampleDistance(); yield return null; }
                        Assert.LessOrEqual(Vector3.Distance(player.transform.position, entrance), 6f, "Reach the fork on the road");
                        Debug.Log($"[GetawayApproach] speed={player.Car.SpeedKmh:F1}, offset={Vector3.Distance(player.transform.position, entrance):F1}m, nearest={closest:F1}m");
                        var input = player.gameObject.AddComponent<ScriptedDriverInput>();
                        player.Car.SetInputSource(input);
                        yield return TrackForkPlayTests.DriveBranch(race, fork, input, false, true);
                        escapeDriver.speedMultiplier = 1f; escapeDriver.baseLaneOffset = 0f;
                        debug.Execute("autopilot", "on");
                    }
                    else
                    {
                        var brake = player.gameObject.AddComponent<ScriptedDriverInput>();
                        brake.Set(0f, 0f, true); player.Car.SetInputSource(brake);
                    }
                    while (flow.State == FlowState.Racing && Time.realtimeSinceStartup < until) { SampleDistance(); yield return null; }
                    Assert.AreEqual(FlowState.Results, flow.State);
                    Assert.AreEqual(escape, race.Results.Player.Finished);
                    if (escape) { Assert.AreEqual(1, player.Shortcuts); Assert.AreEqual(1, race.Results.Player.Laps); }
                    else { Assert.LessOrEqual(closest, PursuitRace.CaptureMetres); Assert.AreEqual(0, race.Results.Player.Laps); }
                    Assert.AreEqual(1, race.Results.Entries.Count(x => x.Finished));
                    Assert.AreEqual(Loc.Get(escape ? "format.escaped" : "format.caught"), GameObject.Find("Summary").transform.Find("Detail").GetComponent<TMP_Text>().text);
                    Debug.Log($"[Pursuit] escaped={escape}, laps={race.Results.Player.Laps}, bridge={player.Shortcuts}, nearest={closest:F1}m, results={results}");
                    if (escape)
                    {
                        var retry = GameObject.Find("Actions").GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<TMP_Text>().text.Contains(Loc.Get("results.retry")));
                        retry.onClick.Invoke(); yield return null;
                    }
                }
                Assert.AreEqual(2, results);
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
