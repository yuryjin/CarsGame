using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// Registry and factory for the simulation: assigns CarIds, owns the seeded random streams,
    /// spawns and pools projectiles. Lives on the same GameObject as <see cref="SimulationDriver"/>.
    /// This is the seam a network layer will replace (ids, spawning, tick) — gameplay code only
    /// talks to it, never to Instantiate/Destroy for simulated objects.
    /// </summary>
    public sealed class RaceDirector : MonoBehaviour, ISimulated
    {
        [Tooltip("0 = pick a seed at start. Set for reproducible AI behaviour.")]
        public int raceSeed;
        [Tooltip("Shells created up front (prewarmed before the race starts).")]
        public int projectilePoolSize = 24;
        [Tooltip("Hard cap on shells in flight. When it is reached the oldest shell is recycled, so the pool never grows past this.")]
        public int maxLiveProjectiles = 48;

        public static RaceDirector Instance
        {
            get
            {
                var driver = SimulationDriver.Instance;
                var d = driver.GetComponent<RaceDirector>();
                return d != null ? d : driver.gameObject.AddComponent<RaceDirector>();
            }
        }

        /// <summary>The live director or null — never creates one.</summary>
        public static RaceDirector Current => SimulationDriver.Current != null ? SimulationDriver.Current.GetComponent<RaceDirector>() : null;

        public RngStreams Rng { get; private set; }
        public int Tick => SimulationDriver.Instance.Tick;
        public IReadOnlyList<CarController> Cars => cars;
        public int SimulationOrder => int.MaxValue; // after every car
        public int ShotsFired { get; private set; }
        public int ShellHits { get; private set; }
        /// <summary>(shooter, victim) — fired when a shell damages a car.</summary>
        public event System.Action<GameObject, GameObject> ShellHit;
        internal void CountHit(GameObject shooter, GameObject victim) { ShellHits++; ShellHit?.Invoke(shooter, victim); }

        private readonly List<CarController> cars = new List<CarController>();
        private readonly List<Projectile> pooledShells = new List<Projectile>();
        private readonly List<Projectile> liveShells = new List<Projectile>();
        private Transform shellRoot;
        private int nextCarId = 1;

        private void Awake()
        {
            if (raceSeed == 0) raceSeed = System.Environment.TickCount ^ 0x5bd1e995;
            Rng = new RngStreams(raceSeed);
        }

        private void OnEnable() { SimulationDriver.Instance.Register(this); }
        private void OnDisable() { var d = SimulationDriver.Current; if (d != null) d.Unregister(this); }

        public int RegisterCar(CarController car)
        {
            if (!cars.Contains(car)) cars.Add(car);
            return nextCarId++;
        }

        public void UnregisterCar(CarController car) { cars.Remove(car); }

        public int LiveProjectiles => liveShells.Count;
        private int MaxLive => Mathf.Max(1, maxLiveProjectiles);
        public int PooledProjectiles => pooledShells.Count;

        public Projectile RequestProjectile(in ProjectileSpawn spawn)
        {
            var root = ShellRoot;   // prewarms the pool on first use
            // Hard cap on shells in flight, checked before any pooled shell is used (the prewarm or a
            // lowered cap must not bypass it): the oldest shells are recycled first.
            int cap = MaxLive;
            while (liveShells.Count >= cap) { int before = liveShells.Count; liveShells[0].Deactivate(); if (liveShells.Count == before) liveShells.RemoveAt(0); }
            if (pooledShells.Count == 0) pooledShells.Add(Projectile.CreatePooled(root));
            var shell = pooledShells[pooledShells.Count - 1];
            pooledShells.RemoveAt(pooledShells.Count - 1);
            liveShells.Add(shell);
            shell.Activate(this, spawn);
            ShotsFired++;
            return shell;
        }

        // Projectile.Deactivate guards on its live flag, so a shell comes back at most once.
        internal void ReturnToPool(Projectile shell)
        {
            liveShells.Remove(shell);
            pooledShells.Add(shell);
        }

        /// <summary>Between races: recall every shell in flight, zero the counters and make sure the pool is warm.</summary>
        public void ResetRace()
        {
            for (int i = liveShells.Count - 1; i >= 0; i--) liveShells[i].Deactivate();
            ShotsFired = 0;
            ShellHits = 0;
            _ = ShellRoot;
        }

        public void SimulationStep(int tick)
        {
            // Iterate backwards: a shell that expires removes itself from liveShells.
            for (int i = liveShells.Count - 1; i >= 0; i--) liveShells[i].SimulationStep(tick);
        }

        private Transform ShellRoot
        {
            get
            {
                if (shellRoot == null)
                {
                    shellRoot = new GameObject("Shells").transform;
                    shellRoot.SetParent(transform, false);
                    int prewarm = Mathf.Min(projectilePoolSize, MaxLive);
                    for (int i = 0; i < prewarm; i++) pooledShells.Add(Projectile.CreatePooled(shellRoot));
                }
                return shellRoot;
            }
        }
    }
}
