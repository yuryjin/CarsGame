using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame.Tests
{
    public class CarWeaponsTests
    {
        private GameObject testGo;
        private CarWeapons weapons;
        private CarStatus status;

        [SetUp]
        public void SetUp()
        {
            testGo = new GameObject("TestCar_Weapons");
            status = testGo.AddComponent<CarStatus>();
            weapons = testGo.AddComponent<CarWeapons>();
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
        public void TenSlots_MatchWheelDirectionsAndWrap_RejectInvalidSelection()
        {
            Assert.AreEqual(0, WeaponWheelGraphic.SlotAt(Vector2.zero));
            for (int slot = 1; slot <= PrimaryWeaponCatalog.Count; slot++)
                Assert.AreEqual(slot, WeaponWheelGraphic.SlotAt(WeaponWheelGraphic.SlotDirection(slot) * 100f));
            Assert.AreEqual(10, weapons.CyclePrimarySlot(1, -1));
            Assert.AreEqual(1, weapons.CyclePrimarySlot(10, 1));
            Assert.AreEqual(3, weapons.CyclePrimarySlot(1, 1), "Locked machine gun is skipped.");
            Assert.IsFalse(weapons.TrySwitchPrimary((PrimaryWeaponType)10, 0));
            Assert.IsFalse(weapons.TrySwitchPrimary((PrimaryWeaponType)(-1), 0));
            Assert.AreEqual(0, new RacingGame.Core.Sim.DriveCommand { SelectPrimary = 11 }.Clamped().SelectPrimary);
            Assert.AreEqual(10, new RacingGame.Core.Sim.DriveCommand { SelectPrimary = 10 }.Clamped().SelectPrimary);
        }

        [Test]
        public void TenWeapons_FireDistinctProfiles_KeepEachMagazineAndUpgrades()
        {
            var cannon = testGo.AddComponent<CarCannon>();
            weapons.SetPrimaryType(PrimaryWeaponType.Cannon);
            weapons.machineGunUnlocked = true;
            cannon.shellDamage *= 1.5f;
            cannon.fireInterval *= 0.9f;
            var director = RaceDirector.Instance;
            director.ResetRace();
            for (int slot = 0; slot < PrimaryWeaponCatalog.Count; slot++)
            {
                var type = (PrimaryWeaponType)slot;
                var stats = PrimaryWeaponCatalog.Get(type);
                int tick = (slot + 1) * 1000;
                weapons.TrySwitchPrimary(type, tick);
                weapons.ResetHeat();
                int before = director.LiveProjectiles;
                Assert.IsTrue(cannon.TryFire(tick + 100), type.ToString());
                Assert.AreEqual(before + stats.Pellets, director.LiveProjectiles, type.ToString());
                Assert.AreEqual(stats.Damage * 1.5f, cannon.shellDamage, 0.001f);
                Assert.AreEqual(stats.Interval * 0.9f, cannon.fireInterval, 0.001f);
            }
            for (int slot = 0; slot < PrimaryWeaponCatalog.Count; slot++)
            {
                var type = (PrimaryWeaponType)slot;
                weapons.TrySwitchPrimary(type, 20000 + slot);
                int magazine = PrimaryWeaponCatalog.Get(type).Magazine;
                Assert.AreEqual(magazine == 0 ? int.MaxValue : magazine - 1, cannon.Ammo, type.ToString());
            }
            bool freeze = false, jam = false, puncture = false;
            foreach (var projectile in director.GetComponentsInChildren<Projectile>())
            {
                freeze |= projectile.StatusToApply == StatusKind.Frozen;
                jam |= projectile.StatusToApply == StatusKind.Jammed;
                puncture |= projectile.StatusToApply == StatusKind.Punctured;
            }
            Assert.IsTrue(freeze && jam && puncture, "Special weapons spawn projectiles with their status effects.");
            weapons.ResetHeat();
            Assert.IsTrue(cannon.TryFire(21000)); // empty the heavy cannon's second shell
            cannon.SimulationStep(22000); // reloading it must not refill other weapons
            weapons.TrySwitchPrimary(PrimaryWeaponType.Cannon, 23000);
            Assert.AreEqual(4, cannon.Ammo);
            director.ResetRace();
        }

        [Test]
        public void InitialState_HasZeroHeat_AndFullSecondaryAmmo()
        {
            Assert.AreEqual(0f, weapons.CurrentHeat, 1e-4f);
            Assert.IsFalse(weapons.IsOverheated);
            Assert.AreEqual(weapons.secondaryAmmoMax, weapons.SecondaryAmmo);
            Assert.IsTrue(weapons.IsSecondaryReady(0));
        }

        [Test]
        public void HeatBuildup_AndOverheatThreshold_LocksWeapon()
        {
            weapons.heatMax = 100f;
            weapons.overheatThreshold = 100f;
            weapons.overheatRecoveryThreshold = 40f;

            weapons.AddHeat(50f);
            Assert.AreEqual(50f, weapons.CurrentHeat, 1e-4f);
            Assert.IsFalse(weapons.IsOverheated);

            weapons.AddHeat(50f);
            Assert.AreEqual(100f, weapons.CurrentHeat, 1e-4f);
            Assert.IsTrue(weapons.IsOverheated);
        }

        [Test]
        public void CoolingRate_ClearsOverheat_OnlyBelowRecoveryThreshold()
        {
            weapons.heatMax = 100f;
            weapons.overheatThreshold = 100f;
            weapons.overheatRecoveryThreshold = 40f;
            weapons.heatCooldownRate = 20f; // 20 heat per sec

            weapons.AddHeat(100f);
            Assert.IsTrue(weapons.IsOverheated);

            // Cool for 2 seconds -> heat decreases by 40 (to 60)
            weapons.CoolHeat(2f);
            Assert.AreEqual(60f, weapons.CurrentHeat, 1e-4f);
            Assert.IsTrue(weapons.IsOverheated); // Still >= 40, so remains overheated

            // Cool for another 1.1 seconds -> heat decreases by 22 (to 38)
            weapons.CoolHeat(1.1f);
            Assert.AreEqual(38f, weapons.CurrentHeat, 1e-4f);
            Assert.IsFalse(weapons.IsOverheated); // Now recovered!
        }

        [Test]
        public void JammedStatus_BlocksSecondaryFiring()
        {
            weapons.secondaryAmmo = 3;
            status.ApplyStatus(StatusKind.Jammed, 50);
            Assert.IsTrue(status.IsJammed);

            bool fired = weapons.TryFireSecondary(0);
            Assert.IsFalse(fired);
            Assert.AreEqual(3, weapons.SecondaryAmmo);
        }

        [Test]
        public void SecondaryFiring_ConsumesAmmo_AndEnforcesCooldown()
        {
            weapons.secondaryAmmo = 2;
            weapons.secondaryAmmoMax = 2;
            weapons.secondaryCooldown = 3f; // 150 ticks
            weapons.secondaryType = SecondaryWeaponType.Mines;

            bool fired = weapons.TryFireSecondary(10);
            Assert.IsTrue(fired);
            Assert.AreEqual(1, weapons.SecondaryAmmo);
            Assert.IsFalse(weapons.IsSecondaryReady(11));

            // Try firing again before cooldown ticks
            bool firedTooSoon = weapons.TryFireSecondary(20);
            Assert.IsFalse(firedTooSoon);
            Assert.AreEqual(1, weapons.SecondaryAmmo);

            // Advance past cooldown (10 + 150 = 160)
            Assert.IsTrue(weapons.IsSecondaryReady(160));
            bool secondFired = weapons.TryFireSecondary(160);
            Assert.IsTrue(secondFired);
            Assert.AreEqual(0, weapons.SecondaryAmmo);

            // Empty ammo cannot fire even if ready
            Assert.IsFalse(weapons.TryFireSecondary(400));
        }

        [Test]
        public void RefillAll_ResetsHeat_AndRefillsSecondaryAmmo()
        {
            weapons.AddHeat(80f);
            weapons.secondaryAmmo = 0;

            weapons.RefillAll();

            Assert.AreEqual(0f, weapons.CurrentHeat, 1e-4f);
            Assert.IsFalse(weapons.IsOverheated);
            Assert.AreEqual(weapons.secondaryAmmoMax, weapons.SecondaryAmmo);
        }

        [Test]
        public void ZeroAllocations_InWeaponsSimulationStep()
        {
            weapons.AddHeat(50f);

            // Warm up
            for (int i = 0; i < 10; i++)
            {
                weapons.SimulationStep(i);
            }

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 11; i <= 30; i++)
            {
                weapons.SimulationStep(i);
                _ = weapons.CurrentHeat;
                _ = weapons.IsOverheated;
                _ = weapons.SecondaryAmmo;
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after, "SimulationStep of CarWeapons must produce 0 GC allocations.");
        }

        [Test]
        public void CanonicalComposition_AndDefensiveFallback_AttachesCarWeaponsAndStatus()
        {
            var bareCarGo = new GameObject("BareCar");
            _ = bareCarGo.AddComponent<Rigidbody>();
            var bareCannon = bareCarGo.AddComponent<CarCannon>();
            var bareController = bareCarGo.AddComponent<CarController>();
            var bareParticipant = bareCarGo.AddComponent<RaceParticipant>();

            Assert.IsNotNull(bareController.Weapons, "CarController must guarantee CarWeapons is attached");
            Assert.IsNotNull(bareController.Status, "CarController must guarantee CarStatus is attached");
            Assert.IsNotNull(bareParticipant.Weapons, "RaceParticipant must resolve CarWeapons");
            Assert.IsNotNull(bareParticipant.Status, "RaceParticipant must resolve CarStatus");

            Object.DestroyImmediate(bareCarGo);
        }

        [Test]
        public void MachineGun_PrimaryWeaponProfile_OverheatsThroughRuntimeFireAndCoolPath()
        {
            var cannon = testGo.AddComponent<CarCannon>();
            weapons.SetPrimaryType(PrimaryWeaponType.MachineGun);

            Assert.AreEqual(PrimaryWeaponType.MachineGun, weapons.primaryType);
            Assert.AreEqual(0.125f, cannon.fireInterval, 1e-4f);
            Assert.AreEqual(120f, cannon.shellSpeed, 1e-4f);
            Assert.AreEqual(8f, cannon.shellDamage, 1e-4f);
            Assert.AreEqual(0, cannon.magazineSize);
            Assert.AreEqual(2.5f, weapons.heatPerShot, 1e-4f);

            // Exercise the same per-tick path used by CarController: the cannon fires first,
            // then CarWeapons processes heat/cooldown for that tick.
            cannon.SetWantsFire(true);
            int fireTicks = cannon.FireIntervalTicks;
            for (int tick = 0; tick < 40 * fireTicks; tick++)
            {
                cannon.SimulationStep(tick);
                weapons.SimulationStep(tick);
            }

            Assert.AreEqual(100f, weapons.CurrentHeat, 1e-4f);
            Assert.IsTrue(weapons.IsOverheated);

            // Once overheated, the held trigger remains blocked and does not bypass the lock.
            int blockedTick = 40 * fireTicks;
            cannon.SimulationStep(blockedTick);
            weapons.SimulationStep(blockedTick);
            Assert.IsTrue(weapons.IsOverheated);

            // Releasing the trigger resumes deterministic cooling.
            cannon.SetWantsFire(false);
            weapons.SimulationStep(blockedTick + 1);
            Assert.Less(weapons.CurrentHeat, 100f);
        }

        [Test]
        public void SwitchingBetween_CannonAndMachineGun_UpdatesProfilesCorrectly()
        {
            var cannon = testGo.AddComponent<CarCannon>();

            // Cannon profile
            weapons.SetPrimaryType(PrimaryWeaponType.Cannon);
            Assert.AreEqual(0.4f, cannon.fireInterval, 1e-4f);
            Assert.AreEqual(70f, cannon.shellSpeed, 1e-4f);
            Assert.AreEqual(25f, cannon.shellDamage, 1e-4f);
            Assert.AreEqual(5, cannon.magazineSize);
            Assert.AreEqual(15f, weapons.heatPerShot, 1e-4f);

            // Switch to MachineGun
            weapons.SetPrimaryType(PrimaryWeaponType.MachineGun);
            Assert.AreEqual(0.125f, cannon.fireInterval, 1e-4f);
            Assert.AreEqual(120f, cannon.shellSpeed, 1e-4f);
            Assert.AreEqual(8f, cannon.shellDamage, 1e-4f);
            Assert.AreEqual(0, cannon.magazineSize);
            Assert.AreEqual(2.5f, weapons.heatPerShot, 1e-4f);
        }

        [Test]
        public void TrySwitchPrimary_ToMachineGun_IsDeniedUntilBought()
        {
            var cannon = testGo.AddComponent<CarCannon>();
            weapons.SetPrimaryType(PrimaryWeaponType.Cannon);
            int denied = 0;
            weapons.PrimarySwitchDenied += () => denied++;

            Assert.IsFalse(weapons.TrySwitchPrimary(PrimaryWeaponType.MachineGun, 10));
            Assert.AreEqual(PrimaryWeaponType.Cannon, weapons.primaryType);
            Assert.AreEqual(5, cannon.magazineSize);
            Assert.AreEqual(1, denied);

            weapons.machineGunUnlocked = true;
            Assert.IsTrue(weapons.TrySwitchPrimary(PrimaryWeaponType.MachineGun, 10));
            Assert.AreEqual(PrimaryWeaponType.MachineGun, weapons.primaryType);
            Assert.IsFalse(weapons.TrySwitchPrimary(PrimaryWeaponType.MachineGun, 11), "already out");
        }

        [Test]
        public void TrySwitchPrimary_KeepsUpgradesAndParksTheCannonMagazine()
        {
            var cannon = testGo.AddComponent<CarCannon>();
            weapons.SetPrimaryType(PrimaryWeaponType.Cannon);
            weapons.machineGunUnlocked = true;
            weapons.machineGunHeatFactor = 0.8f;
            cannon.shellDamage *= 1.5f;   // what PlayerProgression.ApplyTo does with upgrades and build
            cannon.fireInterval *= 0.9f;

            Assert.IsTrue(cannon.TryFire(0));
            Assert.IsTrue(cannon.TryFire(cannon.FireIntervalTicks));
            Assert.AreEqual(3, cannon.Ammo);

            int swapTick = 100;
            Assert.IsTrue(weapons.TrySwitchPrimary(PrimaryWeaponType.MachineGun, swapTick));
            Assert.AreEqual(8f * 1.5f, cannon.shellDamage, 1e-4f);
            Assert.AreEqual(0.125f * 0.9f, cannon.fireInterval, 1e-4f);
            Assert.AreEqual(0, cannon.magazineSize);
            Assert.AreEqual(2.5f * 0.8f, weapons.heatPerShot, 1e-4f);
            int ready = swapTick + SimulationDriver.SecondsToTicks(weapons.primarySwitchSeconds);
            Assert.IsFalse(cannon.TryFire(ready - 1), "the swap holds fire for a moment");
            Assert.IsTrue(cannon.TryFire(ready));

            Assert.IsTrue(weapons.TrySwitchPrimary(PrimaryWeaponType.Cannon, 200));
            Assert.AreEqual(25f * 1.5f, cannon.shellDamage, 1e-4f);
            Assert.AreEqual(0.4f * 0.9f, cannon.fireInterval, 1e-4f);
            Assert.AreEqual(3, cannon.Ammo, "swapping back does not reload the cannon");
            Assert.AreEqual(15f, weapons.heatPerShot, 1e-4f);
        }

        [Test]
        public void DriveCommand_SelectPrimary_SwapsThroughCarControllerSimulationStep()
        {
            var director = RaceDirector.Instance;
            director.ResetRace();

            var carGo = new GameObject("TestCar_Swap");
            _ = carGo.AddComponent<Rigidbody>();
            var controller = carGo.AddComponent<CarController>();
            controller.playerControlled = false;
            controller.SetInputSource(null);
            _ = carGo.AddComponent<CarCannon>();
            var carWeapons = controller.Weapons;
            carWeapons.machineGunUnlocked = true;
            director.RegisterCar(controller);

            controller.ApplyCommand(new RacingGame.Core.Sim.DriveCommand { Tick = 1, SelectPrimary = 2 });
            controller.SimulationStep(1);
            Assert.AreEqual(PrimaryWeaponType.MachineGun, carWeapons.primaryType);

            controller.ApplyCommand(new RacingGame.Core.Sim.DriveCommand { Tick = 2, SelectPrimary = 1 });
            controller.SimulationStep(2);
            Assert.AreEqual(PrimaryWeaponType.Cannon, carWeapons.primaryType);

            for (int slot = 3; slot <= PrimaryWeaponCatalog.Count; slot++)
            {
                controller.ApplyCommand(new RacingGame.Core.Sim.DriveCommand { Tick = slot, SelectPrimary = slot });
                controller.SimulationStep(slot);
                Assert.AreEqual((PrimaryWeaponType)(slot - 1), carWeapons.primaryType);
            }
            director.ResetRace();
            Object.DestroyImmediate(carGo);
        }

        [Test]
        public void DriveCommand_Secondary_FiresSecondaryWeaponThroughCarControllerSimulationStep()
        {
            var director = RaceDirector.Instance;
            director.ResetRace();

            var carGo = new GameObject("TestCar_Sim");
            _ = carGo.AddComponent<Rigidbody>();
            var controller = carGo.AddComponent<CarController>();
            controller.playerControlled = false;
            controller.SetInputSource(null);
            var carWeapons = controller.Weapons;
            var carStatus = controller.Status;
            director.RegisterCar(controller);

            carWeapons.secondaryType = SecondaryWeaponType.Mines;
            carWeapons.secondaryAmmo = 3;

            // Tick with Secondary = false
            var cmdIdle = new RacingGame.Core.Sim.DriveCommand { Tick = 1, Secondary = false };
            controller.ApplyCommand(cmdIdle);
            controller.SimulationStep(1);

            Assert.AreEqual(3, carWeapons.SecondaryAmmo);
            Assert.AreEqual(0, director.LiveMines);

            // Tick with Secondary = true
            var cmdSecondary = new RacingGame.Core.Sim.DriveCommand { Tick = 2, Secondary = true };
            controller.ApplyCommand(cmdSecondary);
            controller.SimulationStep(2);

            Assert.AreEqual(2, carWeapons.SecondaryAmmo);
            Assert.AreEqual(1, director.LiveMines);

            director.ResetRace();
            Object.DestroyImmediate(carGo);
        }

        [Test]
        public void SingleTickDeduplication_SimulationStepCalledMultipleTimes_DoesNotDoubleStep()
        {
            weapons.AddHeat(50f);
            weapons.heatCooldownRate = 20f; // 0.4 heat per tick at 0.02s

            // Call SimulationStep multiple times on tick 10
            weapons.SimulationStep(10);
            float heatAfterFirst = weapons.CurrentHeat;
            weapons.SimulationStep(10);
            float heatAfterSecond = weapons.CurrentHeat;

            Assert.AreEqual(heatAfterFirst, heatAfterSecond, 1e-4f);
        }
    }
}
