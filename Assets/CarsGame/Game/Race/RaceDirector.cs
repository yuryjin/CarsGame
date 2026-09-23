using System;
using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Combat;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// Registry and factory for the simulation: assigns CarIds, owns the seeded random streams,
    /// spawns and pools projectiles and mines. Lives on the same GameObject as <see cref="SimulationDriver"/>.
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

        [Tooltip("Mines created up front (prewarmed before the race starts).")]
        public int minePoolSize = 12;
        [Tooltip("Hard cap on active mines on track. When reached the oldest mine is recycled.")]
        public int maxLiveMines = 24;

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
        public static RaceDirector Current => SimulationDriver.Current != null
            ? SimulationDriver.Current.GetComponent<RaceDirector>()
            : FindAnyObjectByType<RaceDirector>();

        public RngStreams Rng
        {
            get
            {
                if (rng == null)
                {
                    if (raceSeed == 0) raceSeed = Environment.TickCount ^ 0x5bd1e995;
                    rng = new RngStreams(raceSeed);
                }
                return rng;
            }
            private set => rng = value;
        }
        private RngStreams rng;
        public int Tick => SimulationDriver.Instance.Tick;
        public IReadOnlyList<CarController> Cars => cars;
        public int SimulationOrder => int.MaxValue; // after every car

        public int ShotsFired { get; private set; }
        public int ShellHits { get; private set; }
        public int MinesDropped { get; private set; }
        public int MineHits { get; private set; }

        /// <summary>(shooter, victim) — fired when a shell damages a car.</summary>
        public event Action<GameObject, GameObject> ShellHit;
        /// <summary>(owner, victim) — fired when a mine damages a car.</summary>
        public event Action<GameObject, GameObject> MineHit;
        /// <summary>Fired when a car is eliminated with killer and victim attribution.</summary>
        public event Action<KillEvent> OnKillRegistered;

        public void RegisterKill(int killerId, int victimId)
        {
            int expireTick = SimulationDriver.Instance != null
                ? SimulationDriver.Instance.Tick + SimulationDriver.SecondsToTicks(4f)
                : 0;
            var killEvent = new KillEvent(killerId, victimId, expireTick);
            OnKillRegistered?.Invoke(killEvent);
        }

        public void RegisterKill(KillEvent killEvent)
        {
            OnKillRegistered?.Invoke(killEvent);
        }

        internal void CountHit(GameObject shooter, GameObject victim)
        {
            ShellHits++;
            ShellHit?.Invoke(shooter, victim);
        }

        internal void CountMineHit(GameObject owner, GameObject victim)
        {
            MineHits++;
            MineHit?.Invoke(owner, victim);
        }

        private readonly List<CarController> cars = new List<CarController>();
        private readonly List<Projectile> pooledShells = new List<Projectile>();
        private readonly List<Projectile> liveShells = new List<Projectile>();
        private readonly List<MineHazard> pooledMines = new List<MineHazard>();
        private readonly List<MineHazard> liveMines = new List<MineHazard>();
        private Transform shellRoot;
        private Transform mineRoot;
        private int nextCarId = 1;

        private void Awake()
        {
            if (raceSeed == 0) raceSeed = Environment.TickCount ^ 0x5bd1e995;
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
        private int MaxLiveProjectilesCap => Mathf.Max(1, maxLiveProjectiles);
        public int PooledProjectiles => pooledShells.Count;

        public int LiveMines => liveMines.Count;
        public IReadOnlyList<MineHazard> LiveMineHazards => liveMines;
        private int MaxLiveMinesCap => Mathf.Max(1, maxLiveMines);
        public int PooledMines => pooledMines.Count;

        public OilHazardPool OilSlicks
        {
            get
            {
                if (oilSlicks == null)
                {
                    oilSlicks = GetComponent<OilHazardPool>();
                    if (oilSlicks == null) oilSlicks = gameObject.AddComponent<OilHazardPool>();
                }
                return oilSlicks;
            }
        }
        private OilHazardPool oilSlicks;

        public void SpawnOilSlick(Vector3 position, float lifetimeSeconds = OilHazardPool.DefaultLifetimeSeconds)
        {
            OilSlicks.SpawnSlick(position, Tick, lifetimeSeconds);
        }

        public Projectile RequestProjectile(in ProjectileSpawn spawn)
        {
            var root = ShellRoot;   // prewarms the pool on first use
            int cap = MaxLiveProjectilesCap;
            while (liveShells.Count >= cap)
            {
                int before = liveShells.Count;
                liveShells[0].Deactivate();
                if (liveShells.Count == before) liveShells.RemoveAt(0);
            }
            if (pooledShells.Count == 0) pooledShells.Add(Projectile.CreatePooled(root));
            var shell = pooledShells[pooledShells.Count - 1];
            pooledShells.RemoveAt(pooledShells.Count - 1);
            liveShells.Add(shell);
            shell.Activate(this, spawn);
            ShotsFired++;
            return shell;
        }

        internal void ReturnToPool(Projectile shell)
        {
            liveShells.Remove(shell);
            pooledShells.Add(shell);
        }

        public MineHazard RequestMine(in MineSpawn spawn)
        {
            var root = MineRoot;   // prewarms the pool on first use
            int cap = MaxLiveMinesCap;
            while (liveMines.Count >= cap)
            {
                int before = liveMines.Count;
                liveMines[0].Deactivate();
                if (liveMines.Count == before) liveMines.RemoveAt(0);
            }
            if (pooledMines.Count == 0) pooledMines.Add(MineHazard.CreatePooled(root));
            var mine = pooledMines[pooledMines.Count - 1];
            pooledMines.RemoveAt(pooledMines.Count - 1);
            liveMines.Add(mine);
            mine.Activate(this, spawn);
            MinesDropped++;
            return mine;
        }

        internal void ReturnToPool(MineHazard mine)
        {
            liveMines.Remove(mine);
            pooledMines.Add(mine);
        }

        /// <summary>Between races: recall every shell and mine in flight, zero the counters and make sure pools are warm.</summary>
        public void ResetRace()
        {
            for (int i = liveShells.Count - 1; i >= 0; i--) liveShells[i].Deactivate();
            for (int i = liveMines.Count - 1; i >= 0; i--) liveMines[i].Deactivate();
            if (oilSlicks != null) oilSlicks.ResetSlicks();
            ShotsFired = 0;
            ShellHits = 0;
            MinesDropped = 0;
            MineHits = 0;
            _ = ShellRoot;
            _ = MineRoot;
        }

        public void SimulationStep(int tick)
        {
            // Iterate backwards: objects that expire remove themselves from live lists.
            for (int i = liveShells.Count - 1; i >= 0; i--) liveShells[i].SimulationStep(tick);
            for (int i = liveMines.Count - 1; i >= 0; i--) liveMines[i].SimulationStep(tick);
            if (oilSlicks != null) oilSlicks.SimulationStep(tick, cars);
        }

        private Transform ShellRoot
        {
            get
            {
                if (shellRoot == null)
                {
                    shellRoot = new GameObject("Shells").transform;
                    shellRoot.SetParent(transform, false);
                    int prewarm = Mathf.Min(projectilePoolSize, MaxLiveProjectilesCap);
                    for (int i = 0; i < prewarm; i++) pooledShells.Add(Projectile.CreatePooled(shellRoot));
                }
                return shellRoot;
            }
        }

        private Transform MineRoot
        {
            get
            {
                if (mineRoot == null)
                {
                    mineRoot = new GameObject("Mines").transform;
                    mineRoot.SetParent(transform, false);
                    int prewarm = Mathf.Min(minePoolSize, MaxLiveMinesCap);
                    for (int i = 0; i < prewarm; i++) pooledMines.Add(MineHazard.CreatePooled(mineRoot));
                }
                return mineRoot;
            }
        }
    }
}
