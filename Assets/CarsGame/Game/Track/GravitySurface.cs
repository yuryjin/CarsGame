using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Marks a collider a fast car sticks to: while <see cref="CarController"/> is on it above its attach speed,
    /// gravity pulls into the surface instead of down, so walls and a tunnel ceiling can be driven.
    /// </summary>
    public sealed class GravitySurface : MonoBehaviour
    {
        /// <summary>Enabled surfaces in the scene; cars skip the extra ground cast while it is zero.</summary>
        public static int ActiveCount { get; private set; }

        private void OnEnable() => ActiveCount++;
        private void OnDisable() => ActiveCount--;
    }
}
