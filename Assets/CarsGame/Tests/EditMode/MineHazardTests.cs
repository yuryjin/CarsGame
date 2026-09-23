using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame.Tests
{
    public class MineHazardTests
    {
        private GameObject directorGo;
        private RaceDirector director;
        private GameObject ownerGo;
        private GameObject victimGo;
        private GameObject mineGo;
        private MineHazard mine;
        private CarHealth victimHealth;
        private CarStatus victimStatus;
        private CarController victimController;

        [SetUp]
        public void SetUp()
        {
            directorGo = new GameObject("Test_Director");
            director = directorGo.AddComponent<RaceDirector>();

            ownerGo = new GameObject("OwnerCar");
            ownerGo.AddComponent<CarController>();
            ownerGo.AddComponent<CarHealth>();
            director.RegisterCar(ownerGo.GetComponent<CarController>());

            victimGo = new GameObject("VictimCar");
            victimController = victimGo.AddComponent<CarController>();
            victimHealth = victimGo.AddComponent<CarHealth>();
            victimStatus = victimGo.AddComponent<CarStatus>();
            victimHealth.SetMaxHealth(100f, true);
            director.RegisterCar(victimController);

            mineGo = new GameObject("Test_Mine");
            mine = mineGo.AddComponent<MineHazard>();
        }

        [TearDown]
        public void TearDown()
        {
            if (mineGo != null) Object.DestroyImmediate(mineGo);
            if (victimGo != null) Object.DestroyImmediate(victimGo);
            if (ownerGo != null) Object.DestroyImmediate(ownerGo);
            if (directorGo != null) Object.DestroyImmediate(directorGo);
        }

        [Test]
        public void ArmingDelay_IsNotArmedBefore25Ticks()
        {
            mine.Activate(director, new MineSpawn
            {
                Owner = ownerGo,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                Damage = 25f,
                Tick = 0
            });

            Assert.IsTrue(mine.IsLive);
            Assert.IsFalse(mine.IsArmed(10));
            Assert.IsFalse(mine.IsArmed(24));
            Assert.IsTrue(mine.IsArmed(25));
        }

        [Test]
        public void OwnerImmunity_ProtectsOwnerFor50Ticks()
        {
            mine.Activate(director, new MineSpawn
            {
                Owner = ownerGo,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                Damage = 25f,
                Tick = 0
            });

            Assert.IsTrue(mine.IsOwnerImmune(0));
            Assert.IsTrue(mine.IsOwnerImmune(49));
            Assert.IsFalse(mine.IsOwnerImmune(50));
        }

        [Test]
        public void ProximityDetonation_AppliesDamageAndPuncturedStatusToVictim()
        {
            mine.Activate(director, new MineSpawn
            {
                Owner = ownerGo,
                Position = Vector3.zero,
                Rotation = Quaternion.identity,
                Damage = 25f,
                Tick = 0
            });

            // Position victim within trigger radius (e.g. 1.5m away)
            victimGo.transform.position = new Vector3(1.5f, 0f, 0f);

            // Step at tick 10 (not armed yet)
            mine.SimulationStep(10);
            Assert.IsTrue(mine.IsLive);
            Assert.AreEqual(100f, victimHealth.Health);

            // Step at tick 25 (armed now!)
            mine.SimulationStep(25);

            // Mine should have detonated and deactivated
            Assert.IsFalse(mine.IsLive);
            Assert.Less(victimHealth.Health, 100f);
            Assert.IsTrue(victimStatus.IsPunctured);
        }

        [Test]
        public void LifetimeExpiry_DeactivatesMine()
        {
            mine.Activate(director, new MineSpawn
            {
                Owner = ownerGo,
                Position = new Vector3(100f, 0f, 100f), // far away from cars
                Rotation = Quaternion.identity,
                Damage = 25f,
                Tick = 0
            });

            Assert.IsTrue(mine.IsLive);

            // Step before 15s (750 ticks)
            mine.SimulationStep(700);
            Assert.IsTrue(mine.IsLive);

            // Step at or after 750 ticks
            mine.SimulationStep(750);
            Assert.IsFalse(mine.IsLive);
        }

        [Test]
        public void ZeroAllocations_InMineSimulationStep()
        {
            mine.Activate(director, new MineSpawn
            {
                Owner = ownerGo,
                Position = new Vector3(100f, 0f, 100f),
                Rotation = Quaternion.identity,
                Damage = 25f,
                Tick = 0
            });

            // Warm up
            for (int i = 0; i < 10; i++)
            {
                mine.SimulationStep(i);
            }

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 11; i <= 30; i++)
            {
                mine.SimulationStep(i);
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after, "SimulationStep of MineHazard must produce 0 GC allocations.");
        }
    }
}
