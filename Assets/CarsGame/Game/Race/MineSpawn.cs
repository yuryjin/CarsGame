using UnityEngine;

namespace RacingGame
{
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
    }
}
