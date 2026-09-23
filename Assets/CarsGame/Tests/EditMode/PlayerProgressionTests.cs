using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame.Tests
{
    public class PlayerProgressionTests
    {
        private GameObject testGo;

        [SetUp]
        public void SetUp()
        {
            testGo = new GameObject("TestCar");
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
        public void ApplyToNullDoesNotThrow()
        {
            Assert.DoesNotThrow(() => PlayerProgression.ApplyTo(null, null, null, new[] { 0, 0, 0 }));
            Assert.DoesNotThrow(() => PlayerProgression.ApplyTo(null, null, null, new[] { 2, 2, 2 }));
        }

        [Test]
        public void ApplyToUpgradesCarStats()
        {
            var car = testGo.AddComponent<CarController>();
            car.maxSpeed = 100f;
            car.grip = 1.0f;
            car.motorTorque = 5000f;

            var health = testGo.AddComponent<CarHealth>();
            health.maxHealth = 100f;

            var cannon = testGo.AddComponent<CarCannon>();
            cannon.shellDamage = 20f;
            cannon.fireInterval = 0.5f;

            var levels = new[] { 2, 1, 3 }; // Engine 2, Armor 1, Weapon 3
            var fx = UpgradeCatalog.For(levels);

            PlayerProgression.ApplyTo(car, health, cannon, levels);

            Assert.AreEqual(100f * fx.MaxSpeedFactor, car.maxSpeed, 1e-4f);
            Assert.AreEqual(1.0f * fx.GripFactor, car.grip, 1e-4f);
            Assert.AreEqual(5000f * fx.TorqueFactor, car.motorTorque, 5000f * 1e-6f);   // float spacing at ~5000 is ~5e-4

            Assert.AreEqual(100f * fx.MaxHealthFactor, health.maxHealth, 1e-4f);
            Assert.AreEqual(health.maxHealth, health.Health, 1e-4f);

            Assert.AreEqual(20f * fx.DamageFactor, cannon.shellDamage, 1e-4f);
            Assert.AreEqual(0.5f * fx.FireIntervalFactor, cannon.fireInterval, 1e-4f);
        }

        [Test]
        public void ApplyWeaponsConfiguresCarWeaponsAndAppliesScaling()
        {
            var weapons = testGo.AddComponent<CarWeapons>();
            var weaponLevels = new[] { 2, 1, 3 }; // Mines lv2, FreezeLauncher lv1, MachineGun lv3

            // Equip Mines as secondary, Cannon as primary
            PlayerProgression.ApplyWeapons(weapons, weaponLevels, (int)SecondaryWeaponType.Mines, 0);
            Assert.AreEqual(PrimaryWeaponType.Cannon, weapons.PrimaryType);
            Assert.AreEqual(SecondaryWeaponType.Mines, weapons.secondaryType);
            Assert.AreEqual(4, weapons.secondaryAmmoMax); // 3 * 1.34 = 4.02 -> 4
            Assert.AreEqual(weapons.secondaryAmmoMax, weapons.SecondaryAmmo);

            // Equip FreezeLauncher as secondary, MachineGun as primary
            PlayerProgression.ApplyWeapons(weapons, weaponLevels, (int)SecondaryWeaponType.FreezeLauncher, 1);
            Assert.AreEqual(PrimaryWeaponType.MachineGun, weapons.PrimaryType);
            Assert.AreEqual(SecondaryWeaponType.FreezeLauncher, weapons.secondaryType);
            Assert.AreEqual(3, weapons.secondaryAmmoMax); // lv 1 -> 3
            Assert.Less(weapons.heatPerShot, 2.5f); // scaled down with mg upgrade
        }
    }
}
