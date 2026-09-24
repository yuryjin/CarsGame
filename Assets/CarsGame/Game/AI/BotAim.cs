using UnityEngine;

namespace RacingGame
{
    /// <summary>What an AI gunner commits to when it lines up a shot. The cannon's lead solver stays exact.</summary>
    public readonly struct BotAimDecision
    {
        public readonly float TelegraphSeconds;
        public readonly bool StripLateralLead;
        public readonly int BurstShots;

        public BotAimDecision(float telegraphSeconds, bool stripLateralLead, int burstShots)
        {
            TelegraphSeconds = telegraphSeconds;
            StripLateralLead = stripLateralLead;
            BurstShots = burstShots;
        }
    }

    /// <summary>
    /// Bot aiming skill. A calm target gets the full lead and a short warning; a target already
    /// changing lanes loses only the sideways part of the lead and a longer warning. The shell
    /// still flies where the barrel points.
    /// </summary>
    public static class BotAim
    {
        /// <summary>Sideways offset rate (m/s, shooter-right) treated as an in-progress lane change.</summary>
        public const float LaneChangeRate = 2.5f;
        public const float LaneChangeTelegraphScale = 1.5f;
        /// <summary>Long enough to move about a car's width once the barrel has stopped tracking.</summary>
        public const float MinTelegraphSeconds = 0.35f;

        public static float BaseTelegraph(string tierId)
        {
            if (tierId == "easy") return 0.55f;
            if (tierId == "hard") return 0.35f;
            return 0.42f;
        }

        public static BotAimDecision Evaluate(float lateralRate, float baseTelegraph, bool machineGun, bool hardTier)
        {
            bool laneChange = Mathf.Abs(lateralRate) >= LaneChangeRate;
            // A base of 0 still means fire on sight (tests and rules that skip the warning).
            float scaled = baseTelegraph * (laneChange ? LaneChangeTelegraphScale : 1f);
            float telegraph = baseTelegraph <= 0f ? 0f : Mathf.Max(MinTelegraphSeconds, scaled);
            int burst = 1;
            if (machineGun) burst = hardTier && !laneChange ? 3 : 2;
            return new BotAimDecision(telegraph, laneChange, burst);
        }
    }
}
