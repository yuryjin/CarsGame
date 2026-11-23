using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RacingGame.Tests
{
    public sealed class PhotoFinishPlayTests
    {
        internal static void ArmLap(RaceProgress progress)
        {
            var path = progress.Path; var count = progress.Counter;
            float line = path.startLineDistance;
            count.Begin(path.Wrap(line - 3f));
            for (float d = -2f; d < path.Length - 1f; d += 1f) count.Step(path.Wrap(line + d));
            count.Step(progress.Distance);
            Assert.IsTrue(count.LineArmed);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TwoSubframeLineCrossingsChooseTheRightCarAndExposeTheGap()
        {
            float clock = Time.captureDeltaTime;
            RaceEventDefinition ev = null;
            GameFlow flow = null;
            string saved = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float timeout = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
                yield return null; yield return null;
                flow = Object.FindAnyObjectByType<GameFlow>(); var race = Object.FindAnyObjectByType<RaceManager>();
                saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
                ev = Object.Instantiate(System.Array.Find(flow.events, e => e != null && e.eventId == "e1"));
                ev.aiCountOverride = 1; ev.laps = 1; ev.pickupCount = 0;
                foreach (int run in new[] { 0, 1, 2 })
                {
                    // The wide finish spans multiple frames, so physical travel and the race clock must both run in real time.
                    Time.captureDeltaTime = run < 2 ? SimulationDriver.FixedStep : 0f;
                    flow.StartRace(ev, flow.tiers[0]);
                    while (race.State == RaceState.Countdown) yield return null;
                    var player = race.Player;
                    var opponent = race.Participants[0] == player ? race.Participants[1] : race.Participants[0];
                    var cars = new[] { player, opponent };
                    var path = player.Progress.Path;
                    var tangent = path.Spline.Tangent(path.startLineDistance);
                    var side = Vector3.Cross(Vector3.up, tangent).normalized;
                    Vector3 line = path.Position(path.startLineDistance);
                    var frames = new Dictionary<RaceParticipant, int>();
                    var times = new Dictionary<RaceParticipant, double>();
                    bool close = run < 2;
                    for (int i = 0; i < cars.Length; i++)
                    {
                        var p = cars[i]; var car = p.Car;
                        float before = (run == 1 ? 1 - i : i) == 0 ? .09f : close ? .31f : 7.9f;
                        car.Teleport(line + side * (i == 0 ? 1.6f : -1.6f) - tangent * before + Vector3.up * .43f,
                            Quaternion.LookRotation(tangent));
                        car.Body.interpolation = RigidbodyInterpolation.None;
                        car.Body.constraints = RigidbodyConstraints.FreezeAll;
                        var driver = car.gameObject.AddComponent<ScriptedDriverInput>(); driver.Set(0f, 0f);
                        car.SetInputSource(driver);
                        var gunner = p.GetComponent<AiGunner>(); if (gunner != null) gunner.enabled = false;
                        p.Progress.LapCompleted += (_, _) => { frames[p] = Time.frameCount; times[p] = RaceClock.Now; };
                    }
                    // The fixture fast-forwards the gates; leave enough real race-clock time before the test crossing.
                    yield return new WaitForSecondsRealtime(.4f);
                    for (int tick = 0; tick < 4; tick++) yield return null;
                    foreach (var p in cars)
                    {
                        ArmLap(p.Progress);
                        p.Car.Body.constraints = RigidbodyConstraints.FreezeRotation;
                        p.Car.Body.linearVelocity = tangent * 21f;
                    }
                    float finishDeadline = Time.realtimeSinceStartup + 4f;
                    while (frames.Count < 2 && Time.realtimeSinceStartup < finishDeadline) yield return null;
                    Assert.AreEqual(2, frames.Count);
                    yield return null;
                    var winner = run == 1 ? opponent : player;
                    Assert.AreEqual(1, race.PositionOf(winner));
                    double actual = System.Math.Abs(player.Progress.LastCrossingTime - opponent.Progress.LastCrossingTime);
                    double expected = close ? (.31 - .09) / 21d : (7.9 - .09) / 21d;
                    Debug.Log($"[PhotoTiming] run={run} race={race.RaceTime:F4}s raw={actual:F4}s frames={frames[player]}/{frames[opponent]} ticks={player.Progress.LastCrossingTick}/{opponent.Progress.LastCrossingTick} player={player.Progress.LastCrossingPosition} opponent={opponent.Progress.LastCrossingPosition}");
                    Assert.That(actual, Is.EqualTo(expected).Within(close ? .002 : .04));
                    if (close)
                    {
                        Assert.AreEqual(frames[player], frames[opponent]); Assert.AreEqual(times[player], times[opponent]);
                        Assert.AreSame(winner, race.PhotoFirst); Assert.AreEqual(actual, race.PhotoGap, .00001);
                        Assert.AreEqual(FlowState.Racing, flow.State);
                        var picture = GameObject.Find("FinishPhoto").GetComponent<RawImage>();
                        Assert.IsTrue(picture.gameObject.activeInHierarchy);
                        Assert.IsTrue(GameObject.Find("FinishSummary").GetComponent<TMP_Text>().text.Contains(UiFactory.FormatTime(actual)));
                        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        {
                            var px = picture.texture as Texture2D;
                            Assert.NotNull(px);
                            var colors = new HashSet<Color32>(px.GetPixels32());
                            Assert.Greater(colors.Count, 128, "The freeze frame must contain the real car geometry");
                            var dir = System.Environment.GetEnvironmentVariable("CG_FINISH_SHOTS");
                            if (!string.IsNullOrEmpty(dir))
                            {
                                Directory.CreateDirectory(dir);
                                File.WriteAllBytes(Path.Combine(dir, "line-" + run + ".png"), px.EncodeToPNG());
                            }
                        }
                        float ends = Time.realtimeSinceStartup + 6f;
                        while (flow.State == FlowState.Racing && Time.realtimeSinceStartup < ends) yield return null;
                    }
                    else Assert.IsNull(race.PhotoFirst);
                    Assert.AreEqual(FlowState.Results, flow.State);
                    Debug.Log($"[PhotoFinish] run={run} first={winner.displayName} frame={frames[player]}/{frames[opponent]} expected={expected:F4}s actual={actual:F4}s photo={close}");
                    flow.EnterMenu(); yield return null;
                }
            }
            finally
            {
                if (flow != null) flow.EnterMenu(); if (ev != null) Object.Destroy(ev);
                if (saved != null) { JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data); PlatformGlue.Save.MarkDirty(); }
                Time.captureDeltaTime = clock;
            }
        }
    }
}
