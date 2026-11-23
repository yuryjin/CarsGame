using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RacingGame.Tests
{
    // SimulationDriver (-100) has advanced RaceClock, and PerfectStart (0) has yet to poll the throttle.
    [DefaultExecutionOrder(-50)]
    public sealed class LaunchTrainingInputProbe : MonoBehaviour
    {
        private RaceManager race;
        private Keyboard keyboard;
        private float offset;
        public double? PressedAt { get; private set; }

        public void Schedule(RaceManager manager, Keyboard device, float seconds)
        {
            race = manager; keyboard = device; offset = seconds; PressedAt = null;
        }

        private void Update()
        {
            if (race == null || PressedAt.HasValue) return;
            if (offset > 0f && race.State != RaceState.Racing) return;
            double threshold = offset > 0f ? race.RaceStartTime + offset : race.countdownSeconds + offset;
            if (RaceClock.Now < threshold) return;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            InputSystem.Update();
            PressedAt = RaceClock.Now;
        }
    }
}
