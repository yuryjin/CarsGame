using UnityEngine;

namespace RacingGame
{
    /// <summary>One entry of the race-select screen. Rules vary per event so the single track stays fresh.</summary>
    [CreateAssetMenu(menuName = "Racing/Race Event", fileName = "Event")]
    public sealed class RaceEventDefinition : ScriptableObject
    {
        public string eventId = "e1";
        [Tooltip("Localization key of the display name.")]
        public string nameKey = "events.e1.name";
        [Range(1, 20)] public int laps = 3;
        [Tooltip("-1 = take the AI count from the difficulty tier.")]
        public int aiCountOverride = -1;
        public bool reversed;
        public bool night;
        [Range(0.5f, 3f)] public float damageMultiplier = 1f;
        [Range(0, 20)] public int pickupCount = 9;
        [Tooltip("Event that must be finished in the top 3 before this one unlocks (empty = always open).")]
        public string unlockAfterEventId = "";
        public int order;
    }
}
