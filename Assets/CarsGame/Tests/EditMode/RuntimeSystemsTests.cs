using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public class RuntimeSystemsTests
    {
        private sealed class Probe : ISimulated
        {
            public int SimulationOrder { get; }
            public Action<int> Step;
            public Probe(int order, Action<int> step = null) { SimulationOrder = order; Step = step; }
            public void SimulationStep(int tick) => Step?.Invoke(tick);
        }

        [TearDown]
        public void TearDown()
        {
            PauseController.ResetAll();
            if (SimulationDriver.Current != null) global::UnityEngine.Object.DestroyImmediate(SimulationDriver.Current.gameObject);
        }

        [Test]
        public void SimulationDriverOrdersStepsAndDefersRegistrationUntilNextTick()
        {
            var go = new GameObject("SimulationDriverTest");
            var driver = go.AddComponent<SimulationDriver>();
            var order = new List<string>();
            var late = new Probe(-1, tick => order.Add($"late:{tick}"));
            var second = new Probe(20, tick => order.Add($"second:{tick}"));
            var first = new Probe(10, tick =>
            {
                order.Add($"first:{tick}");
                driver.Register(late);
                driver.Register(late); // registration is idempotent
            });
            driver.Register(second);
            driver.Register(first);

            var fixedUpdate = typeof(SimulationDriver).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            fixedUpdate.Invoke(driver, null);
            CollectionAssert.AreEqual(new[] { "first:1", "second:1" }, order);
            fixedUpdate.Invoke(driver, null);
            CollectionAssert.AreEqual(new[] { "first:1", "second:1", "late:2", "first:2", "second:2" }, order);
        }

        [Test]
        public void SimulationTickConversionsRoundUpAndNeverReturnZero()
        {
            Assert.AreEqual(1, SimulationDriver.SecondsToTicks(0f));
            Assert.AreEqual(1, SimulationDriver.SecondsToTicks(0.001f));
            Assert.AreEqual(2, SimulationDriver.SecondsToTicks(0.021f));
            Assert.AreEqual(0.1f, SimulationDriver.TicksToSeconds(5), 1e-6f);
        }

        [Test]
        public void RotationPausePreservesUserAndHostPauseReasons()
        {
            PauseController.Set(PauseReason.User, true);
            PauseController.Set(PauseReason.Orientation, true);
            PauseController.Set(PauseReason.Orientation, false);
            Assert.AreEqual(PauseReason.User, PauseController.Reasons);
            PauseController.Set(PauseReason.Orientation, true);
            PauseController.Set(PauseReason.Ad, true);
            PauseController.Set(PauseReason.User, false);
            PauseController.Set(PauseReason.Orientation, false);
            Assert.AreEqual(PauseReason.Ad, PauseController.Reasons);
            Assert.IsTrue(RaceClock.Paused);
            PauseController.Set(PauseReason.Ad, false);
            Assert.IsFalse(PauseController.IsPaused);
            Assert.IsFalse(RaceClock.Paused);
        }

        [Test]
        public void PauseSourcesComposeAndNotifyOnlyOnStateChanges()
        {
            var changes = new List<bool>();
            Action<bool> handler = changes.Add;
            PauseController.PausedChanged += handler;
            try
            {
                PauseController.Set(PauseReason.User, true);
                PauseController.Set(PauseReason.User, true);
                PauseController.Set(PauseReason.Ad, true);
                PauseController.Set(PauseReason.User, false);
                Assert.IsTrue(PauseController.IsPaused, "an ad pause must remain after the user resumes");
                Assert.IsTrue(RaceClock.Paused);
                Assert.AreEqual(0f, Time.timeScale);
                PauseController.Set(PauseReason.Ad, false);
                CollectionAssert.AreEqual(new[] { true, false }, changes);
                Assert.IsFalse(PauseController.IsPaused);
                Assert.IsFalse(RaceClock.Paused);
                Assert.AreEqual(1f, Time.timeScale);
            }
            finally { PauseController.PausedChanged -= handler; }
        }
    }
}
