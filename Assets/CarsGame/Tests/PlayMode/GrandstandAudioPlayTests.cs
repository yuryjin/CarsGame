using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class GrandstandAudioPlayTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator OnlyThePassBesideTheSpectatorsTriggersOneCheer()
        {
            float clock = Time.captureDeltaTime;
            GameFlow flow = null;
            RaceEventDefinition ev = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float timeout = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
                yield return null; yield return null;
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>();
                ev = Object.Instantiate(System.Array.Find(flow.events, e => e != null && e.eventId == "e1"));
                ev.aiCountOverride = 1; ev.pickupCount = 0;
                foreach (bool near in new[] { true, false })
                {
                    flow.StartRace(ev, flow.tiers[0]);
                    while (race.State == RaceState.Countdown) yield return null;
                    var player = race.Player;
                    var rival = race.Participants[0] == player ? race.Participants[1] : race.Participants[0];
                    var gunner = rival.GetComponent<AiGunner>(); if (gunner != null) gunner.enabled = false;
                    var group = Object.FindAnyObjectByType<GrandstandReaction>();
                    var path = player.Progress.Path;
                    float origin = near ? path.startLineDistance - 28f : path.startLineDistance + path.Length * .53f;
                    var racers = new[] { player, rival };
                    var inputs = new ScriptedDriverInput[2];
                    for (int i = 0; i < racers.Length; i++)
                    {
                        path.Pose(origin + i * 12f, i == 0 ? 1.6f : -1.6f, out var pos, out var rot);
                        racers[i].Car.Teleport(pos + Vector3.up * .4f, rot);
                        inputs[i] = racers[i].gameObject.AddComponent<ScriptedDriverInput>();
                        racers[i].Car.SetInputSource(inputs[i]); inputs[i].Set(0f, 0f, true);
                        racers[i].Progress.Resync();
                    }
                    using (var capture = new AgentAudioCapture(near ? "grandstand-near" : "grandstand-far"))
                    {
                        if (capture.Active) { Time.captureDeltaTime = 0f; capture.Solo(); yield return new WaitForSecondsRealtime(1f); }
                        double since = race.RaceTime;
                        float side = -12f;
                        bool passed = false, heard = false;
                        int previous = group.Reactions;
                        capture.Mark("accelerate");
                        var voices = SfxPlayer.Instance.GetComponentsInChildren<AudioSource>();
                        var cheerVoices = new List<AudioSource>();
                        for (int i = 0; i < racers.Length; i++) racers[i].Car.Body.linearVelocity = racers[i].transform.forward * (i == 0 ? 17f : 6f);
                        while (race.RaceTime - since < 5f)
                        {
                            for (int i = 0; i < racers.Length; i++)
                            {
                                var car = racers[i].Car;
                                path.Pose(racers[i].Progress.Distance + 13f, i == 0 ? 1.6f : -1.6f, out var aim, out _);
                                float angle = Vector3.SignedAngle(car.transform.forward, aim - car.transform.position, Vector3.up);
                                inputs[i].Set(car.SpeedKmh < (i == 0 ? 68f : 30f) ? 1f : -.25f, Mathf.Clamp(angle / 24f - car.Body.angularVelocity.y * .2f, -1f, 1f));
                            }
                            side = path.SignedDelta(rival.Progress.Distance, player.Progress.Distance);
                            if (!passed && side >= 0) { passed = true; capture.Mark("position-swapped"); }
                            if (!heard && group.Reactions > previous) { heard = true; capture.Mark("crowd"); }
                            if (capture.Active)
                            {
                                cheerVoices.Clear();
                                foreach (var voice in voices) if (voice.clip == GrandstandReaction.Cheer) cheerVoices.Add(voice);
                                capture.Solo(cheerVoices.ToArray());
                            }
                            yield return null;
                        }
                        Assert.IsTrue(passed); Assert.AreEqual(near ? 1 : 0, group.Reactions - previous);
                        Debug.Log($"[Grandstand] near={near} physical pass={passed} reactions={group.Reactions - previous} end gap={side:F1}m");
                    }
                    flow.EnterMenu(); yield return null; Time.captureDeltaTime = SimulationDriver.FixedStep;
                }
            }
            finally { if (flow != null) flow.EnterMenu(); if (ev != null) Object.Destroy(ev); Time.captureDeltaTime = clock; }
        }
    }
}
