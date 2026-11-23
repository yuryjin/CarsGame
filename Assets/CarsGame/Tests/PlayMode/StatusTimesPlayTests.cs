using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Combat;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class StatusTimesPlayTests
    {
        private float capture;
        private GameFlow flow;

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CountdownMatchesShieldJammingAndDrivingAcrossPauseAndReapplication()
        {
            IEnumerator Frames(int count) { while (count-- > 0) yield return null; }
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady); yield return Frames(2);
            flow = Object.FindAnyObjectByType<GameFlow>();
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
            var race = flow.GetComponent<RaceManager>();
            while (race.State == RaceState.Countdown) yield return null;
            var player = race.Player;
            var car = player.Car;
            var cannon = player.GetComponent<CarCannon>();
            cannon.locked = false; cannon.magazineSize = 0;
            var status = player.GetComponent<CarStatus>();
            var hud = flow.GetComponent<HudScreen>();
            var driver = player.GetComponent<PlayerDriverInput>();
            driver.autoThrottle = true;
            var pose = car.transform.position; var facing = car.transform.rotation;
            void Check(StatusKind kind)
            {
                int seconds = Mathf.CeilToInt(status.GetRemainingSeconds(kind));
                var badge = hud.GetStatusIndicator(kind);
                Assert.AreEqual(seconds > 0, badge.IsActive, kind.ToString());
                if (seconds > 0) StringAssert.Contains(Loc.Format("hud.effect_seconds", "n", seconds), badge.LabelText.text);
            }
            status.ApplyStatus(StatusKind.Shield, 5f);
            status.ApplyStatus(StatusKind.Jammed, 1f);
            status.ApplyStatus(StatusKind.Frozen, 1.8f);
            yield return Frames(4);
            Check(StatusKind.Shield); Check(StatusKind.Jammed); Check(StatusKind.Frozen);
            Assert.IsFalse(cannon.TryFire(), "The displayed jam blocks an actual shot");
            float before = status.GetRemainingSeconds(StatusKind.Jammed);
            status.ApplyStatus(StatusKind.Jammed, 2.6f); yield return Frames(2);
            Assert.Greater(status.GetRemainingSeconds(StatusKind.Jammed), before + 1f);
            Check(StatusKind.Jammed);
            PauseController.Set(PauseReason.User, true); yield return Frames(3);
            int held = status.GetRemainingTicks(StatusKind.Jammed);
            string label = hud.GetStatusIndicator(StatusKind.Jammed).LabelText.text;
            yield return Frames(25);
            Assert.AreEqual(held, status.GetRemainingTicks(StatusKind.Jammed));
            Assert.AreEqual(label, hud.GetStatusIndicator(StatusKind.Jammed).LabelText.text);
            PauseController.Set(PauseReason.User, false); yield return Frames(2);
            float hp = player.Health.Health;
            Assert.IsFalse(player.Health.TakeDamage(12f, null));
            Assert.AreEqual(hp, player.Health.Health);
            yield return Frames(2); Check(StatusKind.Shield);
            Assert.IsFalse(status.HasShield);
            car.Teleport(pose, facing); yield return Frames(20);
            float frozenSpeed = car.Body.linearVelocity.magnitude;
            Assert.Less(status.SpeedMultiplier, 1f);
            while (status.IsFrozen) yield return null;
            yield return Frames(1);
            Check(StatusKind.Frozen); Assert.AreEqual(1f, status.SpeedMultiplier);
            car.Teleport(pose, facing); yield return Frames(20);
            float restoredSpeed = car.Body.linearVelocity.magnitude;
            Debug.Log($"Effect HUD frozen acceleration {frozenSpeed:F2} m/s, restored {restoredSpeed:F2} m/s over 20 frames");
            Assert.Greater(restoredSpeed, frozenSpeed * 1.15f);
            while (status.IsJammed) yield return null;
            yield return Frames(1); Check(StatusKind.Jammed);
            Assert.IsTrue(cannon.TryFire(), "The gun is available when the last jam second disappears");
        }
    }
}
