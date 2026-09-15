using UnityEngine;
using RacingGame.Core.Sim;

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
        public bool isPlayer;
        [Tooltip("Lateral offset used for respawn so cars do not reappear on the centre line every time.")]
        public float laneOffset;
        [Tooltip("Metres behind the current position to respawn (looking forward again).")]
        public float respawnSetback = 6f;

        public CarController Car { get; private set; }
        public CarHealth Health { get; private set; }
        public RaceProgress Progress { get; private set; }
        /// <summary>Kills this race, per victim (the payout shrinks for repeat kills of the same rival).</summary>
        public KillTally KillTally { get; } = new KillTally();
        public int Kills => KillTally.Total;
        public int Deaths { get; set; }
        [Tooltip("Seconds off the asphalt before the car is put back on the track automatically.")]
        public float offTrackResetSeconds = 5f;
        [Tooltip("Lateral distance (m) from the centre line that counts as lost.")]
        public float lostLateralMetres = 20f;
        public int AutoResets { get; private set; }

        private int carMask;
        private float offTrackTime;

        private void Awake()
        {
            Car = GetComponent<CarController>();
            Health = GetComponent<CarHealth>();
            Progress = GetComponent<RaceProgress>();
            if (Progress == null) Progress = gameObject.AddComponent<RaceProgress>();
            int carLayer = LayerMask.NameToLayer("Car");
            carMask = carLayer >= 0 ? 1 << carLayer : Physics.AllLayers;
        }

        private void OnEnable()
        {
            if (Health != null) Health.OnDied += OnDied;
        }

        private void OnDisable()
        {
            if (Health != null) Health.OnDied -= OnDied;
        }

        // Lost cars come back by themselves: a touch player with auto-throttle who wanders into the
        // grass must never have to find the track again (and AI reuse the same rule).
        private void FixedUpdate()
        {
            if (Car == null || !Car.inputEnabled || Progress == null || Progress.Path == null) return;
            if (Health != null && Health.IsDead) return;
            bool offAsphalt = Car.Surface == GroundSurface.Ground || (Car.Surface == GroundSurface.None && Car.IsGrounded == false && transform.position.y > -5f);
            float lateral = Mathf.Abs(Progress.Path.Spline.LateralOffset(transform.position, Progress.Distance));
            bool lost = lateral > lostLateralMetres;
            offTrackTime = offAsphalt || lost ? offTrackTime + Time.fixedDeltaTime : 0f;
            if (offTrackTime >= offTrackResetSeconds || lateral > lostLateralMetres * 2f)
            {
                offTrackTime = 0f;
                AutoResets++;
                Car.ResetCar();
                if (Health != null) Health.SetInvulnerable(1.5f);
            }
        }

        private void OnDied(GameObject attacker)
        {
            Deaths++;
            if (attacker != null)
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
            position = default; rotation = default;
            var path = Progress != null ? Progress.Path : TrackPath.Instance;
            if (path == null || Progress == null) return false;

            float d = Progress.Distance;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float candidate = path.Wrap(d - respawnSetback * (attempt + 1));
                path.Pose(candidate, laneOffset, out position, out rotation);
                position += Vector3.up * 0.5f;
                if (!Physics.CheckBox(position + Vector3.up * 0.5f, new Vector3(1.1f, 0.6f, 2.3f), rotation, carMask, QueryTriggerInteraction.Ignore))
                    return true;
            }
            return true; // crowded: use the last candidate anyway rather than not respawning
        }
    }
}
