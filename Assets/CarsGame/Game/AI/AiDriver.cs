using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// AI driver on the <see cref="TrackPath"/> spline. Heading-error steering towards a
    /// speed-scaled lookahead point on the chosen lane, corner speed from the curvature ahead,
    /// lane changes to pass slower cars (sphere casts on the Car layer), stuck recovery and a
    /// tunable <see cref="AiDriverProfile"/>. The race director can scale <see cref="speedMultiplier"/>
    /// for rubber-banding. All randomness comes from the director's AI stream.
    /// </summary>
    [RequireComponent(typeof(CarController))]
    public sealed class AiDriver : MonoBehaviour, ICarInputSource
    {
        public AiDriverProfile profile;
        [Tooltip("Lane the driver returns to when the road is clear (m, +right). Set by the spawner.")]
        public float baseLaneOffset;
        [Tooltip("Top-speed scale applied by rubber-banding (1 = none).")]
        public float speedMultiplier = 1f;
        [Tooltip("Comfortable braking deceleration used to plan corner entry (m/s^2).")]
        public float plannedDeceleration = 6f;

        [Header("Telemetry")]
        [SerializeField] private float offTrackTime;
        [SerializeField] private int resetCount;
        [SerializeField] private float currentLane;

        public float OffTrackTime => offTrackTime;
        public int ResetCount => resetCount;
        public float CurrentLane => currentLane;
        public float TargetLane => targetLane;
        public bool BoxedIn => boxedIn;
        public int PassDirection => passDirection;
        public bool Reversing => reversing;
        public float StuckTime => stuckTime;
        public float LastDesiredSpeed { get; private set; }
        public float LastCurvature { get; private set; }
        public float LastSideSlip { get; private set; }

        private CarController car;
        private AiGunner gunner;
        private RaceProgress progress;
        private TrackPath path;
        private RngStreams rng;
        private int carMask;
        private float targetLane;
        private float clearTime;
        private int passDirection;      // -1 left, +1 right, 0 none (latched until the pass resolves)
        private bool boxedIn;           // blocked ahead with no free side: hold the lane and brake
        private float stuckTime, reverseTime, overturnedTime;
        private bool reversing;
        private float noise, noiseTarget;
        private int ticksSinceEnable;
        private DriveCommand[] delayBuffer;
        private int delayHead;
        private readonly RaycastHit[] hits = new RaycastHit[4];

        private AiDriverProfile P => profile != null ? profile : AiDriverProfile.Default;

        private void Awake()
        {
            car = GetComponent<CarController>();
            car.playerControlled = false;
            gunner = GetComponent<AiGunner>();
            progress = GetComponent<RaceProgress>();
            if (progress == null) progress = gameObject.AddComponent<RaceProgress>();
            int carLayer = LayerMask.NameToLayer("Car");
            carMask = carLayer >= 0 ? 1 << carLayer : Physics.AllLayers;
        }

        private void OnEnable() { Configure(); if (car != null) car.Teleported += OnTeleported; }
        private void OnDisable() { if (car != null) car.Teleported -= OnTeleported; }

        // After a reset the car is on the line again: forget stuck/reverse state and buffered commands.
        private void OnTeleported()
        {
            reversing = false; stuckTime = reverseTime = overturnedTime = 0f;
            passDirection = 0; boxedIn = false;
            if (delayBuffer != null) System.Array.Clear(delayBuffer, 0, delayBuffer.Length);
            delayHead = 0;
        }

        /// <summary>Assign personality and lane and reset all driving state. Call after AddComponent.</summary>
        public void Initialize(AiDriverProfile driverProfile, float lane)
        {
            profile = driverProfile;
            baseLaneOffset = lane;
            Configure();
        }

        private void Configure()
        {
            path = TrackPath.Instance;
            rng = RaceDirector.Instance.Rng;
            targetLane = currentLane = baseLaneOffset;
            passDirection = 0; boxedIn = false; clearTime = 0f;
            ticksSinceEnable = 0;
            reversing = false; stuckTime = reverseTime = overturnedTime = 0f;
            noise = noiseTarget = 0f;
            int delayTicks = SimulationDriver.SecondsToTicks(P.reactionDelay);
            delayBuffer = P.reactionDelay > 0.001f ? new DriveCommand[delayTicks] : null;
            delayHead = 0;
        }

        public DriveCommand Sample(int tick)
        {
            if (path == null) path = TrackPath.Instance;
            if (path == null || progress == null) return DriveCommand.None(tick);
            ticksSinceEnable++;

            var cmd = Decide(tick);

            if (delayBuffer == null || delayBuffer.Length == 0) return cmd;
            var delayed = delayBuffer[delayHead];
            delayBuffer[delayHead] = cmd;
            delayHead = (delayHead + 1) % delayBuffer.Length;
            // Edges must not be lost or duplicated by the delay line.
            delayed.ResetEdge = cmd.ResetEdge;
            if (delayed.Tick == 0) return DriveCommand.None(tick);
            delayed.Tick = tick;
            return delayed;
        }

        private DriveCommand Decide(int tick)
        {
            float dt = SimulationDriver.FixedStep;
            float speedMs = Mathf.Abs(car.SpeedKmh) / 3.6f;
            float speedKmh = Mathf.Abs(car.SpeedKmh);
            float d = progress.Distance;

            // --- Lane choice: pass slower traffic, otherwise drift back to the base lane.
            UpdateLane(dt, speedMs);

            // --- Steering: heading error towards a lookahead point on the lane.
            float lookahead = Mathf.Clamp(speedMs * 0.6f, 8f, 18f);
            float ahead = path.Wrap(d + lookahead);
            Vector3 target = path.Position(ahead) + path.Right(ahead) * currentLane;
            Vector3 toTarget = target - transform.position; toTarget.y = 0f;
            Vector3 local = transform.InverseTransformDirection(toTarget.normalized);
            float turnAngle = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float gain = Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(speedMs / 33f));
            noiseTarget = Mathf.Lerp(noiseTarget, rng.Range(RngStreams.Stream.Ai, -1f, 1f), 0.02f);
            noise = Mathf.Lerp(noise, noiseTarget, 0.1f);
            // Yaw-rate damping stops the weave a pure heading-error controller produces at low speed.
            float yawRate = car.Body.angularVelocity.y * Mathf.Rad2Deg;   // deg/s, +right
            float steer = Mathf.Clamp(turnAngle / 35f * gain - yawRate / 180f * 0.6f + noise * P.steerNoise, -1f, 1f);

            // --- Corner speed from curvature within the braking distance.
            float top = car.maxSpeed * P.topSpeedFactor * speedMultiplier;
            float brakeWindow = speedMs * speedMs / (2f * plannedDeceleration) + 10f;
            float curvature = path.CurvatureAhead(d, brakeWindow);           // deg per metre
            float corner = Mathf.InverseLerp(0.6f, 3.5f, curvature);
            float desired = Mathf.Lerp(top, top * 0.45f, corner) * P.corneringConfidence;
            // Sharp heading error right now also means slow down.
            float immediate = Mathf.InverseLerp(25f, 75f, Mathf.Abs(turnAngle));
            desired = Mathf.Min(desired, Mathf.Lerp(top, top * 0.4f, immediate));

            // Understeer/drift guard: the lateral grip is a force, so past a few m/s of side slip
            // the car ploughs straight on regardless of steering — lift off until it bites again.
            float sideSlip = Mathf.Abs(Vector3.Dot(car.Body.linearVelocity, transform.right));
            LastSideSlip = sideSlip;

            // Boxed in (car ahead, both sides taken): match its pace instead of ramming through.
            if (boxedIn) desired = Mathf.Min(desired, Mathf.Max(20f, speedKmh - 8f));
            LastDesiredSpeed = desired; LastCurvature = curvature;

            float throttle;
            bool brake = false;
            if (speedKmh > desired + 5f) { throttle = 0f; brake = true; }
            else if (speedKmh > desired) throttle = 0f;
            else throttle = 1f;
            if (sideSlip > 3f) throttle = Mathf.Min(throttle, 0f);
            if (sideSlip > 5f) brake = true;

            // Start ramp so the grid does not pile into itself.
            throttle *= Mathf.Min(1f, 0.5f + ticksSinceEnable / 75f);

            // --- Off the asphalt: come back to the centre line and do not push harder.
            if (car.Surface == GroundSurface.Ground)
            {
                offTrackTime += dt;
                targetLane = 0f;
                desired = Mathf.Min(desired, car.offTrackMaxSpeedKmh);
            }

            // --- Stuck recovery: reverse a moment, then give up and respawn.
            bool reset = false;
            if (speedMs < 0.55f && throttle > 0.5f && ticksSinceEnable > 100) stuckTime += dt; else if (speedMs > 1.5f) stuckTime = 0f;
            if (!reversing && stuckTime > 2.5f) { reversing = true; reverseTime = 0f; }
            if (reversing)
            {
                reverseTime += dt;
                throttle = -1f; brake = false; steer = -steer;
                if (reverseTime > 1.2f) { reversing = false; stuckTime = Mathf.Max(0f, stuckTime - 1.5f); }
            }
            if (stuckTime > 6f) { reset = true; stuckTime = 0f; reversing = false; resetCount++; }

            // --- Overturned or fallen off the world.
            bool overturned = transform.position.y < -5f || Vector3.Dot(transform.up, Vector3.up) < 0.2f;
            overturnedTime = overturned ? overturnedTime + dt : 0f;
            if (overturnedTime > 2f) { reset = true; overturnedTime = 0f; resetCount++; }

            bool fire = gunner != null && !reversing && gunner.WantsFire(tick);
            return new DriveCommand { Tick = tick, Throttle = throttle, Steer = steer, Brake = brake, Fire = fire, ResetEdge = reset }.Clamped();
        }

        private void UpdateLane(float dt, float speedMs)
        {
            float halfWidth = path.halfWidth;
            float limit = Mathf.Max(0.5f, halfWidth - 1.2f);

            Vector3 origin = transform.position + transform.forward * 2.2f + Vector3.up * 0.5f;
            float range = 14f;
            bool centre = Cast(origin, transform.forward, range, out var centreHit);
            bool left = Cast(origin, Quaternion.AngleAxis(-10f, Vector3.up) * transform.forward, range, out _);
            bool right = Cast(origin, Quaternion.AngleAxis(10f, Vector3.up) * transform.forward, range, out _);

            bool blocked = false;
            if (centre)
            {
                var otherBody = centreHit.rigidbody;
                float otherSpeed = otherBody != null ? Vector3.Dot(otherBody.linearVelocity, transform.forward) : 0f;
                blocked = otherSpeed < speedMs - 0.5f || speedMs < 2f;
            }

            boxedIn = false;
            if (blocked)
            {
                clearTime = 0f;
                if (passDirection == 0)
                {
                    // Choose once and latch: free side first, else the side with more room.
                    if (!left && right) passDirection = -1;
                    else if (left && !right) passDirection = +1;
                    else if (!left && !right) passDirection = currentLane <= 0f ? +1 : -1;
                    else passDirection = 0;
                }
                bool sideFree = passDirection < 0 ? !left : (passDirection > 0 ? !right : false);
                if (passDirection != 0 && sideFree)
                {
                    targetLane = Mathf.Clamp(currentLane + passDirection * 2.6f, -limit, limit);
                    // No room left on that side: give up the pass and hold.
                    if (Mathf.Abs(targetLane - currentLane) < 0.3f) { passDirection = 0; boxedIn = true; targetLane = currentLane; }
                }
                else
                {
                    passDirection = 0;
                    boxedIn = true;
                    targetLane = currentLane;
                }
            }
            else
            {
                if (left && !right) targetLane = Mathf.Clamp(targetLane + 1f, -limit, limit);
                else if (right && !left) targetLane = Mathf.Clamp(targetLane - 1f, -limit, limit);
                clearTime += dt;
                // Hysteresis: keep the pass lane a moment after the road clears, then drift home.
                if (clearTime > 0.5f) passDirection = 0;
                if (clearTime > 3f) targetLane = Mathf.Clamp(baseLaneOffset, -limit, limit);
            }

            currentLane = Mathf.MoveTowards(currentLane, targetLane, 3f * dt);
        }

        private bool Cast(Vector3 origin, Vector3 dir, float range, out RaycastHit best)
        {
            best = default;
            int n = Physics.SphereCastNonAlloc(origin, 0.6f, dir, hits, range, carMask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == null || h.collider.transform.root == transform.root) continue;
                if (h.distance < nearest) { nearest = h.distance; best = h; found = true; }
            }
            return found;
        }
    }
}
