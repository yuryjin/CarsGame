using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    /// <summary>
    /// Drift on the real car physics: full throttle, then brake + full steer into a slide, then release.
    /// Measures the slide angle, the speed kept through the drift and the boost stage, and logs them for tuning.
    /// </summary>
    public sealed class DriftPlayTests
    {
        private float savedCaptureDelta;
        [SetUp] public void Save() { savedCaptureDelta = Time.captureDeltaTime; }
        [TearDown] public void Restore() { Time.captureDeltaTime = savedCaptureDelta; LogAssert.ignoreFailingMessages = false; }

        [UnityTest, Timeout(300000)]
        public IEnumerator BrakeSteerDriftsHoldsSpeedAndBoosts()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float wait = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < wait) yield return null;
            yield return null; yield return null;   // GameFlow.Start (EnterMenu) must run before a race starts
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            RaceEventDefinition ev = null;
            foreach (var e in flow.events) if (e != null && e.eventId == "e1") ev = e;
            ev = Object.Instantiate(ev); ev.aiCountOverride = 0; ev.laps = 1; ev.pickupCount = 0;
            flow.StartRace(ev, null);
            var car = race.Player.Car;
            var input = car.gameObject.AddComponent<ScriptedDriverInput>();
            car.SetInputSource(input);
            Assert.IsTrue(car.driftEnabled, "the flow enables drift on the player car");
            while (race.State == RaceState.Countdown) yield return null;

            int stage = 0; float boostSeconds = 0f;
            car.DriftBoosted += (s, b) => { stage = s; boostSeconds = b; };
            input.Set(1f, 0f);
            for (int i = 0; i < 150; i++) yield return null;               // 3 s flat out
            float entrySpeed = car.Body.linearVelocity.magnitude;
            input.Set(1f, 1f, true);
            float maxSlip = 0f, minSpeed = float.MaxValue; bool drifted = false;
            for (int i = 0; i < 100; i++)                                  // 2 s of drift
            {
                yield return null;
                drifted |= car.IsDrifting;
                var v = car.Body.linearVelocity; v.y = 0f;
                if (v.magnitude > 3f) maxSlip = Mathf.Max(maxSlip, Vector3.Angle(car.transform.forward, v));
                minSpeed = Mathf.Min(minSpeed, v.magnitude);
            }
            input.Set(1f, 0f, false);
            for (int i = 0; i < 5; i++) yield return null;
            Debug.Log($"[DriftPlayTests] entry={entrySpeed:0.0} m/s minDuring={minSpeed:0.0} maxSlip={maxSlip:0.0}° stage={stage} boost={boostSeconds:0.0}s boosting={car.IsBoosting}");
            Assert.IsTrue(drifted, "brake + steer at speed starts a drift");
            Assert.Greater(maxSlip, 12f, "the tail comes round");
            Assert.Less(maxSlip, 80f, "no spin-out");
            Assert.Greater(minSpeed, entrySpeed * 0.45f, "a drift keeps most of the speed (a hard brake would not)");
            Assert.GreaterOrEqual(stage, 2, "2 s of full-lock drift reaches stage 2");
            Assert.IsTrue(car.IsBoosting, "releasing the brake fires the boost");
            Assert.Greater(race.Player.DriftBoosts, 0, "the skill goal receives the real drift release");
            Time.captureDeltaTime = 0f;
            flow.EnterMenu();
        }
    }
}
