using UnityEngine;
using RacingGame.Core.Ghost;

namespace RacingGame
{
    /// <summary>Samples the player's pose every N ticks from GO to finish.</summary>
    public sealed class GhostRecorder : MonoBehaviour
    {
        public int sampleStep = 5;
        public GhostRecord Record { get; private set; }
        public bool Recording { get; private set; }
        private Transform target;
        private CarController car;
        private int lastSampledTick = -1;

        public void Begin(RaceParticipant player, int trackHash, string name)
        {
            target = player.transform; car = player.Car;
            Record = new GhostRecord { TrackHash = trackHash, SampleStep = sampleStep, TickRate = 50, PlayerName = name ?? "" };
            Recording = true; lastSampledTick = -1;
        }

        public GhostRecord Stop() { Recording = false; return Record; }

        private void FixedUpdate()
        {
            if (!Recording || target == null) return;
            int tick = SimulationDriver.Instance.Tick;
            if (tick - lastSampledTick < sampleStep) return;
            lastSampledTick = tick;
            byte flags = 0;
            if (car != null && car.IsBoosting) flags |= 1;
            Record.Add(target.position, target.eulerAngles.y, car != null ? car.LastCommand.Steer : 0f, flags);
            if (Record.Samples.Count > 20000) Recording = false;   // ~33 min at 10 Hz: hard stop
        }
    }
}
