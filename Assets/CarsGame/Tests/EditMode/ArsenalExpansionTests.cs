using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Combat;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class ArsenalExpansionTests
    {
        private RaceDirector director;
        private GameObject owner, victim;
        private CarHealth health;
        private CarStatus status;
        private GameObject wall;

        [SetUp]
        public void SetUp()
        {
            director = RaceDirector.Instance;
            director.ResetRace();
            owner = MakeCar("ArsenalOwner", Vector3.zero);
            victim = MakeCar("ArsenalVictim", Vector3.forward * 3f);
            health = victim.GetComponent<CarHealth>();
            status = victim.GetComponent<CarStatus>();
        }

        private GameObject MakeCar(string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            go.AddComponent<Rigidbody>().isKinematic = true;
            var car = go.AddComponent<CarController>();
            go.AddComponent<CarStatus>();
            go.AddComponent<CarHealth>().SetMaxHealth(10000f, true);
            director.RegisterCar(car);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            if (director != null) director.ResetRace();
            if (wall != null) Object.DestroyImmediate(wall);
            if (owner != null) Object.DestroyImmediate(owner);
            if (victim != null) Object.DestroyImmediate(victim);
        }

        [Test]
        public void CatalogHasFiftyUniqueNamedWeapons_AndAllFamiliesHaveWorkingDamage()
        {
            Assert.AreEqual(50, Enum.GetValues(typeof(PrimaryWeaponType)).Length);
            var keys = new HashSet<string>();
            for (int i = 0; i < PrimaryWeaponCatalog.Count; i++)
            {
                var type = (PrimaryWeaponType)i;
                var shot = PrimaryWeaponCatalog.Get(type);
                Assert.IsTrue(keys.Add(shot.NameKey), type.ToString());
                Assert.Greater(shot.Interval, 0f);
                Assert.Greater(shot.Damage, 0f);
                if (shot.Mode == WeaponMode.Projectile || shot.Mode == WeaponMode.Trap) continue;
                var attack = new SpecialWeaponAttack(owner);
                attack.Configure(type);
                status.ClearAllStatuses();
                float before = health.Health;
                attack.Fire(shot, shot.Damage, 100);
                if (shot.Mode == WeaponMode.Melee)
                {
                    Assert.AreEqual(before, health.Health, "Melee has a visible wind-up.");
                    attack.Step(100 + SimulationDriver.SecondsToTicks(Mathf.Min(0.18f, shot.Interval * 0.45f)));
                }
                Assert.AreEqual(before - shot.Damage, health.Health, 0.01f, type.ToString());
            }
        }

        [Test]
        public void BoxerHitsToTheSide_ButCannotHitThroughWalls_OrAfterCancellation()
        {
            victim.transform.position = Vector3.right * 4f;
            var attack = new SpecialWeaponAttack(owner);
            var shot = PrimaryWeaponCatalog.Get(PrimaryWeaponType.Boxer);
            attack.Configure(PrimaryWeaponType.Boxer);
            float initial = health.Health;
            attack.Fire(shot, shot.Damage, 0);
            attack.Step(20);
            Assert.AreEqual(initial - shot.Damage, health.Health);
            attack.Fire(shot, shot.Damage, 100);
            attack.Cancel();
            attack.Step(120);
            Assert.AreEqual(initial - shot.Damage, health.Health);
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.layer = LayerMask.NameToLayer("Track");
            wall.transform.position = new Vector3(2f, 1f, 0f);
            wall.transform.localScale = new Vector3(0.3f, 3f, 3f);
            Physics.SyncTransforms();
            attack.Fire(shot, shot.Damage, 200);
            attack.Step(220);
            Assert.AreEqual(initial - shot.Damage, health.Health, "No punching through track geometry.");
        }

        [Test]
        public void WelderRespectsShieldAndRange_PulseCanHitBehindCar()
        {
            var attack = new SpecialWeaponAttack(owner);
            var welder = PrimaryWeaponCatalog.Get(PrimaryWeaponType.Welder);
            attack.Configure(PrimaryWeaponType.Welder);
            status.ApplyStatus(StatusKind.Shield, 1000);
            float initial = health.Health;
            attack.Fire(welder, welder.Damage, 0);
            Assert.AreEqual(initial, health.Health);
            Assert.IsFalse(status.HasShield);
            victim.transform.position = Vector3.forward * 30f;
            attack.Fire(welder, welder.Damage, 100);
            Assert.AreEqual(initial, health.Health);
            victim.transform.position = Vector3.back * 3f;
            var pulse = PrimaryWeaponCatalog.Get(PrimaryWeaponType.Repulsor);
            attack.Configure(PrimaryWeaponType.Repulsor);
            attack.Fire(pulse, pulse.Damage, 200);
            Assert.AreEqual(initial - pulse.Damage, health.Health);
        }

        [Test]
        public void BananaIsCosmetic_MineReuseClearsTrapOverrides()
        {
            var banana = PrimaryWeaponCatalog.Get(PrimaryWeaponType.GiantBanana);
            var mineProfile = PrimaryWeaponCatalog.Get(PrimaryWeaponType.MineDispenser);
            Assert.AreEqual(mineProfile.Damage, banana.Damage);
            Assert.AreEqual(mineProfile.Interval, banana.Interval);
            Assert.AreEqual(mineProfile.Range, banana.Range);
            var mine = director.RequestMine(new MineSpawn { Position = Vector3.zero, Rotation = Quaternion.identity, Tick = 0,
                Damage = 5f, Skin = MineSkin.Glue, OverrideStatus = true, Status = StatusKind.Slowed, StatusDuration = 4f, BlastRadius = 8f });
            Assert.AreEqual(8f, mine.BlastRadius);
            mine.Deactivate();
            mine = director.RequestMine(new MineSpawn { Position = Vector3.zero, Rotation = Quaternion.identity, Tick = 1, Damage = 25f, Skin = MineSkin.Banana });
            Assert.AreEqual(4f, mine.BlastRadius);
            Assert.AreEqual(MineSkin.Banana, mine.Skin);
            mine.Detonate(100);
            Assert.IsTrue(status.IsPunctured);
            Assert.IsFalse(status.IsSlowed);
        }

        [Test]
        public void BananaAndStandardMineShareTheirMagazine()
        {
            var cannon = owner.AddComponent<CarCannon>();
            var weapons = owner.GetComponent<CarWeapons>() ?? owner.AddComponent<CarWeapons>();
            weapons.SetPrimaryType(PrimaryWeaponType.GiantBanana);
            Assert.IsTrue(cannon.TryFire(100));
            Assert.AreEqual(2, cannon.Ammo);
            Assert.IsTrue(weapons.TrySwitchPrimary(PrimaryWeaponType.MineDispenser, 200));
            Assert.AreEqual(2, cannon.Ammo, "A cosmetic switch never adds ammunition.");
            Assert.IsTrue(cannon.TryFire(300));
            Assert.IsTrue(weapons.TrySwitchPrimary(PrimaryWeaponType.GiantBanana, 400));
            Assert.AreEqual(1, cannon.Ammo);
        }

        [Test]
        public void HomingRocketTurnsTowardTarget_AndGravityResetsOnReuse()
        {
            victim.transform.position = new Vector3(10f, 0f, 10f);
            var rocket = director.RequestProjectile(new ProjectileSpawn { Owner = owner, Velocity = Vector3.forward * 30f,
                Weapon = PrimaryWeaponType.HomingRocket, HomingTarget = victim.transform, Damage = 20f });
            Assert.IsFalse(rocket.GetComponent<Rigidbody>().useGravity);
            rocket.SimulationStep(1);
            Assert.Greater(rocket.GetComponent<Rigidbody>().linearVelocity.x, 0f);
            rocket.Deactivate();
            var shell = director.RequestProjectile(new ProjectileSpawn { Owner = owner, Velocity = Vector3.forward * 30f });
            Assert.IsTrue(shell.GetComponent<Rigidbody>().useGravity);
        }

        [Test]
        public void EveryTrapCanBeDroppedAndFiftySlotsCanBeSelected()
        {
            var cannon = owner.AddComponent<CarCannon>();
            var weapons = owner.GetComponent<CarWeapons>() ?? owner.AddComponent<CarWeapons>();
            weapons.SetPrimaryType(PrimaryWeaponType.Cannon);
            weapons.machineGunUnlocked = true;
            for (int slot = 0; slot < PrimaryWeaponCatalog.Count; slot++)
            {
                var type = (PrimaryWeaponType)slot;
                var shot = PrimaryWeaponCatalog.Get(type);
                int tick = (slot + 1) * 1000;
                if (slot != 0) Assert.IsTrue(weapons.TrySwitchPrimary(type, tick), type.ToString());
                Assert.AreEqual(type, weapons.PrimaryType);
                weapons.ResetHeat();
                int before = director.LiveMines;
                Assert.IsTrue(cannon.TryFire(tick + 100), type.ToString());
                if (shot.Mode == WeaponMode.Trap) Assert.AreEqual(before + 1, director.LiveMines);
                cannon.SimulationStep(tick + 120);
            }
        }

        [Test]
        public void RicochetReflectsVelocityAndIsLimited_PoolReuseClearsIt()
        {
            var projectile = director.RequestProjectile(new ProjectileSpawn { Owner = owner, Position = Vector3.up, Velocity = Vector3.forward * 20f,
                Ricochets = 2, Weapon = PrimaryWeaponType.RicochetSaw, Damage = 10f });
            Assert.IsTrue(projectile.TryRicochet(Vector3.back));
            Assert.Less(projectile.GetComponent<Rigidbody>().linearVelocity.z, 0f);
            Assert.IsTrue(projectile.TryRicochet(Vector3.forward));
            Assert.IsFalse(projectile.TryRicochet(Vector3.back));
            projectile.Deactivate();
            projectile = director.RequestProjectile(new ProjectileSpawn { Owner = owner, Velocity = Vector3.forward * 20f });
            Assert.AreEqual(0, projectile.RicochetsRemaining);
            Assert.AreEqual(Vector3.one * 0.22f, projectile.transform.localScale);
            Assert.IsTrue(projectile.GetComponent<Renderer>().enabled);
        }
    }
}
