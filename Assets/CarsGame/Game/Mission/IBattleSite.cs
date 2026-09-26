using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// A map that is not the circuit (the mission desert): where the field starts, where a wrecked or lost car
    /// comes back, and which driver its bots use. A participant with a site never looks at the TrackPath.
    /// </summary>
    public interface IBattleSite
    {
        void GetSpawnPose(int slot, int total, out Vector3 position, out Quaternion rotation);
        bool TryGetRecoveryPose(CarController car, out Vector3 position, out Quaternion rotation);
        ICarInputSource CreateBotDriver(GameObject car, AiDriverProfile profile);
    }
}
