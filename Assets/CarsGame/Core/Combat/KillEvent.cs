using System;

namespace RacingGame.Core.Combat
{
    /// <summary>
    /// Describes a vehicle elimination event with killer and victim car IDs and simulation expiration tick.
    /// </summary>
    public struct KillEvent
    {
        public int KillerId;
        public int VictimId;
        public int ExpireTick;

        public KillEvent(int killerId, int victimId, int expireTick = 0)
        {
            KillerId = killerId;
            VictimId = victimId;
            ExpireTick = expireTick;
        }
    }
}
