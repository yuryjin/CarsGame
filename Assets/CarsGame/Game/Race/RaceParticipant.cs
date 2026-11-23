using UnityEngine;
using RacingGame.Core.Sim;
using RacingGame.Core.Combat;

namespace RacingGame
{
    /// <summary>
    /// One car in a race: identity for results, respawn pose on the track, and the glue between
    /// <see cref="CarController"/>, <see cref="CarHealth"/> and <see cref="RaceProgress"/>.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public sealed class RaceParticipant : MonoBehaviour, IRespawnPoseProvider
    {
        public string displayName = "Car";
        public string rivalId;
        public bool isPlayer;
        [Tooltip("Lateral offset used for respawn so cars do not reappear on the centre line every time.")]
        public float laneOffset;
        [Tooltip("Metres behind the current position to respawn (looking forward again).")]
        public float respawnSetback = 6f;

        private CarController car;
        private CarHealth health;
        private CarStatus status;
        private CarWeapons weapons;
        private RaceProgress progress;

        public CarController Car => car != null ? car : (car = GetComponent<CarController>());
        public CarHealth Health => health != null ? health : (health = GetComponent<CarHealth>());
        public CarStatus Status => status != null ? status : (status = GetComponent<CarStatus>() ?? gameObject.AddComponent<CarStatus>());
        public CarWeapons Weapons => weapons != null ? weapons : (weapons = GetComponent<CarWeapons>() ?? gameObject.AddComponent<CarWeapons>());
        public RaceProgress Progress => progress != null ? progress : (progress = GetComponent<RaceProgress>());
        /// <summary>Kills this race, per victim (the payout shrinks for repeat kills of the same rival).</summary>
        public KillTally KillTally { get; } = new KillTally();
        public int Kills => KillTally.Total;
        public int TurretPoints { get; internal set; }
        public int Deaths { get; set; }
        public int EliminatedAtTick { get; private set; } = int.MaxValue;
        public ArenaLayout Arena { get; private set; }
        public ArenaScore Score { get; private set; }

        internal void JoinArena(ArenaLayout arena)
        {
            Arena = arena;
            Score = new ArenaScore(Mathf.RoundToInt(1f / SimulationDriver.FixedStep), Mathf.RoundToInt(arena.Duration / SimulationDriver.FixedStep));
            if (Health != null && isActiveAndEnabled) Health.HitResolved += ArenaHit;
        }

        public int RetiredOnStage { get; private set; }

        internal void RetireOnStage(int stage)
        {
            RetiredOnStage = stage;
            EliminatedAtTick = SimulationDriver.Instance.Tick;
            Car.inputEnabled = false;
            gameObject.SetActive(false);
        }
        /// <summary>A map other than the circuit (the boss mission): it owns respawn poses; no track auto-reset.</summary>
        public IBattleSite Site { get; set; }
        public HotBombRace HotBomb { get; internal set; }
        [Tooltip("Seconds off the asphalt before the car is put back on the track automatically.")]
        public float offTrackResetSeconds = 5f;
        [Tooltip("Lateral distance (m) from the centre line that counts as lost.")]
        public float lostLateralMetres = 20f;
        public int AutoResets { get; private set; }
        public int DriftBoosts { get; private set; }
        public int Shortcuts { get; private set; }
        public int CeilingBoosts { get; internal set; }
        public int BombPasses { get; internal set; }
        public int Resets { get; private set; }
        public int Rams { get; private set; }
        public bool CleanFinalLap => finalLapStarted && !finalLapMistake;
        private bool finalLapStarted, finalLapMistake, wasOnFork;
        private float forkTravel;
        private int dirtCheckpoint;

        private int carMask;
        private float offTrackTime;

        private void Awake()
        {
            car = GetComponent<CarController>();
            health = GetComponent<CarHealth>();
            status = GetComponent<CarStatus>();
            weapons = GetComponent<CarWeapons>();
            progress = GetComponent<RaceProgress>();
            if (progress == null) progress = gameObject.AddComponent<RaceProgress>();
            int carLayer = LayerMask.NameToLayer("Car");
            carMask = carLayer >= 0 ? 1 << carLayer : Physics.AllLayers;
        }

        private void OnEnable()
        {
            if (Health != null) Health.OnDied += OnDied;
            if (Score != null && Health != null) Health.HitResolved += ArenaHit;
            Car.DriftBoosted += OnDriftBoost;
            Car.Teleported += OnReset;
            if (Progress != null) Progress.LapCompleted += OnLap;
        }

        private void OnDisable()
        {
            if (Health != null) Health.OnDied -= OnDied;
            if (Score != null && Health != null) Health.HitResolved -= ArenaHit;
            if (Car != null) { Car.DriftBoosted -= OnDriftBoost; Car.Teleported -= OnReset; }
            if (Progress != null) Progress.LapCompleted -= OnLap;
        }

        private void OnDriftBoost(int stage, float seconds)
        {
            if (!Car.inputEnabled) return;
            DriftBoosts++;
            if (Arena != null && Arena.Running) Score.DriftBoosted();
        }

        private void ArenaHit(HitInfo hit, HitResult result)
        {
            if (Arena == null || !Arena.Running || result.DamageDealt <= 0f) return;
            int tick = SimulationDriver.Instance.Tick;
            var source = hit.Attacker != null ? hit.Attacker.GetComponent<RaceParticipant>() : null;
            if (source == this || source == null || source.Arena != Arena) source = null;
            if (source != null)
            {
                source.Score.Hit(hit.HitType == HitType.Collision, source.Car.Body.linearVelocity.magnitude * 3.6f);
                if (!result.Fatal) Score.DamagedBy(source.Car.CarId, tick);
            }
            if (!result.Fatal) return;
            // The health component announces death before publishing the resolved fatal hit.
            var killer = source ?? Arena.FindCar(Score.RecentAttacker(tick));
            if (killer != null)
            {
                killer.KillTally.Record(Car.CarId);
                killer.Score.Knockout(false);
                Arena.FindCar(Score.RecentAttacker(tick, killer.Car.CarId))?.Score.Knockout(true);
            }
            Score.Died();
        }
        private void OnLap(int laps, double seconds)
        {
            var ev = RaceSetup.Event;
            if (ev != null && laps == ev.laps - 1) finalLapStarted = true;
        }
        private void OnReset()
        {
            wasOnFork = false; forkTravel = 0f; dirtCheckpoint = 0;
            if (!Car.inputEnabled) return;
            Resets++;
            if (finalLapStarted) finalLapMistake = true;
        }

        // Lost cars come back by themselves: a touch player with auto-throttle who wanders into the
        // grass must never have to find the track again (and AI reuse the same rule).
        private void FixedUpdate()
        {
            if (GameFlow.RoomReplica || Arena != null || Site != null) return;
            if (Car == null || !Car.inputEnabled || Progress == null || Progress.Path == null) return;
            if (Health != null && Health.IsDead) return;
            bool offAsphalt = Car.Surface == GroundSurface.Ground || (Car.Surface == GroundSurface.None && Car.IsGrounded == false && transform.position.y > -5f);
            float lateral = Mathf.Abs(Progress.Path.Spline.LateralOffset(transform.position, Progress.Distance));
            bool onFork = Progress.Path.OnFork(transform.position, out float fraction);
            var fork = Progress.Path.Fork;
            bool dirt = fork != null && fork.IsDirt;
            if (dirt) CountDirtPass(fork, onFork, fraction);
            else if (onFork && Progress.SpeedAlongTrack > 0f && Car.IsGrounded)
                forkTravel += Mathf.Min(Car.Body.linearVelocity.magnitude * Time.fixedDeltaTime, 5f);
            if (!dirt && wasOnFork && !onFork)
            {
                if (Progress.Path.Fork != null && forkTravel >= Progress.Path.Fork.Length * 0.7f && Car.IsGrounded && Progress.SpeedAlongTrack > 0f
                    && Vector3.Distance(transform.position, Progress.Path.Fork.Position(Progress.Path.reversed ? 0f : 1f)) < 20f)
                    Shortcuts++;
                forkTravel = 0f;
            }
            wasOnFork = onFork;
            bool lost = !onFork && lateral > lostLateralMetres;
            bool onDirtRoute = dirt && onFork && Car.Surface == GroundSurface.Ground;
            offTrackTime = ((offAsphalt && !onDirtRoute) || lost) ? offTrackTime + Time.fixedDeltaTime : 0f;
            if (offTrackTime >= offTrackResetSeconds || (!onFork && lateral > lostLateralMetres * 2f))
            {
                offTrackTime = 0f;
                AutoResets++;
                Car.ResetCar();
                if (Health != null) Health.SetInvulnerable(1.5f);
            }
        }

        // The ends of the low dirt road share the asphalt, and its tyres can leave the
        // crown briefly. Confirm the loose surface in order, then the return to asphalt.
        private void CountDirtPass(TrackFork fork, bool onFork, float fraction)
        {
            if (!Car.IsGrounded || Progress.SpeedAlongTrack <= 0f) return;
            if (onFork && Car.Surface == GroundSurface.Ground)
            {
                if (fraction < .38f) dirtCheckpoint = 1;
                else if (dirtCheckpoint == 1 && fraction < .65f) dirtCheckpoint = 2;
                else if (dirtCheckpoint == 2 && fraction >= .65f) dirtCheckpoint = 3;
            }
            if (dirtCheckpoint != 3 || Car.Surface != GroundSurface.Track || fraction < .84f) return;
            var exit = fork.Position(Progress.Path.reversed ? 0f : 1f);
            if ((transform.position - exit).sqrMagnitude > 20f * 20f) return;
            Shortcuts++;
            dirtCheckpoint = 0;
        }

        private void OnCollisionEnter(Collision collision)
        {
            PassBomb(collision);
            if (!Car.inputEnabled || collision.rigidbody == null || collision.contactCount == 0 || collision.relativeVelocity.sqrMagnitude < 36f) return;
            var rival = collision.rigidbody.GetComponent<RaceParticipant>();
            var towardRival = rival == null ? Vector3.zero : (rival.Car.Body.worldCenterOfMass - Car.Body.worldCenterOfMass).normalized;
            if (Vector3.Dot(transform.forward, towardRival) > .6f && Vector3.Dot(Car.Body.linearVelocity, towardRival) > 2f) Rams++;
        }
        private void OnCollisionStay(Collision collision) => PassBomb(collision);

        private void PassBomb(Collision collision)
        {
            if (HotBomb == null || collision.rigidbody == null) return;
            var other = collision.rigidbody.GetComponent<RaceParticipant>();
            if (other != null) HotBomb.Touch(this, other);
        }

        private void OnDied(GameObject attacker)
        {
            Deaths++;
            if (finalLapStarted) finalLapMistake = true;
            forkTravel = 0f; dirtCheckpoint = 0;
            EliminatedAtTick = SimulationDriver.Instance.Tick;
            if (Score == null && attacker != null)
            {
                var other = attacker.GetComponent<RaceParticipant>();
                if (other != null && other != this) other.KillTally.Record(Car != null ? Car.CarId : 0);   // RequireComponent(CarController): Car is always set
            }
        }

        /// <summary>
        /// A spot on the track a few metres behind where the car left it, facing forward, moved
        /// further back if another car occupies it.
        /// </summary>
        public bool TryGetRespawnPose(out Vector3 position, out Quaternion rotation)
        {
            if (Arena != null) return Arena.TryGetRecoveryPose(Car, out position, out rotation);
            if (Site != null) return Site.TryGetRecoveryPose(Car, out position, out rotation);
            position = default; rotation = default;
            var path = Progress != null ? Progress.Path : TrackPath.Instance;
            if (path == null || Progress == null) return false;

            float d = Progress.Distance;
            if (path.RespawnGuard != null) d = path.RespawnGuard(d);   // e.g. never over a bridge gap
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float candidate = path.Wrap(d - respawnSetback * (attempt + 1));
                float safeLane = Mathf.Clamp(laneOffset, -Mathf.Max(0f, path.halfWidth - 1.2f), Mathf.Max(0f, path.halfWidth - 1.2f));
                path.Pose(candidate, safeLane, out position, out rotation);
                position += Vector3.up * 0.5f;
                if (!Physics.CheckBox(position + Vector3.up * 0.5f, new Vector3(1.1f, 0.6f, 2.3f), rotation, carMask, QueryTriggerInteraction.Ignore))
                    return true;
            }
            return true; // crowded: use the last candidate anyway rather than not respawning
        }
    }
}
