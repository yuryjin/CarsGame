using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame.Tests
{
    public class RaceDirectorPoolTests
    {
        private GameObject directorGo;
        private RaceDirector director;
        private GameObject testCarGo;
        private CarController car;
        private CarHealth health;
        private CarStatus status;

        [SetUp]
        public void SetUp()
        {
            directorGo = new GameObject("Test_Director");
            director = directorGo.AddComponent<RaceDirector>();
            director.maxLiveProjectiles = 48;
            director.maxLiveMines = 24;

            testCarGo = new GameObject("TestCar");
            testCarGo.AddComponent<BoxCollider>();
            car = testCarGo.AddComponent<CarController>();
            health = testCarGo.AddComponent<CarHealth>();
            status = testCarGo.AddComponent<CarStatus>();
            director.RegisterCar(car);
        }

        [TearDown]
        public void TearDown()
        {
            if (testCarGo != null) Object.DestroyImmediate(testCarGo);
            if (directorGo != null) Object.DestroyImmediate(directorGo);
        }

        [Test]
        public void ProjectilePool_EnforcesHardCapOf48()
        {
            director.maxLiveProjectiles = 48;

            for (int i = 0; i < 60; i++)
            {
                director.RequestProjectile(new ProjectileSpawn
                {
                    Owner = testCarGo,
                    Position = new Vector3(i * 2f, 0f, 0f),
                    Velocity = Vector3.forward * 10f,
                    Damage = 10f,
                    Tick = 0
                });
            }

            Assert.AreEqual(48, director.LiveProjectiles);
            Assert.AreEqual(60, director.ShotsFired);
        }

        [Test]
        public void MinePool_EnforcesHardCapOf24()
        {
            director.maxLiveMines = 24;

            for (int i = 0; i < 30; i++)
            {
                director.RequestMine(new MineSpawn
                {
                    Owner = testCarGo,
                    Position = new Vector3(i * 2f, 0f, 0f),
                    Rotation = Quaternion.identity,
                    Damage = 25f,
                    Tick = 0
                });
            }

            Assert.AreEqual(24, director.LiveMines);
            Assert.AreEqual(30, director.MinesDropped);
        }

        [Test]
        public void ResetRace_RecallsAllLiveProjectilesAndMines()
        {
            director.RequestProjectile(new ProjectileSpawn { Owner = testCarGo, Position = Vector3.zero, Velocity = Vector3.forward, Damage = 10f, Tick = 0 });
            director.RequestProjectile(new ProjectileSpawn { Owner = testCarGo, Position = Vector3.one, Velocity = Vector3.forward, Damage = 10f, Tick = 0 });
            director.RequestMine(new MineSpawn { Owner = testCarGo, Position = Vector3.right, Rotation = Quaternion.identity, Damage = 25f, Tick = 0 });

            Assert.AreEqual(2, director.LiveProjectiles);
            Assert.AreEqual(1, director.LiveMines);

            director.ResetRace();

            Assert.AreEqual(0, director.LiveProjectiles);
            Assert.AreEqual(0, director.LiveMines);
            Assert.AreEqual(0, director.ShotsFired);
            Assert.AreEqual(0, director.MinesDropped);
        }

        [Test]
        public void FreezeProjectile_AppliesFrozenStatus()
        {
            var victimGo = new GameObject("Victim");
            var victimHealth = victimGo.AddComponent<CarHealth>();
            var victimStatus = victimGo.AddComponent<CarStatus>();
            victimHealth.SetMaxHealth(100f, true);

            var proj = director.RequestProjectile(new ProjectileSpawn
            {
                Owner = testCarGo,
                Position = Vector3.zero,
                Velocity = Vector3.forward * 40f,
                Damage = 5f,
                Tick = 0,
                Kind = ProjectileKind.Freeze,
                Status = StatusKind.Frozen,
                StatusDuration = 1.5f
            });

            Assert.AreEqual(ProjectileKind.Freeze, proj.Kind);
            Assert.AreEqual(StatusKind.Frozen, proj.StatusToApply);

            Object.DestroyImmediate(victimGo);
        }

        [Test]
        public void ItemPickup_GivesShieldWhenHealthAboveThreshold_AndRepairWhenLow()
        {
            var pickupGo1 = new GameObject("Pickup_Item_Shield");
            var pickup1 = pickupGo1.AddComponent<Pickup>();
            pickup1.kind = PickupKind.Item;
            pickup1.itemRepairThreshold = 60f;
            pickup1.itemRepairHeal = 40f;
            pickup1.itemShieldDuration = 6f;

            var carCollider = testCarGo.GetComponent<Collider>();

            // Test 1: Full health (100 HP > 60) -> grants Shield
            health.SetMaxHealth(100f, true);
            status.ClearAllStatuses();

            pickup1.OnTriggerEnter(carCollider);
            Assert.IsTrue(status.HasShield);

            // Test 2: Low health (50 HP <= 60) with Frozen debuff -> heals and clears debuff
            var pickupGo2 = new GameObject("Pickup_Item_Repair");
            var pickup2 = pickupGo2.AddComponent<Pickup>();
            pickup2.kind = PickupKind.Item;
            pickup2.itemRepairThreshold = 60f;
            pickup2.itemRepairHeal = 40f;
            pickup2.itemShieldDuration = 6f;

            status.ClearAllStatuses();
            health.SetMaxHealth(100f, false);
            health.ApplyHit(new HitInfo { Damage = 50f, HitType = HitType.Direct }); // HP = 50
            status.ApplyStatus(StatusKind.Frozen, 50);
            Assert.IsTrue(status.IsFrozen);

            pickup2.OnTriggerEnter(carCollider);

            Assert.AreEqual(90f, health.Health); // 50 + 40 = 90
            Assert.IsFalse(status.IsFrozen); // debuff cleared!

            Object.DestroyImmediate(pickupGo1);
            Object.DestroyImmediate(pickupGo2);
        }
    }
}
