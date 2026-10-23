using System.Collections;
using NUnit.Framework;
using RacingGame.Core.Combat;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class AiRecoveryPlayTests
    {
        private float capture;
        private string before;
        private GameFlow flow;
        private RaceEventDefinition practice;
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (before != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(before, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (practice != null) Object.DestroyImmediate(practice);
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        private struct Observation
        {
            public int FirstResetTick;
            public float MinDistance, MaxDistance, MaxForwardSpeed;
            public bool Reversed;
        }

        // Controlled telemetry fixture: centimeter movement is projected through the real
        // RaceProgress/LapCounter, then sampled by the actual driver. It does not replay a collision.
        private static Observation SampleMotion(RaceParticipant player, AiDriver ai, bool jitter, bool moving, bool frozen, float step = 0.08f, int teleportAt = -1, int freezeAt = -1, float origin = 200f)
        {
            var path = TrackPath.Instance;
            path.Pose(origin, 0f, out var position, out var rotation);
            player.Car.Teleport(position + Vector3.up * 0.5f, rotation);
            player.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
            player.Status.ClearAllStatuses();
            player.Progress.BeginRace();
            ai.Initialize(null, 0f);
            if (frozen) player.Status.ApplyStatus(StatusKind.Frozen, 100f);
            var result = new Observation { FirstResetTick = -1, MinDistance = float.PositiveInfinity, MaxDistance = float.NegativeInfinity };
            int start = SimulationDriver.Instance.Tick;
            for (int i = 1; i <= 1000; i++)
            {
                if (i == teleportAt)
                {
                    origin += 100f;
                    path.Pose(origin, 0f, out position, out rotation);
                    player.Car.Teleport(position + Vector3.up * 0.5f, rotation);
                    Assert.AreEqual(0f, ai.StuckTime, "Teleport clears the old deadline");
                    Assert.IsFalse(ai.Reversing, "Teleport clears the reverse manoeuvre");
                }
                if (i == freezeAt) player.Status.ApplyStatus(StatusKind.Frozen, 100f);
                if (freezeAt > 0 && i == freezeAt + 400) player.Status.ClearAllStatuses();
                float distance = origin + (moving ? i * step : jitter && i % 50 == 0 ? 0.08f : 0f);
                path.Pose(distance, 0f, out position, out rotation);
                player.transform.SetPositionAndRotation(position + Vector3.up * 0.5f, rotation);
                Physics.SyncTransforms();
                player.Progress.SimulationStep(start + i);
                result.MinDistance = Mathf.Min(result.MinDistance, player.Progress.Distance);
                result.MaxDistance = Mathf.Max(result.MaxDistance, player.Progress.Distance);
                result.MaxForwardSpeed = Mathf.Max(result.MaxForwardSpeed, player.Progress.SpeedAlongTrack);
                var command = ai.Sample(start + i);
                result.Reversed |= ai.Reversing;
                if (command.ResetEdge && result.FirstResetTick < 0) result.FirstResetTick = i;
            }
            Debug.Log($"[AiRecovery] jitter={jitter} moving={moving} frozen={frozen} step={step} teleportAt={teleportAt} freezeAt={freezeAt} firstReset={result.FirstResetTick} span={result.MaxDistance - result.MinDistance:F3}m maxAlong={result.MaxForwardSpeed:F3}m/s");
            return result;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator BoundedJitterCannotPreventAiRecovery()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            before = JsonUtility.ToJson(PlatformGlue.Save.Data);
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 0; practice.pickupCount = 0;
            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            var player = race.Player;
            var ai = player.gameObject.AddComponent<AiDriver>();
            var stopped = SampleMotion(player, ai, false, false, false);
            Assert.That(stopped.FirstResetTick, Is.InRange(350, 450), "A blocked driver should recover after the initial grace and six-second deadline");
            var moving = SampleMotion(player, ai, false, true, false);
            Assert.Greater(moving.MaxDistance - moving.MinDistance, 70f);
            Assert.AreEqual(-1, moving.FirstResetTick, "Sustained forward travel is not a stall");
            Assert.IsFalse(moving.Reversed, "Normal travel must not trigger a reverse manoeuvre");
            var slow = SampleMotion(player, ai, false, true, false, step: 0.02f);
            Assert.AreEqual(-1, slow.FirstResetTick, "One metre per second is still forward travel");
            Assert.IsFalse(slow.Reversed, "Slow forward travel must not trigger a reverse manoeuvre");
            var seam = SampleMotion(player, ai, false, true, false, origin: TrackPath.Instance.Length - 10f);
            Assert.AreEqual(-1, seam.FirstResetTick, "Crossing the spline seam must preserve progress");
            Assert.IsFalse(seam.Reversed);
            var frozen = SampleMotion(player, ai, false, false, true);
            Assert.AreEqual(-1, frozen.FirstResetTick, "Freeze status must not cause recovery");
            var thawed = SampleMotion(player, ai, false, false, false, freezeAt: 250);
            Assert.That(thawed.FirstResetTick, Is.InRange(750, 850), "Frozen time must suspend an existing recovery deadline");
            var teleported = SampleMotion(player, ai, false, false, false, teleportAt: 250);
            Assert.That(teleported.FirstResetTick, Is.InRange(540, 560), "Teleport starts a fresh six-second recovery deadline");
            var jitter = SampleMotion(player, ai, true, false, false);
            Assert.Less(jitter.MaxDistance - jitter.MinDistance, 0.2f, "The car never makes meaningful forward progress");
            Assert.Greater(jitter.MaxForwardSpeed, 2f, "The fixture must exercise the instantaneous speed reset");
            Assert.That(jitter.FirstResetTick, Is.InRange(350, 450), "Small repeated bounces must not cancel the recovery deadline indefinitely");
        }
    }
}
