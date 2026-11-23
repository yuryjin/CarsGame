using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RacingGame.Core.Economy;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class BodyEngineAudioPlayTests
    {
        [UnityTest, Timeout(480000)]
        public IEnumerator FourBodyClassesRevAndShiftOnTheSameLaunch()
        {
            float before = Time.captureDeltaTime;
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
                var heard = new HashSet<AudioClip>();
                foreach (var body in CarBodies.Classes)
                {
                    string id = System.Array.Find(CarBodies.All, b => b.Class == body).Id;
                    Assert.IsTrue(flow.StartTestDrive(id));
                    while (race.State == RaceState.Countdown) yield return null;
                    var car = race.Player.Car;
                    var input = car.gameObject.AddComponent<ScriptedDriverInput>();
                    car.SetInputSource(input);
                    var motor = car.GetComponent<EngineAudio>();
                    var source = car.transform.Find("Audio_Engine").GetComponent<AudioSource>();
                    Assert.AreSame(SfxSynth.EngineFor(body), source.clip);
                    Assert.IsTrue(heard.Add(source.clip));
                    using (var capture = new AgentAudioCapture("motor-" + body.ToString().ToLowerInvariant()))
                    {
                        Time.captureDeltaTime = capture.Active ? 0f : SimulationDriver.FixedStep;
                        if (capture.Active) capture.Solo(source);
                        var path = race.Player.Progress.Path;
                        float best = -1f, straight = 0f;
                        for (float d = 0f; d < path.Length; d += 5f)
                        {
                            float room = path.Profile.SpeedAt(d) + path.Profile.SpeedAt(d + 50f) + path.Profile.SpeedAt(d + 100f);
                            if (room > best) { best = room; straight = d; }
                        }
                        path.Pose(straight, 0f, out var pos, out var rot);
                        car.Teleport(pos + Vector3.up * .5f, rot);
                        input.Set(0f, 0f, true);
                        capture.Mark("idle");
                        float begin = (float)race.RaceTime;
                        while (race.RaceTime - begin < 1.3f) yield return null;
                        float idle = motor.EnginePitch, highest = idle;
                        capture.Mark("full-throttle");
                        begin = (float)race.RaceTime;
                        bool shifted = false;
                        while (race.RaceTime - begin < 7.5f)
                        {
                            Vector3 to = path.Position(race.Player.Progress.Distance + 16f) - car.transform.position;
                            float steer = Vector3.SignedAngle(car.transform.forward, to, Vector3.up) / 25f - car.Body.angularVelocity.y * .23f;
                            input.Set(1f, Mathf.Clamp(steer, -1f, 1f));
                            highest = Mathf.Max(highest, motor.EnginePitch);
                            if (!shifted && motor.Gear > 0) { shifted = true; capture.Mark("upshift"); }
                            if (capture.Active && Time.frameCount % 9 == 0) capture.Solo(source);
                            yield return null;
                        }
                        Assert.Greater(highest, idle + .12f);
                        Assert.IsTrue(shifted);
                        capture.Mark("release");
                        Debug.Log($"[MotorClass] {body} clip={source.clip.name} idle={idle:F2} high={highest:F2} gear={motor.Gear} kmh={car.SpeedKmh:F0}");
                    }
                    flow.EnterMenu();
                    yield return null;
                }
            }
            finally { if (flow != null) flow.EnterMenu(); Time.captureDeltaTime = before; }
        }
    }
}
