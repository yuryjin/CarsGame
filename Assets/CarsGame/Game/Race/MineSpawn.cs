using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    /// <summary>Contact mines go off when a rival drives close; charges wait for their owner (or the fuse).</summary>
    public enum MineSkin { Standard, Banana, BearTrap, Glue, Ice, Emp, Spring, Cactus }

    public enum MineKind { Contact, Charge }

    /// <summary>
    /// A request to drop a contact mine into the world.
    /// </summary>
    public struct MineSpawn
    {
        public GameObject Owner;
        public int OwnerId;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Damage;
        public int Tick;
        public Material Material;
        public MineKind Kind;
        public MineSkin Skin;
        public bool OverrideStatus;
        public StatusKind Status;
        public float StatusDuration;
        public float BlastRadius;
        public float Impulse;
        /// <summary>Laid by the map (<see cref="MineField"/>), not a car: no owner, never expires, not counted in the dropped-mine cap.</summary>
        public bool Field;
    }
}
