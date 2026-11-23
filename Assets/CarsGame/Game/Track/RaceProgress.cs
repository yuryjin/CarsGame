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
        public double LastCrossingTime { get; private set; }
        public int LastCrossingTick { get; private set; }
        public Vector3 LastCrossingPosition { get; private set; }
        private Vector3 previousWorld;
        public float TotalProgress => Counter != null ? Counter.TotalProgress(Distance) : 0f;
        public float SpeedAlongTrack { get; private set; }  // m/s, negative when driving backwards

        /// <summary>(lapNumber, lapTimeSeconds)</summary>
        public event Action<int, double> LapCompleted;

        private double[] gateTimes;
        private int[] gateLaps;
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
            previousWorld = transform.position;
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
            if (gateLaps == null || gateLaps.Length != Counter.GateCount)
            {
                gateTimes = new double[Counter.GateCount];
                gateLaps = new int[Counter.GateCount];
            }
            else Array.Clear(gateLaps, 0, gateLaps.Length);
            CurrentLapStart = RaceClock.Now;
            LastCrossingTime = 0d;
            previousWorld = transform.position;
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

            float previousDistance = Distance;
            Distance = Path.Project(transform.position, ref hint);
            int gate = Counter.NextGate;
            float delta = Counter.Step(Distance);
            if (gateLaps != null && gate != Counter.NextGate)
            {
                gateLaps[gate] = Counter.LapsCompleted + 1;
                gateTimes[gate] = RaceClock.Now;
            }
            SpeedAlongTrack = delta / SimulationDriver.FixedStep;

            if (Counter.LapJustCompleted)
            {
                Vector3 line = Path.Position(Path.startLineDistance), tangent = Path.Spline.Tangent(Path.startLineDistance);
                float before = Vector3.Dot(previousWorld - line, tangent);
                float after = Vector3.Dot(transform.position - line, tangent);
                float fraction = before <= 0f && after >= 0f && after > before
                    ? -before / (after - before)
                    : Mathf.Clamp01(Path.SignedDelta(previousDistance, Path.startLineDistance) / Mathf.Max(delta, .0001f));
                double end = Time.inFixedTimeStep && SimulationDriver.Current != null ? SimulationDriver.Current.StepEndClock : RaceClock.Now;
                LastCrossingTick = tick;
                LastCrossingPosition = Vector3.Lerp(previousWorld, transform.position, fraction);
                LastCrossingTime = Math.Max(CurrentLapStart, end - (1d - fraction) * SimulationDriver.FixedStep);
                CompleteLap(LastCrossingTime);
            }
            previousWorld = transform.position;

            if (SpeedAlongTrack < -2f) wrongWayTime += SimulationDriver.FixedStep; else wrongWayTime = 0f;
            WrongWay = wrongWayTime >= wrongWaySeconds;
        }

        /// <summary>Rival minus car time at the last common forward gate of the current lap.</summary>
        public bool TryGapTo(RaceProgress rival, out double seconds)
        {
            seconds = 0d;
            if (rival == null || gateLaps == null || rival.gateLaps == null || LapsCompleted != rival.LapsCompleted
                || Path != rival.Path || gateLaps.Length != rival.gateLaps.Length) return false;
            int lap = LapsCompleted + 1;
            for (int gate = gateLaps.Length - 1; gate >= 0; gate--)
            {
                if (gateLaps[gate] != lap || rival.gateLaps[gate] != lap) continue;
                seconds = rival.gateTimes[gate] - gateTimes[gate];
                return true;
            }
            return false;
        }

        private void CompleteLap(double now)
        {
            LastLapTime = now - CurrentLapStart;
            CurrentLapStart = now;
            if (LastLapTime < BestLapTime) BestLapTime = LastLapTime;
            LapCompleted?.Invoke(Counter.LapsCompleted, LastLapTime);
        }
    }
}
