using UnityEngine;
using RacingGame.Core.Daily;

namespace RacingGame
{
    /// <summary>
    /// Race-time effects of an event's <see cref="RaceTwist"/> on the spawned field. Damage, laps, sky and
    /// pickups are plain event fields; this covers the rules that change the cars themselves.
    /// Applied once per race after the field spawns (before upgrades would be wrong: the flow calls it after).
    /// </summary>
    public static class EventRules
    {
        public const int MinesOnlyAmmo = 5;
        public const float MinesOnlyCooldown = 2f;
        public const int ArenaChargeAmmo = 2;
        public const float ArenaChargeCooldown = 4f;

        public static void Apply(RaceTwist twist, RaceManager race)
        {
            if (race == null) return;
            bool arena = race.IsArenaMode;
            foreach (var p in race.Participants)
            {
                if (p == null) continue;
                if (twist == RaceTwist.HotBomb)
                {
                    if (p.Car.Cannon != null) p.Car.Cannon.locked = true;
                    p.Weapons.secondaryType = SecondaryWeaponType.None;
                    if (p.Health != null) { p.Health.respawnOnDeath = true; p.Health.damagePerImpactSpeed = 0f; }
                }
                if (arena && twist != RaceTwist.MinesOnly && twist != RaceTwist.HotBomb) ArenaCharges(p);
                if (twist == RaceTwist.MinesOnly) MinesOnly(p);
                if (twist == RaceTwist.OneLife && p.Health != null) p.Health.respawnOnDeath = false;
            }
        }

        // No cannons: the barrel is hidden, the car never ticks the cannon, and everyone lays mines instead.
        private static void MinesOnly(RaceParticipant p)
        {
            var cannon = p.Car != null ? p.Car.Cannon : null;
            if (cannon != null)
            {
                cannon.locked = true;
                if (cannon.barrel != null) cannon.barrel.gameObject.SetActive(false);
            }
            var w = p.Weapons;
            if (w == null) return;
            w.secondaryType = SecondaryWeaponType.Mines;
            w.secondaryAmmoMax = MinesOnlyAmmo;
            w.secondaryCooldown = MinesOnlyCooldown;
            w.RefillSecondaryAmmo();
            // Mines are the only weapon: drop them on anyone in the mirror, not just a car glued to the bumper.
            var gunner = p.GetComponent<AiGunner>();
            if (gunner != null) { gunner.mineDropMin = 6f; gunner.mineDropMax = 45f; gunner.mineDropLateral = 6f; }
        }

        // Arena: whoever brought no secondary weapon gets planted charges, so the brawl has traps on both sides.
        private static void ArenaCharges(RaceParticipant p)
        {
            var w = p.Weapons;
            if (w == null || w.secondaryType != SecondaryWeaponType.None) return;
            w.secondaryType = SecondaryWeaponType.RemoteCharge;
            w.secondaryAmmoMax = ArenaChargeAmmo;
            w.secondaryCooldown = ArenaChargeCooldown;
            w.RefillSecondaryAmmo();
        }

        /// <summary>True when this car can still fire its primary (the HUD hides the ammo panel otherwise).</summary>
        public static bool PrimaryEnabled(CarController car) => car != null && car.Cannon != null && !car.Cannon.locked;
    }
}
