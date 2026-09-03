using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// Test double: drive a car from code (Editor eval scripts, play-mode tests, replays).
    /// Set the public fields and the car follows them on the next tick. Also the shape a
    /// network input source will take.
    /// </summary>
    public sealed class ScriptedDriverInput : MonoBehaviour, ICarInputSource
    {
        [Range(-1f, 1f)] public float throttle;
        [Range(-1f, 1f)] public float steer;
        public bool brake;
        public bool fire;

        private readonly EdgeLatch resetLatch = new EdgeLatch();
        public int SamplesTaken { get; private set; }
        public DriveCommand LastCommand { get; private set; }

        public void PressReset() => resetLatch.Press();

        public void Set(float throttleValue, float steerValue, bool brakeValue = false, bool fireValue = false)
        {
            throttle = throttleValue; steer = steerValue; brake = brakeValue; fire = fireValue;
        }

        public DriveCommand Sample(int tick)
        {
            SamplesTaken++;
            LastCommand = new DriveCommand
            {
                Tick = tick, Throttle = throttle, Steer = steer, Brake = brake, Fire = fire,
                ResetEdge = resetLatch.ConsumeAll()
            }.Clamped();
            return LastCommand;
        }
    }
}
