using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class RaceClockHitchPlayTests
    {
        [UnityTest, Timeout(30000)]
        public IEnumerator LongFramesCannotCountLessRaceTimeThanThePhysicsTheyAdvance()
        {
            float capture = Time.captureDeltaTime, maximum = Time.maximumDeltaTime, fixedStep = Time.fixedDeltaTime;
            GameObject created = null;
            try
            {
                PauseController.ResetAll();
                Time.captureDeltaTime = 0f;
                Time.maximumDeltaTime = 1f / 3f;
                var driver = SimulationDriver.Current;
                if (driver == null)
                {
                    created = new GameObject("ClockHitchDriver");
                    driver = created.AddComponent<SimulationDriver>();
                }
                RaceClock.Reset();
                yield return null; yield return null;
                int startTick = driver.Tick;
                double startTime = RaceClock.Now;
                // Real engine frames: captureDeltaTime does not model wall-clock stalls.
                // Six bounded sleeps use no worker cores and reproduce a 3 Hz display.
                for (int frame = 0; frame < 6; frame++)
                {
                    System.Threading.Thread.Sleep(300);
                    yield return null;
                }
                double simulated = (driver.Tick - startTick) * (double)SimulationDriver.FixedStep;
                double clock = RaceClock.Now - startTime;
                Debug.Log($"[ClockHitch] physics={simulated:F4}s clock={clock:F4}s cap={RaceClock.MaxFrameStep:F4}s");
                Assert.Greater(simulated, 1.5d, "Control: the engine actually ran multiple catch-up physics steps");
                Assert.GreaterOrEqual(clock + 2 * SimulationDriver.FixedStep, simulated,
                    $"A stalled frame must not produce a faster timed run: physics={simulated:F3}s, clock={clock:F3}s");
            }
            finally
            {
                if (created != null) Object.DestroyImmediate(created);
                Time.captureDeltaTime = capture;
                Time.maximumDeltaTime = maximum;
                Time.fixedDeltaTime = fixedStep;
                RaceClock.Reset();
                PauseController.ResetAll();
            }
        }
    }
}
