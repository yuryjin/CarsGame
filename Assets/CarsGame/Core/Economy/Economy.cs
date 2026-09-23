using System;

namespace RacingGame.Core.Economy
{
    /// <summary>
    /// Coin payout for one race. Pure and unit-tested. Tuned so a normal-tier mid-field finish
    /// pays 100–200 and the first garage upgrade (150) arrives after 1–2 races.
    /// Kill bonuses shrink for repeated kills of the same victim (no farming a wreck).
    /// </summary>
    public static class Economy
    {
        public static readonly int[] PositionPayout = { 300, 220, 160, 110, 80, 60, 50, 40 };
        public const int KillBonus = 25;
        public const int DnfPayout = 30;
        /// <summary>A finished first race always pays at least the first upgrade, so the garage opens at once.</summary>
        public const int FirstRaceMinimum = 150;

        public static int WithFirstRaceFloor(int reward, bool finished, bool firstRace) =>
            finished && firstRace ? Math.Max(reward, FirstRaceMinimum) : reward;

        public static int RaceReward(int position, bool finished, int[] killsPerVictim, float tierMultiplier) => RaceReward(position, finished, finished ? 1 : 0, 1, killsPerVictim, tierMultiplier);

        /// <summary>DNF pays only for laps actually completed (no idle/retry farming); zero laps = zero coins.</summary>
        public static int RaceReward(int position, bool finished, int lapsCompleted, int lapsRequired, int[] killsPerVictim, float tierMultiplier)
        {
            if (!finished && lapsCompleted <= 0) return 0;
            int coins = finished ? PositionPayout[Math.Clamp(position, 1, PositionPayout.Length) - 1] : (int)Math.Round(DnfPayout * Math.Clamp(lapsCompleted / (double)Math.Max(1, lapsRequired), 0, 1));
            if (killsPerVictim != null)
            {
                foreach (int k in killsPerVictim)
                {
                    // 25, 12, 6, 3 … for the 1st, 2nd, 3rd kill of the same rival.
                    int bonus = KillBonus;
                    for (int i = 0; i < k; i++) { coins += bonus; bonus = Math.Max(1, bonus / 2); }
                }
            }
            return (int)Math.Round(coins * Math.Max(0.1f, tierMultiplier));
        }
    }

    public enum UpgradeKind { Engine = 0, Armor = 1, Cannon = 2 }

    /// <summary>Three upgrade lines with a trade-off each, so buying is a choice rather than a stat ladder.</summary>
    public static class UpgradeCatalog
    {
        public const int MaxLevel = 4;
        public const int BaseCost = 150;
        public const float CostGrowth = 1.6f;

        public static int Cost(int currentLevel) => currentLevel >= MaxLevel ? int.MaxValue : (int)Math.Round(BaseCost * Math.Pow(CostGrowth, currentLevel));

        public readonly struct Effects
        {
            public readonly float MaxSpeedFactor, GripFactor, MaxHealthFactor, TorqueFactor, DamageFactor, FireIntervalFactor;
            public Effects(float maxSpeed, float grip, float maxHealth, float torque, float damage, float fireInterval)
            { MaxSpeedFactor = maxSpeed; GripFactor = grip; MaxHealthFactor = maxHealth; TorqueFactor = torque; DamageFactor = damage; FireIntervalFactor = fireInterval; }
        }

        /// <summary>Combined multipliers for a set of levels [engine, armor, cannon].</summary>
        public static Effects For(int[] levels)
        {
            int e = Level(levels, 0), a = Level(levels, 1), c = Level(levels, 2);
            return new Effects(
                maxSpeed: 1f + 0.04f * e,        // engine: faster …
                grip: 1f - 0.02f * e,            // … but slides more
                maxHealth: 1f + 0.15f * a,       // armor: tougher …
                torque: 1f - 0.03f * a,          // … but heavier off the line
                damage: 1f + 0.12f * c,          // cannon: hits harder …
                fireInterval: 1f + 0.06f * c);   // … but fires slower
        }

        public static int Level(int[] levels, int kind) => levels != null && kind < levels.Length ? Math.Clamp(levels[kind], 0, MaxLevel) : 0;

        /// <summary>Attempt a purchase against a balance. Returns the new level or -1.</summary>
        public static bool TryBuy(int[] levels, UpgradeKind kind, long coins, out int cost)
        {
            int lvl = Level(levels, (int)kind);
            cost = Cost(lvl);
            if (lvl >= MaxLevel || coins < cost) return false;
            levels[(int)kind] = lvl + 1;
            return true;
        }
    }

    public enum WeaponKind { Mines = 0, FreezeLauncher = 1, MachineGun = 2 }

    /// <summary>Purchasable and upgradable weapon modules for secondary and primary slots.</summary>
    public static class WeaponCatalog
    {
        public const int MaxLevel = 3;
        public const int BaseCost = 200;
        public const float CostGrowth = 1.5f;

        public static int Cost(int currentLevel) => currentLevel >= MaxLevel ? int.MaxValue : (int)Math.Round(BaseCost * Math.Pow(CostGrowth, currentLevel));

        public readonly struct WeaponEffects
        {
            public readonly float AmmoFactor, CooldownFactor, DamageFactor, HeatFactor;
            public WeaponEffects(float ammo, float cooldown, float damage, float heat)
            {
                AmmoFactor = ammo;
                CooldownFactor = cooldown;
                DamageFactor = damage;
                HeatFactor = heat;
            }
        }

        public static WeaponEffects For(int level)
        {
            int lvl = Math.Clamp(level, 0, MaxLevel);
            if (lvl == 0) return new WeaponEffects(1f, 1f, 1f, 1f);
            int upg = lvl - 1;
            return new WeaponEffects(
                ammo: 1f + 0.34f * upg,
                cooldown: 1f - 0.15f * upg,
                damage: 1f + 0.20f * upg,
                heat: 1f - 0.15f * upg
            );
        }

        public static int Level(int[] levels, int kind) => levels != null && kind < levels.Length ? Math.Clamp(levels[kind], 0, MaxLevel) : 0;

        /// <summary>Attempt a purchase against a balance. Returns whether purchase succeeded.</summary>
        public static bool TryBuy(int[] levels, WeaponKind kind, long coins, out int cost)
        {
            int lvl = Level(levels, (int)kind);
            cost = Cost(lvl);
            if (lvl >= MaxLevel || coins < cost) return false;
            levels[(int)kind] = lvl + 1;
            return true;
        }
    }
}
