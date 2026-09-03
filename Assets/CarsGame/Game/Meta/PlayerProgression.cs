using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame
{
    /// <summary>Applies the garage upgrades of the save to the freshly spawned player car.</summary>
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
    }
}
