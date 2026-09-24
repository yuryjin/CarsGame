using System;
using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame
{
    /// <summary>Applies the garage upgrades and equipped weapons of the save to the freshly spawned player car.</summary>
    public static class PlayerProgression
    {
        public static void ApplyTo(CarController car, CarHealth health, CarCannon cannon, int[] levels, CarBuild build = CarBuild.Balanced)
        {
            var fx = UpgradeCatalog.For(levels);
            var tuning = BuildCatalog.For(build);
            if (car != null)
            {
                car.maxSpeed *= fx.MaxSpeedFactor * tuning.MaxSpeedFactor;
                car.grip *= fx.GripFactor * tuning.GripFactor;
                car.motorTorque *= fx.TorqueFactor * tuning.TorqueFactor;
            }
            if (health != null) health.SetMaxHealth(health.maxHealth * fx.MaxHealthFactor * tuning.MaxHealthFactor, true);
            if (cannon != null)
            {
                cannon.shellDamage *= fx.DamageFactor * tuning.DamageFactor;
                cannon.fireInterval *= fx.FireIntervalFactor * tuning.FireIntervalFactor;
            }
        }

        public static void SelectBuild(Core.Save.ProgressData progress, CarBuild build)
        {
            build = (CarBuild)Mathf.Clamp((int)build, 0, 3);
            progress.buildPreset = (int)build;
            progress.equippedPrimary = build == CarBuild.Firepower && WeaponCatalog.Level(progress.weapons, 2) > 0 ? 1 : 0;
            int secondary = build == CarBuild.Speed ? 2 : build == CarBuild.Balanced ? 0 : 1;
            progress.equippedSecondary = secondary > 0 && WeaponCatalog.Level(progress.weapons, secondary - 1) > 0 ? secondary : 0;
        }

        public static void ApplyWeapons(CarWeapons weapons, int[] weaponLevels, int equippedSecondary, int equippedPrimary = 0)
        {
            if (weapons == null) return;

            int mgLevel = WeaponCatalog.Level(weaponLevels, (int)WeaponKind.MachineGun);
            weapons.machineGunUnlocked = mgLevel > 0;   // lets the driver swap to it mid-race (key 2)
            weapons.machineGunHeatFactor = WeaponCatalog.For(mgLevel).HeatFactor;
            var primaryType = (PrimaryWeaponType)Mathf.Clamp(equippedPrimary, 0, 1);
            if (primaryType == PrimaryWeaponType.MachineGun && mgLevel == 0) primaryType = PrimaryWeaponType.Cannon;
            weapons.SetPrimaryType(primaryType);

            var secondaryType = (SecondaryWeaponType)Mathf.Clamp(equippedSecondary, 0, 2);
            if (secondaryType != SecondaryWeaponType.None && WeaponCatalog.Level(weaponLevels, (int)secondaryType - 1) == 0) secondaryType = SecondaryWeaponType.None;
            weapons.secondaryType = secondaryType;

            if (secondaryType == SecondaryWeaponType.Mines)
            {
                int lvl = WeaponCatalog.Level(weaponLevels, (int)WeaponKind.Mines);
                var fx = WeaponCatalog.For(lvl);
                weapons.secondaryAmmoMax = Mathf.Max(1, Mathf.RoundToInt(3 * fx.AmmoFactor));
                weapons.secondaryCooldown = 3f * fx.CooldownFactor;
                weapons.RefillSecondaryAmmo();
            }
            else if (secondaryType == SecondaryWeaponType.FreezeLauncher)
            {
                int lvl = WeaponCatalog.Level(weaponLevels, (int)WeaponKind.FreezeLauncher);
                var fx = WeaponCatalog.For(lvl);
                weapons.secondaryAmmoMax = Mathf.Max(1, Mathf.RoundToInt(3 * fx.AmmoFactor));
                weapons.secondaryCooldown = 3f * fx.CooldownFactor;
                weapons.RefillSecondaryAmmo();
            }
            else
            {
                weapons.secondaryAmmoMax = 0;
                weapons.RefillSecondaryAmmo();
            }
        }
    }
}
