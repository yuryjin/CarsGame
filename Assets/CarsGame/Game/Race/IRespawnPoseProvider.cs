using UnityEngine;

namespace RacingGame
{
    /// <summary>Where a car should reappear after a reset or a wreck.</summary>
    public interface IRespawnPoseProvider
    {
        bool TryGetRespawnPose(out Vector3 position, out Quaternion rotation);
    }
}
