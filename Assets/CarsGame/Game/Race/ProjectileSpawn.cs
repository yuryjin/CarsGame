using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    /// <summary>A request to put a shell into the world. Spawning is an event so that a server can own it later.</summary>
    public struct ProjectileSpawn
    {
        public GameObject Owner;
        public int OwnerId;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Damage;
        public int Tick;
        public Material Material;
        public ProjectileKind Kind;
        public StatusKind Status;
        public float StatusDuration;
        public Transform HomingTarget;
    }
}
