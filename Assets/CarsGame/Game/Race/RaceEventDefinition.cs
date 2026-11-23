using UnityEngine;

namespace RacingGame
{
    public enum MountainFork { Bridge = 0, Quarry = 1, IceStation = 2, Rooftops = 3, Volcano = 4 }
    public enum FinishGoal { NoDeaths, WinWithoutRepair, BeatTime, CleanRun }
    public enum SkillGoal { Kills, DriftBoosts, Shortcuts, CeilingBoost, CleanFinalLap, BombPasses }

    /// <summary>Mission events supply their own map and finish conditions.</summary>
    public enum EventMode { Race, Arena, Mission }
    public enum CircuitFormat { Standard, Elimination, Pursuit, Relay }

    /// <summary>One entry of the race-select screen. Rules vary per event so the single track stays fresh.</summary>
    [CreateAssetMenu(menuName = "Racing/Race Event", fileName = "Event")]
    public sealed class RaceEventDefinition : ScriptableObject
    {
        public AiDriverProfile featuredRival;
        // Runtime-only lesson configuration; authored campaign events always retain their race rules.
        public bool IsPractice { get; internal set; }
        public bool IsCustom { get; internal set; }
        public string eventId = "e1";
        public EventMode mode;
        [Tooltip("Match duration in seconds for Arena mode.")]
        public float matchDuration = 75f;
        [Tooltip("Localization key of the display name.")]
        public string nameKey = "events.e1.name";
        [Range(1, 20)] public int laps = 3;
        [Tooltip("Approximate race duration in seconds on the stock Classic at Normal difficulty.")]
        [Min(0)] public int referenceSeconds;
        [Tooltip("-1 = take the AI count from the difficulty tier.")]
        public int aiCountOverride = -1;
        public CircuitFormat format;
        public string RelayFirst { get; internal set; }
        public string RelaySecond { get; internal set; }
        [Tooltip("Rival driver profiles keep their selected top speed at every gap.")]
        public bool fixedRivalPace;
        public bool reversed;
        [Tooltip("Road over an abyss: broken barriers on lap 1, no barriers on lap 2, narrow road on lap 3.")]
        public bool abyssTrack;
        [Tooltip("Suspended space circuit with circular boost gates.")]
        public bool spaceTrack;
        [Tooltip("Star Ring: its own space circuit (bridge jump, boost pad, drivable tunnel walls, turrets).")]
        public bool starRingTrack;
        [Tooltip("Post-apocalyptic wasteland circuit with a broken bridge and drivable tunnel.")]
        public bool wastelandTrack;
        [Tooltip("Side route on the mountain circuit.")]
        public MountainFork mountainFork;
        [Tooltip("Ramming shoves the other car this much harder (1 = plain physics).")]
        [Range(1f, 4f)] public float ramPush = 1f;
        [Tooltip("Legacy: night look when no sky preset is assigned.")] public bool night;
        [Tooltip("Time-of-day look for this event (null = day / legacy night flag).")] public RacingGame.World.SkyPreset sky;
        [Range(0.5f, 3f)] public float damageMultiplier = 1f;
        [Range(0, 20)] public int pickupCount = 9;
        [Tooltip("Mines the map lays at the start and relays after each blast. -1 = mode default (arena 10, race none).")]
        [Range(-1, 40)] public int mineFieldCount = -1;
        public const int ArenaMineFieldDefault = 10;
        public int MineFieldCount => mineFieldCount >= 0 ? mineFieldCount : mode == EventMode.Arena ? ArenaMineFieldDefault : 0;
        [Tooltip("Event that must be finished in the top 3 before this one unlocks (empty = always open).")]
        public string unlockAfterEventId = "";
        [Tooltip("An alternative to the prerequisite podium. Zero uses only the podium rule.")]
        [Min(0)] public int unlockStars;
        public int order;

        public bool OpenedByStars(int totalStars) => unlockStars > 0 && totalStars >= unlockStars;

        [Header("3 Stars Goals")]
        [Tooltip("Target position for Star 1 (default 3 = top 3).")]
        public int star1TargetPosition = 3;
        [Tooltip("Target kills for Star 2 (default 2).")]
        public int star2TargetKills = 2;
        public SkillGoal skillGoal;
        [Min(1)] public int skillTarget = 1;
        [Tooltip("Require 0 deaths for Star 3.")]
        public bool star3ZeroDeaths = true;
        public FinishGoal finishGoal;
        [Tooltip("Rule twist (mines only, one life …) applied to the cars when the race starts.")]
        public RacingGame.Core.Daily.RaceTwist twist;
        [Min(1f)] public float targetSeconds = 210f;

        public int RequiredPosition => star1TargetPosition > 0 ? star1TargetPosition : 3;
        public int RequiredSkillCount => skillGoal == SkillGoal.Kills
            ? (star2TargetKills > 0 ? star2TargetKills : 2) : Mathf.Max(1, skillTarget);

        public int StarMask(RaceResultEntry me)
        {
            if (me == null) return 0;
            int mask = StarMask(me.Finished, me.Position, me.Kills, me.Deaths, me.RepairsUsed, me.TotalTime, me.Resets);
            if (skillGoal != SkillGoal.Kills)
            {
                mask &= ~2;
                if (SkillCount(me) >= RequiredSkillCount) mask |= 2;
            }
            return mask;
        }

        public int SkillCount(RaceResultEntry me)
        {
            if (me == null) return 0;
            switch (skillGoal)
            {
                case SkillGoal.DriftBoosts: return me.DriftBoosts;
                case SkillGoal.Shortcuts: return me.Shortcuts;
                case SkillGoal.CeilingBoost: return me.CeilingBoosts;
                case SkillGoal.CleanFinalLap: return me.Finished && me.CleanFinalLap ? 1 : 0;
                case SkillGoal.BombPasses: return me.BombPasses;
                default: return me.Kills;
            }
        }

        public int StarMask(bool finished, int position, int kills, int deaths, int repairsUsed = 0, double totalTime = double.PositiveInfinity, int resets = 0)
        {
            int mask = 0;
            if (finished && position <= RequiredPosition) mask |= 1;
            if (skillGoal == SkillGoal.Kills && kills >= RequiredSkillCount) mask |= 2;
            bool goalMet = finishGoal == FinishGoal.WinWithoutRepair ? position == 1 && repairsUsed == 0
                : finishGoal == FinishGoal.BeatTime ? totalTime > 0 && totalTime <= targetSeconds
                : finishGoal == FinishGoal.CleanRun ? deaths == 0 && resets == 0
                : !star3ZeroDeaths || deaths == 0;
            if (finished && goalMet) mask |= 4;
            return mask;
        }

        public static int CountStars(int mask)
        {
            int count = 0;
            if ((mask & 1) != 0) count++;
            if ((mask & 2) != 0) count++;
            if ((mask & 4) != 0) count++;
            return count;
        }
    }
}
