using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    internal sealed class AgentAudioCapture : IDisposable
    {
        private readonly Process recorder;
        private readonly Dictionary<AudioSource, bool> previousMute = new Dictionary<AudioSource, bool>();
        private readonly string marks;
        private readonly float start;
        public bool Active => recorder != null;
        public AgentAudioCapture(string name)
        {
#if UNITY_EDITOR_LINUX
            string dir = Environment.GetEnvironmentVariable("CG_AUDIO_CAPTURE");
            if (string.IsNullOrEmpty(dir)) return;
            Assert.AreEqual("AgentSink", Environment.GetEnvironmentVariable("PULSE_SINK"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, name + ".wav");
            marks = Path.Combine(dir, name + ".txt");
            File.WriteAllText(marks, "");
            recorder = Process.Start(new ProcessStartInfo("agent-listen", "record \"" + path + "\"") { UseShellExecute = false });
            var ready = new Stopwatch(); ready.Start();
            var wav = new FileInfo(path);
            while (!recorder.HasExited && ready.Elapsed.TotalSeconds < 12)
            {
                wav.Refresh();
                if (wav.Exists && wav.Length > 256) break;
                System.Threading.Thread.Sleep(20);
            }
            start = Time.realtimeSinceStartup;
#endif
        }

        public void Mark(string phase)
        {
            if (Active) File.AppendAllText(marks, (Time.realtimeSinceStartup - start).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " " + phase + "\n");
        }

        public void Dispose()
        {
            if (!Active) return;
            if (!recorder.HasExited)
            {
                using (var signal = Process.Start(new ProcessStartInfo("kill", "-INT " + recorder.Id) { UseShellExecute = false })) signal.WaitForExit(1500);
                if (!recorder.WaitForExit(3000)) recorder.Kill();
            }
            recorder.Dispose();
            foreach (var entry in previousMute) if (entry.Key != null) entry.Key.mute = entry.Value;
        }

        public void Solo(params AudioSource[] keep)
        {
            foreach (var source in Object.FindObjectsByType<AudioSource>())
            {
                if (!previousMute.ContainsKey(source)) previousMute.Add(source, source.mute);
                source.mute = Array.IndexOf(keep, source) < 0;
            }
        }
    }

    public sealed class CrawlerAudioPlayTests
    {
        [UnityTest, Timeout(240000)]
        public IEnumerator CrawlerTracksFollowItsActualPassAndStopWithTheFortress()
        {
            var old = Time.captureDeltaTime;
            GameFlow flow = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float until = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
                yield return null; yield return null;
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>();
                flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_m1"), flow.tiers[0]);
                var mission = MissionDirector.Current;
                var boss = mission.Boss;
                var sound = boss.transform.Find("TrackRumble").GetComponent<AudioSource>();
                Assert.IsFalse(sound.isPlaying, "The parked crawler is quiet in the countdown");
                Assert.AreEqual(1f, sound.spatialBlend);
                using (var capture = new AgentAudioCapture("crawler-pass"))
                {
                    if (capture.Active) { Time.captureDeltaTime = 0f; capture.Solo(sound); }
                    capture.Mark("countdown");
                    while (race.State == RaceState.Countdown) yield return null;
                    var car = race.Player.Car;
                    var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                    car.SetInputSource(input);
                    Vector3 axis = boss.transform.right;
                    Vector3 ahead = boss.transform.forward;
                    Vector3 start = boss.transform.position - axis * 149f - ahead * 35f;
                    mission.Map.GroundPose(start, axis, out var pos, out var rot);
                    car.Teleport(pos, rot);
                    car.Body.linearVelocity = axis * 27f;
                    float nearest = float.MaxValue, side = -149f;
                    bool passed = false;
                    capture.Mark("approach");
                    int lastTick = SimulationDriver.Instance.Tick + 2500;
                    float timeout = Time.realtimeSinceStartup + 47f;
                    while (SimulationDriver.Instance.Tick < lastTick && Time.realtimeSinceStartup < timeout && side < 130f)
                    {
                        Vector3 relative = car.transform.position - boss.transform.position;
                        side = Vector3.Dot(relative, axis);
                        nearest = Mathf.Min(nearest, relative.magnitude);
                        if (!passed && side >= 0f) { capture.Mark("pass"); passed = true; }
                        Vector3 target = boss.transform.position + axis * (side + 30f) - ahead * 34f;
                        Vector3 to = target - car.transform.position; to.y = 0f;
                        float steer = Vector3.SignedAngle(car.transform.forward, to, Vector3.up) / 27f - car.Body.angularVelocity.y * .25f;
                        input.Set(car.SpeedKmh < 95f ? 1f : 0f, Mathf.Clamp(steer, -1f, 1f));
                        if (capture.Active && Time.frameCount % 8 == 0) capture.Solo(sound);
                        yield return null;
                    }
                    Assert.IsTrue(passed);
                    Assert.Less(nearest, 76f);
                    Assert.Greater(side, 115f);
                    Assert.IsTrue(sound.isPlaying);
                    capture.Mark("hold");
                    boss.Hold(SimulationDriver.Instance.Tick, 4f);
                    for (int i = 0; i < 8; i++) yield return null;
                    Assert.IsFalse(sound.isPlaying);
                    if (capture.Active) yield return new WaitForSecondsRealtime(1f);
                    flow.EnterMenu();
                    yield return null;
                    Assert.IsTrue(sound == null || !sound.isPlaying);
                    capture.Mark("menu");
                    if (capture.Active) { capture.Solo(); yield return new WaitForSecondsRealtime(1f); }
                    Debug.Log($"[CrawlerAudio] drive-by nearest={nearest:F1} endSide={side:F1}m; hold and menu silent");
                }
            }
            finally { if (flow != null) flow.EnterMenu(); Time.captureDeltaTime = old; }
        }
    }
}
