using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame.Tests
{
    public class OilHazardTests
    {
        private RaceDirector director;
        private GameObject testCarGo;
        private CarController carController;
        private CarHealth carHealth;
        private CarStatus carStatus;

        [SetUp]
        public void SetUp()
        {
            director = RaceDirector.Instance;
            director.ResetRace();

            testCarGo = new GameObject("Test_Car");
            carController = testCarGo.AddComponent<CarController>();
            carHealth = testCarGo.AddComponent<CarHealth>();
            carStatus = testCarGo.AddComponent<CarStatus>();
            carHealth.SetMaxHealth(100f, true);
            carStatus.ClearAllStatuses();

            director.RegisterCar(carController);
        }

        [TearDown]
        public void TearDown()
        {
            if (testCarGo != null) Object.DestroyImmediate(testCarGo);
            if (director != null) director.ResetRace();
        }

        [Test]
        public void SpawnSlick_CapsAtMaxCapacity_RecyclesOldest()
        {
            var pool = director.OilSlicks;
            Assert.AreEqual(0, pool.ActiveCount);

            // Spawn 3 slicks at ticks 0, 10, 20
            int s0 = pool.SpawnSlick(new Vector3(10f, 0f, 0f), 0, 10f);
            int s1 = pool.SpawnSlick(new Vector3(20f, 0f, 0f), 10, 10f);
            int s2 = pool.SpawnSlick(new Vector3(30f, 0f, 0f), 20, 10f);

            Assert.AreEqual(3, pool.ActiveCount);
            Assert.AreEqual(OilHazardPool.MaxSlicks, pool.ActiveCount);

            // Spawn 4th slick at tick 30 - must recycle s0 which has the lowest ExpireTick (500 vs 510 vs 520)
            int s3 = pool.SpawnSlick(new Vector3(40f, 0f, 0f), 30, 10f);

            Assert.AreEqual(3, pool.ActiveCount);
            Assert.AreEqual(s0, s3, "Oldest slick slot must be recycled when cap is reached.");
            Assert.AreEqual(new Vector3(40f, 0f, 0f), pool.GetSlick(s3).Position);
        }

        [Test]
        public void LifetimeExpiry_DespawnsAfter10Seconds()
        {
            var pool = director.OilSlicks;
            int slot = pool.SpawnSlick(new Vector3(100f, 0f, 100f), 100, 10f); // 10s = 500 ticks, expires at 600

            Assert.AreEqual(1, pool.ActiveCount);
            Assert.IsTrue(pool.GetSlick(slot).IsActive);

            // Before expiration (tick 599)
            pool.SimulationStep(599, director.Cars);
            Assert.AreEqual(1, pool.ActiveCount);
            Assert.IsTrue(pool.GetSlick(slot).IsActive);

            // At expiration (tick 600)
            pool.SimulationStep(600, director.Cars);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.IsFalse(pool.GetSlick(slot).IsActive);
        }

        [Test]
        public void ProximityTrigger_AppliesSlowedStatusAndGripMultiplier()
        {
            var pool = director.OilSlicks;
            pool.SpawnSlick(Vector3.zero, 0, 10f);

            // Car within 2.5m (1.5m away)
            testCarGo.transform.position = new Vector3(1.5f, 0f, 0f);

            Assert.IsFalse(carStatus.IsSlowed);
            Assert.AreEqual(1f, carStatus.GripMultiplier, 1e-4f);

            pool.SimulationStep(1, director.Cars);

            Assert.IsTrue(carStatus.IsSlowed);
            Assert.AreEqual(OilHazardPool.SlowedGripMultiplier, carStatus.GripMultiplier, 1e-4f);
            Assert.AreEqual(2.0f, carStatus.GetRemainingSeconds(StatusKind.Slowed), 0.1f);
        }

        [Test]
        public void OutOfRange_DoesNotApplyStatus()
        {
            var pool = director.OilSlicks;
            pool.SpawnSlick(Vector3.zero, 0, 10f);

            // Car outside 2.5m (3.5m away)
            testCarGo.transform.position = new Vector3(3.5f, 0f, 0f);

            pool.SimulationStep(1, director.Cars);

            Assert.IsFalse(carStatus.IsSlowed);
            Assert.AreEqual(1f, carStatus.GripMultiplier, 1e-4f);
        }

        [Test]
        public void DeadCar_IgnoredByOilSlick()
        {
            var pool = director.OilSlicks;
            pool.SpawnSlick(Vector3.zero, 0, 10f);

            testCarGo.transform.position = new Vector3(1.0f, 0f, 0f);

            // Destroy the car
            carHealth.TakeDamage(200f);
            Assert.IsTrue(carHealth.IsDead);

            carStatus.ClearAllStatuses();

            pool.SimulationStep(1, director.Cars);

            Assert.IsFalse(carStatus.IsSlowed);
        }

        [Test]
        public void CarHealthWreck_SpawnsOilSlickAtWreckPosition()
        {
            testCarGo.transform.position = new Vector3(45f, 2f, -15f);

            bool wreckFired = false;
            carHealth.OnWreck += h => wreckFired = true;

            carHealth.TakeDamage(150f);

            Assert.IsTrue(wreckFired);
            Assert.IsTrue(carHealth.IsDead);
            Assert.AreEqual(1, director.OilSlicks.ActiveCount);

            var slick = director.OilSlicks.GetSlick(0);
            Assert.IsTrue(slick.IsActive);
            Assert.AreEqual(new Vector3(45f, 2f, -15f), slick.Position);
        }

        [Test]
        public void ResetRace_ClearsAllActiveSlicks()
        {
            var pool = director.OilSlicks;
            pool.SpawnSlick(new Vector3(10f, 0f, 0f), 0, 10f);
            pool.SpawnSlick(new Vector3(20f, 0f, 0f), 0, 10f);
            pool.SpawnSlick(new Vector3(30f, 0f, 0f), 0, 10f);

            Assert.AreEqual(3, pool.ActiveCount);

            director.ResetRace();

            Assert.AreEqual(0, pool.ActiveCount);
        }

        [Test]
        public void ZeroAllocations_InSimulationHotPath()
        {
            var pool = director.OilSlicks;
            pool.SpawnSlick(new Vector3(0f, 0f, 0f), 0, 100f);
            pool.SpawnSlick(new Vector3(50f, 0f, 0f), 0, 100f);
            pool.SpawnSlick(new Vector3(100f, 0f, 0f), 0, 100f);

            testCarGo.transform.position = new Vector3(1.5f, 0f, 0f);

            // Warm up
            for (int i = 0; i < 10; i++)
            {
                pool.SimulationStep(i + 1, director.Cars);
                carStatus.SimulationStep(i + 1);
            }

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 11; i <= 40; i++)
            {
                pool.SimulationStep(i, director.Cars);
                carStatus.SimulationStep(i);
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after, "OilHazardPool.SimulationStep must not allocate GC memory in hot path.");
        }
    }
}
