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
        [Tooltip("Shells kept ready in the pool. Grows on demand.")]
        public int projectilePoolSize = 24;

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

        public Projectile RequestProjectile(in ProjectileSpawn spawn)
        {
            Projectile shell;
            if (pooledShells.Count > 0)
            {
                shell = pooledShells[pooledShells.Count - 1];
                pooledShells.RemoveAt(pooledShells.Count - 1);
            }
            else
            {
                shell = Projectile.CreatePooled(ShellRoot);
            }
            liveShells.Add(shell);
            shell.Activate(this, spawn);
            ShotsFired++;
            return shell;
        }

        internal void ReturnToPool(Projectile shell)
        {
            liveShells.Remove(shell);
            if (!pooledShells.Contains(shell)) pooledShells.Add(shell);
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
                    for (int i = 0; i < projectilePoolSize; i++) pooledShells.Add(Projectile.CreatePooled(shellRoot));
                }
                return shellRoot;
            }
        }
    }
}
