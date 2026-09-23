using UnityEngine;

namespace RacingGame
{
    public enum EventMode { Race, Arena }

    /// <summary>One entry of the race-select screen. Rules vary per event so the single track stays fresh.</summary>
    [CreateAssetMenu(menuName = "Racing/Race Event", fileName = "Event")]
    public sealed class RaceEventDefinition : ScriptableObject
    {
        public string eventId = "e1";
        public EventMode mode;
        [Tooltip("Localization key of the display name.")]
        public string nameKey = "events.e1.name";
        [Range(1, 20)] public int laps = 3;
        [Tooltip("-1 = take the AI count from the difficulty tier.")]
        public int aiCountOverride = -1;
        public bool reversed;
        [Tooltip("Legacy: night look when no sky preset is assigned.")] public bool night;
        [Tooltip("Time-of-day look for this event (null = day / legacy night flag).")] public RacingGame.World.SkyPreset sky;
        [Range(0.5f, 3f)] public float damageMultiplier = 1f;
        [Range(0, 20)] public int pickupCount = 9;
        [Tooltip("Event that must be finished in the top 3 before this one unlocks (empty = always open).")]
        public string unlockAfterEventId = "";
        public int order;

        [Header("3 Stars Goals")]
        [Tooltip("Target position for Star 1 (default 3 = top 3).")]
        public int star1TargetPosition = 3;
        [Tooltip("Target kills for Star 2 (default 2).")]
        public int star2TargetKills = 2;
        [Tooltip("Require 0 deaths for Star 3.")]
        public bool star3ZeroDeaths = true;

        public int StarMask(RaceResultEntry me)
        {
            if (me == null) return 0;
            return StarMask(me.Finished, me.Position, me.Kills, me.Deaths);
        }

        public int StarMask(bool finished, int position, int kills, int deaths)
        {
            int mask = 0;
            if (finished && position <= (star1TargetPosition > 0 ? star1TargetPosition : 3)) mask |= 1;
            if (kills >= (star2TargetKills > 0 ? star2TargetKills : 2)) mask |= 2;
            if (finished && (!star3ZeroDeaths || deaths == 0)) mask |= 4;
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
