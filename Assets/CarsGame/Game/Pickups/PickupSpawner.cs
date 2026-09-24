using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// Places pickups along the track spline (seeded, so a race layout is reproducible) and
    /// respawns taken ones after a delay.
    /// </summary>
    public sealed class PickupSpawner : MonoBehaviour
    {
        public int count = 9;
        public float respawnSeconds = 20f;
        [Tooltip("Keep pickups off the first metres after the start line.")]
        public float startExclusion = 60f;
        public float maxLaneOffset = 2.6f;

        private readonly List<Pickup> pickups = new List<Pickup>();
        private TrackPath path;

        public bool spawnOnStart = false;

        private void Start() { if (spawnOnStart) Spawn(count); }

        public void Clear()
        {
            foreach (var p in pickups) if (p != null) Destroy(p.gameObject);
            pickups.Clear();
        }

        public void Spawn(int amount)
        {
            Clear();
            count = amount;
            path = TrackPath.Instance;
            if (path == null || count <= 0) return;
            var kinds = new[] { PickupKind.Health, PickupKind.Ammo, PickupKind.Nitro, PickupKind.Item };
            for (int i = 0; i < count; i++)
            {
                var kind = kinds[i % kinds.Length];
                Placement(path, kind, i, count, startExclusion, maxLaneOffset, out float d, out float lane);
                path.Pose(d, lane, out var pos, out var rot);
                var pickup = Pickup.Create(kind, pos, transform);
                pickup.transform.rotation = rot;
                pickups.Add(pickup);
            }
            if (path.Fork != null)
            {
                path.Pose(path.Fork.RepairDistance, -1.8f, out var pos, out var rot);
                var repair = Pickup.Create(PickupKind.Health, pos, transform);
                repair.transform.rotation = rot;
                pickups.Add(repair);
            }
        }

        /// <summary>One pickup per sector: repair outside bends, ammo away from the fast line,
        /// nitro on the approach. Uses the current spline direction, including reverse events.</summary>
        public static void Placement(TrackPath track, PickupKind kind, int index, int amount,
            float exclusion, float laneLimit, out float distance, out float lane)
        {
            float excluded = Mathf.Clamp(exclusion, 0f, track.Length * 0.25f);
            float sector = (track.Length - excluded) / Mathf.Max(1, amount);
            float start = track.startLineDistance + excluded + sector * index;
            float margin = Mathf.Min(12f, sector * 0.2f);
            float approach = kind == PickupKind.Nitro ? Mathf.Min(24f, sector * 0.3f) : 0f;
            float best = start + sector * 0.5f, bend = 0f;
            for (float d = start + margin + approach; d <= start + sector - margin; d += 3f)
            {
                float turn = Vector3.SignedAngle(track.Tangent(d - 6f), track.Tangent(d + 6f), Vector3.up);
                if (Mathf.Abs(turn) > Mathf.Abs(bend)) { bend = turn; best = d; }
            }
            distance = track.Wrap(Mathf.Clamp(best - approach, start + margin, start + sector - margin));
            float limit = Mathf.Min(Mathf.Abs(laneLimit), Mathf.Max(0f, track.halfWidth - 1.2f));
            float outside = Mathf.Abs(bend) > 0.1f ? -Mathf.Sign(bend) : (index % 2 == 0 ? -1f : 1f);
            lane = outside * limit;
            if (kind == PickupKind.Item) lane = -lane;
        }

        private void Update()
        {
            if (pickups.Count == 0) return;
            int tick = SimulationDriver.Instance.Tick;
            int respawnTicks = SimulationDriver.SecondsToTicks(respawnSeconds);
            foreach (var p in pickups) if (p.Taken && tick - p.TakenTick >= respawnTicks) p.TryRespawn();
        }
    }
}
