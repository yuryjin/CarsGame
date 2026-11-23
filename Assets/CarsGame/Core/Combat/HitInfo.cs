using UnityEngine;

namespace RacingGame.Core.Combat
{
    /// <summary>
    /// Describes an incoming hit (damage, impulse, attacker, status effect payload, hit type).
    /// Passed to CarHealth.ApplyHit for deterministic damage and status resolution.
    /// </summary>
    public struct HitInfo
    {
        public GameObject Attacker;
        public float Damage;
        public Vector3 Impulse;
        public Vector3 Point;
        public Vector3 Normal;
        public StatusKind Status;
        public float StatusDuration;
        public int StatusDurationTicks;
        public HitType HitType;
        public bool FromMine;

        public HitInfo(GameObject attacker, float damage, HitType hitType = HitType.Direct)
        {
            Attacker = attacker;
            Damage = damage;
            Impulse = Vector3.zero;
            Point = Vector3.zero;
            Normal = Vector3.zero;
            Status = StatusKind.None;
            StatusDuration = 0f;
            StatusDurationTicks = 0;
            HitType = hitType;
            FromMine = false;
        }
    }
}
