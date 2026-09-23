using System;
using UnityEngine;
using RacingGame.Core.Track;

namespace RacingGame
{
    /// <summary>
    /// Tracks one car's position along the <see cref="TrackPath"/> and counts laps through the
    /// pure <see cref="LapCounter"/> (ordered forward gate crossings, teleports never credit).
    /// Lap times come from <see cref="RaceClock"/> (unscaled, pause-aware).
    /// </summary>
    public sealed class RaceProgress : MonoBehaviour
    {
        [Tooltip("Virtual gates per lap; gate 0 is the start/finish line.")]
        public int gateCount = 12;
        [Tooltip("Largest plausible movement along the track in one tick (m). Bigger = teleport.")]
        public float maxDeltaPerTick = 6f;
        [Tooltip("Seconds of driving against the track direction before WrongWay is raised.")]
        public float wrongWaySeconds = 1.5f;

        public TrackPath Path { get; private set; }
        public LapCounter Counter { get; private set; }
        public float Distance { get; private set; }
        public float DistanceFromStart => Counter != null ? Counter.FromStart(Distance) : 0f;
        public int LapsCompleted => Counter != null ? Counter.LapsCompleted : 0;
        public int GatesPassedThisLap => Counter != null ? Counter.NextGate : 0;
        public bool WrongWay { get; private set; }
        public double CurrentLapStart { get; private set; }
        public double BestLapTime { get; private set; } = double.PositiveInfinity;
        public double LastLapTime { get; private set; }
        public float TotalProgress => Counter != null ? Counter.TotalProgress(Distance) : 0f;
        public float SpeedAlongTrack { get; private set; }  // m/s, negative when driving backwards

        /// <summary>(lapNumber, lapTimeSeconds)</summary>
        public event Action<int, double> LapCompleted;

        private int hint = -1;
        private float wrongWayTime;

        private void Awake()
        {
            Path = TrackPath.Instance;
            if (Path == null) { Debug.LogWarning($"{name}: no TrackPath in scene; RaceProgress disabled.", this); enabled = false; return; }
            Counter = new LapCounter(Path.Length, Path.startLineDistance, gateCount, maxDeltaPerTick);
        }

        private void OnEnable() { if (Counter != null) Resync(); }

        /// <summary>Re-project without crediting anything (after teleport, respawn).</summary>
        public void Resync()
        {
            if (Path == null) Path = TrackPath.Instance;
            if (Path == null) return;
            if (Counter == null) Counter = new LapCounter(Path.Length, Path.startLineDistance, gateCount, maxDeltaPerTick);
            hint = -1;
            Distance = Path.Project(transform.position, ref hint);
            Counter.Resync(Distance);
            wrongWayTime = 0f;
            WrongWay = false;
        }

        /// <summary>Start counting laps from the current pose (called at GO).</summary>
        public void BeginRace()
        {
            if (Path == null) return;
            hint = -1;
            Distance = Path.Project(transform.position, ref hint);
            Counter.Begin(Distance);
            CurrentLapStart = RaceClock.Now;
            BestLapTime = double.PositiveInfinity;
            LastLapTime = 0d;
            wrongWayTime = 0f;
            WrongWay = false;
        }

        /// <summary>Tell progress that the car was moved by code, so the jump is not scored.</summary>
        public void NotifyTeleport() { Resync(); }

        /// <summary>Called from the car's simulation step.</summary>
        public void SimulationStep(int tick)
        {
            if (!enabled || Path == null || Counter == null) return;

            Distance = Path.Project(transform.position, ref hint);
            float delta = Counter.Step(Distance);
            SpeedAlongTrack = delta / SimulationDriver.FixedStep;

            if (Counter.LapJustCompleted) CompleteLap();

            if (SpeedAlongTrack < -2f) wrongWayTime += SimulationDriver.FixedStep; else wrongWayTime = 0f;
            WrongWay = wrongWayTime >= wrongWaySeconds;
        }

        private void CompleteLap()
        {
            double now = RaceClock.Now;
            LastLapTime = now - CurrentLapStart;
            CurrentLapStart = now;
            if (LastLapTime < BestLapTime) BestLapTime = LastLapTime;
            LapCompleted?.Invoke(Counter.LapsCompleted, LastLapTime);
        }
    }
}
