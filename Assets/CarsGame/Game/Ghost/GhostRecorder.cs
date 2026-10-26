using UnityEngine;
using RacingGame.Core.Ghost;

namespace RacingGame
{
    /// <summary>Samples complete poses against RaceClock from GO to finish, including recovery cuts.</summary>
    public sealed class GhostRecorder : MonoBehaviour
    {
        public int sampleStep = 5;
        public GhostRecord Record { get; private set; }
        public bool Recording { get; private set; }
        private Transform target;
        private CarController car;
        private int lastSampledTick;
        private double startClock;

        public void Begin(RaceParticipant player, int trackHash, string name)
        {
            Stop();
            target = player.transform; car = player.Car;
            int step = Mathf.Clamp(sampleStep, 1, 50);
            Record = new GhostRecord { TrackHash = trackHash, SampleStep = step, TickRate = 50, PlayerName = name ?? "",
                BodyId = player.GetComponent<CarBody>()?.BodyId ?? Core.Economy.CarBodies.DefaultId };
            Recording = true;
            startClock = RaceClock.Now;
            lastSampledTick = SimulationDriver.Instance.Tick;
            if (car != null) car.Teleported += OnTeleported;
            Capture(0);
        }

        public GhostRecord Stop()
        {
            if (Recording) Capture(0);
            Recording = false;
            Detach();
            return Record;
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Detach();
        private void Detach()
        {
            if (car != null) car.Teleported -= OnTeleported;
            car = null; target = null;
        }

        private void OnTeleported()
        {
            if (!Recording) return;
            Capture(GhostRecord.TeleportFlag);
            lastSampledTick = SimulationDriver.Instance.Tick;
        }

        private void Capture(byte extraFlags)
        {
            if (Record == null || target == null) return;
            double elapsed = System.Math.Max(0d, RaceClock.Now - startClock);
            if (Record.Samples.Count >= GhostRecord.MaxSamples || elapsed * 1000d > GhostRecord.MaxDurationMilliseconds)
            {
                // A truncated run must never replace a complete best with an artificially short duration.
                Recording = false; Record = null; Detach(); return;
            }
            byte flags = extraFlags;
            if (car != null && car.IsBoosting) flags |= GhostRecord.BoostFlag;
            Record.AddPose(target.position, target.rotation, elapsed, car != null ? car.LastCommand.Steer : 0f, flags);
        }

        private void FixedUpdate()
        {
            if (!Recording || target == null) return;
            int tick = SimulationDriver.Instance.Tick;
            if (tick - lastSampledTick < Record.SampleStep) return;
            lastSampledTick = tick;
            Capture(0);
        }
    }
}
