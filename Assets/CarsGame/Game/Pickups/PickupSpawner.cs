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
            var rng = RaceDirector.Instance.Rng;
            var kinds = new[] { PickupKind.Health, PickupKind.Ammo, PickupKind.Nitro, PickupKind.Item };
            float usable = path.Length - startExclusion;
            for (int i = 0; i < count; i++)
            {
                float d = path.Wrap(path.startLineDistance + startExclusion + usable * (i + 0.5f) / count + rng.Range(RngStreams.Stream.Spawn, -15f, 15f));
                float lane = rng.Range(RngStreams.Stream.Spawn, -maxLaneOffset, maxLaneOffset);
                path.Pose(d, lane, out var pos, out _);
                pickups.Add(Pickup.Create(kinds[i % kinds.Length], pos, transform));
            }
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
