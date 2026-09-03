using UnityEngine;

namespace RacingGame
{
    /// <summary>Everything that changes between Easy / Normal / Hard.</summary>
    [CreateAssetMenu(menuName = "Racing/Difficulty Tier", fileName = "Difficulty")]
    public sealed class DifficultyTier : ScriptableObject
    {
        public string tierId = "normal";
        [Range(1, 7)] public int aiCount = 3;
        public AiDriverProfile[] profiles;
        [Tooltip("0 = no rubber-banding, 1 = full ±20% top-speed assistance.")]
        [Range(0f, 1f)] public float rubberBandStrength = 0.6f;
        [Tooltip("Seconds between AI shots.")]
        public float aiFireCooldown = 1.5f;
        [Range(0.5f, 2f)] public float aiHealthMultiplier = 1f;
        [Range(0.5f, 3f)] public float rewardMultiplier = 1f;
    }
}
