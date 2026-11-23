using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// The single fixed-rate loop of the game. Owns the tick counter and calls every registered
    /// <see cref="ISimulated"/> in a deterministic order (cars by CarId, then the director), so
    /// no gameplay code depends on Unity's undefined MonoBehaviour FixedUpdate ordering.
    /// Created on demand; one per scene.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SimulationDriver : MonoBehaviour
    {
        public const float FixedStep = 0.02f;

        private static SimulationDriver instance;
        public static SimulationDriver Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<SimulationDriver>();
                    if (instance == null)
                    {
                        var go = new GameObject("SimulationDriver");
                        instance = go.AddComponent<SimulationDriver>();
                        go.AddComponent<RaceDirector>();
                    }
                }
                return instance;
            }
        }

        /// <summary>The live driver or null — never creates one (safe from OnDisable/OnDestroy during scene unload).</summary>
        public static SimulationDriver Current => instance;

        public int Tick { get; private set; }
        public double StepEndClock { get; private set; }
        private int physicsFrame = -1;
        private int stepsThisFrame;

        private readonly List<ISimulated> simulated = new List<ISimulated>();
        private bool orderDirty;
        private bool stepping;
        private readonly List<ISimulated> pendingAdd = new List<ISimulated>();
        private readonly List<ISimulated> pendingRemove = new List<ISimulated>();

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            if (Mathf.Abs(Time.fixedDeltaTime - FixedStep) > 1e-4f)
            {
                Debug.LogWarning($"[SimulationDriver] fixedDeltaTime is {Time.fixedDeltaTime}, expected {FixedStep}. Forcing it: tick-based timers assume 50 Hz.");
                Time.fixedDeltaTime = FixedStep;
            }
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        public void Register(ISimulated s)
        {
            if (stepping) { pendingAdd.Add(s); return; }
            if (!simulated.Contains(s)) { simulated.Add(s); orderDirty = true; }
        }

        public void Unregister(ISimulated s)
        {
            if (stepping) { pendingRemove.Add(s); return; }
            simulated.Remove(s);
        }

        private void Update()
        {
            RaceClock.Advance(Time.unscaledDeltaTime);
        }

        private void FixedUpdate()
        {
            Tick++;
            if (physicsFrame != Time.frameCount) { physicsFrame = Time.frameCount; stepsThisFrame = 0; }
            StepEndClock = RaceClock.Now + stepsThisFrame++ * (double)FixedStep;
            if (orderDirty)
            {
                simulated.Sort((a, b) => a.SimulationOrder.CompareTo(b.SimulationOrder));
                orderDirty = false;
            }

            stepping = true;
            for (int i = 0; i < simulated.Count; i++)
            {
                var s = simulated[i];
                if (s is Behaviour b && (b == null || !b.isActiveAndEnabled)) continue;
                s.SimulationStep(Tick);
            }
            stepping = false;

            if (pendingRemove.Count > 0) { foreach (var s in pendingRemove) simulated.Remove(s); pendingRemove.Clear(); }
            if (pendingAdd.Count > 0) { foreach (var s in pendingAdd) if (!simulated.Contains(s)) simulated.Add(s); pendingAdd.Clear(); orderDirty = true; }
        }

        public static int SecondsToTicks(float seconds) => Mathf.Max(1, Mathf.CeilToInt(seconds / FixedStep));
        public static float TicksToSeconds(int ticks) => ticks * FixedStep;
    }
}
