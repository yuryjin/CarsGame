using System;
using System.Collections.Generic;

namespace RacingGame.Core.Sim
{
    public struct ArenaPoints
    {
        public int Speed, Hits, Knockouts, Centre, Survival;
        public int Total => Speed + Hits + Knockouts + Centre + Survival;
    }

    public sealed class ArenaScore
    {
        public enum HeatChange { None, Chain, Slowed, Death }
        public const int Share = 200;
        public const string Board = "arena_points_v1";
        private readonly Dictionary<int, int> damageAt = new Dictionary<int, int>();
        private readonly int tickRate, matchTicks;
        private int speedTicks, centreTicks, lifeTicks, longestLife, slowTicks;
        private int hits, knockouts;
        public int Multiplier { get; private set; } = 1;
        public int ComboStep { get; private set; }
        public int Assists { get; private set; }
        public HeatChange Change { get; private set; }
        private int changeTicks;

        public ArenaScore(int tickRate, int matchTicks)
        {
            this.tickRate = Math.Max(1, tickRate);
            this.matchTicks = Math.Max(1, matchTicks);
        }

        public ArenaPoints Points => new ArenaPoints
        {
            Speed = Math.Min(Share, speedTicks / tickRate),
            Hits = hits,
            Knockouts = knockouts,
            Centre = Math.Min(Share, centreTicks / tickRate),
            Survival = Math.Min(Share, longestLife * Share / matchTicks)
        };

        public void Step(float speedKmh, bool cornering, bool nearCentre, bool drifting)
        {
            if (changeTicks > 0 && --changeTicks == 0) Change = HeatChange.None;
            longestLife = Math.Max(longestLife, ++lifeTicks);
            if (speedKmh >= 70f) speedTicks += cornering || drifting ? 3 : 2;
            if (nearCentre && speedKmh >= 20f) centreTicks += 2;
            if (speedKmh >= 20f || drifting) slowTicks = 0;
            else if (++slowTicks >= 3 * tickRate)
            {
                if (Multiplier > 1) SetMultiplier(Multiplier - 1, HeatChange.Slowed);
                ComboStep = slowTicks = 0;
            }
        }

        public void DriftBoosted() { if (ComboStep == 0) ComboStep = 1; }

        public void Hit(bool collision, float speedKmh)
        {
            if (!collision && ComboStep == 1) ComboStep = 2;
            else if (collision && speedKmh >= 70f && ComboStep == 2)
            {
                if (Multiplier < 3) SetMultiplier(Multiplier + 1, HeatChange.Chain);
                ComboStep = 0;
            }
            hits = Math.Min(Share, hits + 5 * Multiplier);
        }

        public void Knockout(bool assist)
        {
            if (assist) Assists++;
            knockouts = Math.Min(Share, knockouts + (assist ? 20 : 40) * Multiplier);
        }

        public void DamagedBy(int carId, int tick) => damageAt[carId] = tick;

        public int RecentAttacker(int tick, int except = -1)
        {
            int bestTick = tick - 5 * tickRate, source = -1;
            foreach (var hit in damageAt)
            {
                if (hit.Key == except || hit.Value < bestTick) continue;
                if (hit.Value > bestTick || source < 0 || hit.Key < source)
                { source = hit.Key; bestTick = hit.Value; }
            }
            return source;
        }

        public void Died()
        {
            damageAt.Clear();
            lifeTicks = ComboStep = slowTicks = 0;
            if (Multiplier > 1) SetMultiplier(1, HeatChange.Death);
        }

        private void SetMultiplier(int value, HeatChange reason)
        {
            Multiplier = value;
            Change = reason;
            changeTicks = 2 * tickRate;
        }

        public void Finish(bool undefeated)
        {
            if (undefeated) longestLife = matchTicks;
        }
    }
}
