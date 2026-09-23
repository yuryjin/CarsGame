using System;
using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame
{
    /// <summary>Applies the garage upgrades and equipped weapons of the save to the freshly spawned player car.</summary>
    public static class PlayerProgression
    {
        public static void ApplyTo(CarController car, CarHealth health, CarCannon cannon, int[] levels)
        {
            var fx = UpgradeCatalog.For(levels);
            if (car != null)
            {
                car.maxSpeed *= fx.MaxSpeedFactor;
                car.grip *= fx.GripFactor;
                car.motorTorque *= fx.TorqueFactor;
            }
            if (health != null) health.SetMaxHealth(health.maxHealth * fx.MaxHealthFactor, true);
            if (cannon != null)
            {
                cannon.shellDamage *= fx.DamageFactor;
                cannon.fireInterval *= fx.FireIntervalFactor;
            }
        }

        public static void ApplyWeapons(CarWeapons weapons, int[] weaponLevels, int equippedSecondary, int equippedPrimary = 0)
        {
            if (weapons == null) return;

            var primaryType = (PrimaryWeaponType)Mathf.Clamp(equippedPrimary, 0, 1);
            weapons.SetPrimaryType(primaryType);

            var secondaryType = (SecondaryWeaponType)Mathf.Clamp(equippedSecondary, 0, 2);
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

            if (primaryType == PrimaryWeaponType.MachineGun)
            {
                int lvl = WeaponCatalog.Level(weaponLevels, (int)WeaponKind.MachineGun);
                var fx = WeaponCatalog.For(lvl);
                weapons.heatCooldownRate = 20f / fx.HeatFactor;
                weapons.heatPerShot = 2.5f * fx.HeatFactor;
            }
        }
    }
}
