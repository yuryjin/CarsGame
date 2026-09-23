using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame.Tests
{
    public class CarStatusTests
    {
        private GameObject testGo;
        private CarStatus status;

        [SetUp]
        public void SetUp()
        {
            testGo = new GameObject("TestCar_Status");
            status = testGo.AddComponent<CarStatus>();
            status.ClearAllStatuses();
        }

        [TearDown]
        public void TearDown()
        {
            if (testGo != null)
            {
                Object.DestroyImmediate(testGo);
            }
        }

        [Test]
        public void InitialState_HasNoActiveStatusesAndDefaultMultipliers()
        {
            Assert.IsFalse(status.IsFrozen);
            Assert.IsFalse(status.IsJammed);
            Assert.IsFalse(status.IsPunctured);
            Assert.IsFalse(status.HasShield);
            Assert.IsFalse(status.HasFrozenImmunity);
            Assert.AreEqual(1f, status.SpeedMultiplier, 1e-4f);
            Assert.AreEqual(1f, status.GripMultiplier, 1e-4f);
        }

        [Test]
        public void ApplyStatus_Frozen_SetsFlagsAndReducesSpeedAndGrip()
        {
            status.SimulationStep(10);
            bool applied = status.ApplyStatus(StatusKind.Frozen, 50); // 50 ticks
            Assert.IsTrue(applied);
            Assert.IsTrue(status.IsFrozen);
            Assert.AreEqual(status.frozenSpeedMultiplier, status.SpeedMultiplier, 1e-4f);
            Assert.AreEqual(status.frozenGripMultiplier, status.GripMultiplier, 1e-4f);
            Assert.AreEqual(50, status.GetRemainingTicks(StatusKind.Frozen));

            // Advance time past expiration
            status.SimulationStep(60);
            Assert.IsFalse(status.IsFrozen);
            Assert.AreEqual(1f, status.SpeedMultiplier, 1e-4f);
            Assert.IsTrue(status.HasFrozenImmunity);
        }

        [Test]
        public void Frozen_Expiration_GrantsImmunityWindow()
        {
            status.SimulationStep(10);
            status.ApplyStatus(StatusKind.Frozen, 20); // expires at 30
            status.SimulationStep(30);

            Assert.IsFalse(status.IsFrozen);
            Assert.IsTrue(status.HasFrozenImmunity);

            // Reapplying during immunity must fail
            bool reapply = status.ApplyStatus(StatusKind.Frozen, 20);
            Assert.IsFalse(reapply);
            Assert.IsFalse(status.IsFrozen);

            // Advance past immunity (1.5s = 75 ticks)
            int immunityTicks = SimulationDriver.SecondsToTicks(status.frozenImmunitySeconds);
            status.SimulationStep(30 + immunityTicks);

            Assert.IsFalse(status.HasFrozenImmunity);
            bool afterImmunity = status.ApplyStatus(StatusKind.Frozen, 20);
            Assert.IsTrue(afterImmunity);
            Assert.IsTrue(status.IsFrozen);
        }

        [Test]
        public void DominantPenalty_TakesStrongestDebuff_NeverMultiplies()
        {
            status.SimulationStep(0);
            status.frozenSpeedMultiplier = 0.4f;
            status.frozenGripMultiplier = 0.4f;
            status.puncturedGripMultiplier = 0.6f;

            status.ApplyStatus(StatusKind.Frozen, 50);
            status.ApplyStatus(StatusKind.Punctured, 50);

            // Dominant grip penalty is min(0.4, 0.6) = 0.4, NOT 0.4 * 0.6 = 0.24
            Assert.AreEqual(0.4f, status.SpeedMultiplier, 1e-4f);
            Assert.AreEqual(0.4f, status.GripMultiplier, 1e-4f);
        }

        [Test]
        public void JammedStatus_BlocksWeaponsUntilExpired()
        {
            status.SimulationStep(5);
            bool applied = status.ApplyStatus(StatusKind.Jammed, 30);
            Assert.IsTrue(applied);
            Assert.IsTrue(status.IsJammed);

            status.SimulationStep(35);
            Assert.IsFalse(status.IsJammed);
        }

        [Test]
        public void ShieldStatus_ActiveUntilConsumedOrExpired()
        {
            status.SimulationStep(0);
            status.ApplyStatus(StatusKind.Shield, 100);
            Assert.IsTrue(status.HasShield);

            bool consumed = status.ConsumeShield();
            Assert.IsTrue(consumed);
            Assert.IsFalse(status.HasShield);

            // Consuming again returns false
            Assert.IsFalse(status.ConsumeShield());
        }

        [Test]
        public void ClearAllStatuses_ClearsEverything()
        {
            status.SimulationStep(0);
            status.ApplyStatus(StatusKind.Frozen, 50);
            status.ApplyStatus(StatusKind.Jammed, 50);
            status.ApplyStatus(StatusKind.Punctured, 50);
            status.ApplyStatus(StatusKind.Shield, 50);

            status.ClearAllStatuses();

            Assert.IsFalse(status.IsFrozen);
            Assert.IsFalse(status.IsJammed);
            Assert.IsFalse(status.IsPunctured);
            Assert.IsFalse(status.HasShield);
            Assert.IsFalse(status.HasFrozenImmunity);
            Assert.AreEqual(1f, status.SpeedMultiplier, 1e-4f);
            Assert.AreEqual(1f, status.GripMultiplier, 1e-4f);
        }

        [Test]
        public void ZeroAllocations_InHotPath()
        {
            status.SimulationStep(0);
            status.ApplyStatus(StatusKind.Frozen, 100);
            status.ApplyStatus(StatusKind.Punctured, 100);

            // Warm up JIT
            for (int i = 0; i < 10; i++)
            {
                status.SimulationStep(i + 1);
                _ = status.SpeedMultiplier;
                _ = status.GripMultiplier;
                _ = status.IsFrozen;
            }

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 11; i <= 30; i++)
            {
                status.SimulationStep(i);
                _ = status.SpeedMultiplier;
                _ = status.GripMultiplier;
                _ = status.IsFrozen;
                _ = status.IsJammed;
                _ = status.HasShield;
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after, "Hot path of CarStatus must produce 0 GC allocations.");
        }
    }
}
