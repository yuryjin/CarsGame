using UnityEngine;
using RacingGame.Core.Sim;

namespace RacingGame
{
    public enum GroundSurface { None, Track, Ground, Other }

    /// <summary>
    /// Rigidbody car without WheelColliders: frictionless sphere "wheels" carry the body and the
    /// script applies motor, steering torque, lateral grip, downforce and coast deceleration as
    /// forces. Input arrives as a <see cref="DriveCommand"/> from an <see cref="ICarInputSource"/>
    /// on the same GameObject; the physics runs in <see cref="SimulationStep"/>, driven by
    /// <see cref="SimulationDriver"/> in CarId order. Update only does visuals and audio.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour, ISimulated
    {
        [Header("Wheel Meshes / Transforms")]
        public Transform frontLeftWheel;
        public Transform frontRightWheel;
        public Transform rearLeftWheel;
        public Transform rearRightWheel;
        public float wheelRadius = 0.34f;

        [Header("Vehicle Dynamics")]
        public float motorTorque = 7500f;
        public float maxSpeed = 120f; // km/h
        public float brakeForce = 9000f;
        public float reverseTorque = 3500f;
        public float maxSteerAngle = 32f;
        [Tooltip("Extra steering authority below 12 m/s, so cars can negotiate the track's tight hairpin.")]
        public float lowSpeedSteeringBoost = 3f;
        public float steerSpeed = 6f;
        public float grip = 0.85f; // Lateral grip factor
        public float downforce = 60f;
        public float coastDeceleration = 2.5f; // m/s^2 of engine braking / rolling resistance with no throttle
        [Tooltip("Extra deceleration (m/s^2) while on the Ground layer (grass), rewards staying on asphalt.")]
        public float offTrackDeceleration = 3.5f;
        [Tooltip("Speed cap (km/h) while on the Ground layer.")]
        public float offTrackMaxSpeedKmh = 70f;
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.4f, 0.1f);

        [Header("Drift (player only)")]
        [Tooltip("Brake + hard steer at speed slides instead of braking and charges a boost. The AI never sets this.")]
        public bool driftEnabled;
        [Tooltip("Lateral grip factor while drifting.")]
        public float driftGrip = 0.3f;
        [Tooltip("Extra yaw torque (N·m per m/s) into the drift, so the tail comes round.")]
        public float driftYawTorque = 130f;
        [Tooltip("Throttle held during a drift even when auto-throttle lifts for the brake.")]
        public float driftThrottle = 0.75f;

        [Header("Audio (Optional)")]
        public AudioSource engineAudio;

        [Header("Control")]
        [Tooltip("If no ICarInputSource is present on this object at Awake, a PlayerDriverInput is added when true.")]
        public bool playerControlled = true;
        [Tooltip("Commands are ignored while false (countdown, finish, wreck).")]
        public bool inputEnabled = true;

        [Header("State")]
        [SerializeField] private float currentSpeedKmh;
        [SerializeField] private float currentSteerAngle;
        [SerializeField] private bool isGrounded;
        [SerializeField] private GroundSurface groundSurface;

        private Rigidbody rb;
        private ICarInputSource inputSource;
        private CarCannon cannon;
        private CarWeapons weapons;
        private CarStatus status;
        private RaceProgress progress;
        private IRespawnPoseProvider respawnProvider;
        private float moveInput;
        private float steerInput;
        private bool isBraking;
        private Vector3 lastGroundedPos;
        private Quaternion lastGroundedRot;
        private int groundMask;
        private int trackLayer = -1, groundLayer = -1;
        private DriveCommand lastCommand;
        private int boostUntilTick = -1;
        private const float BoostSpeedFactor = 1.25f, BoostTorqueFactor = 1.6f;
        private readonly DriftCharge drift = new DriftCharge();

        public bool IsDrifting => drift.Drifting;
        /// <summary>0 while not drifting, 1–3 once the charge reaches a boost stage.</summary>
        public int DriftStage => drift.Drifting ? drift.Stage : 0;
        /// <summary>0..1 towards the top drift stage (HUD meter).</summary>
        public float DriftCharge01 => drift.Drifting ? Mathf.Clamp01(drift.Charge / drift.StageCharge[drift.StageCharge.Length - 1]) : 0f;
        /// <summary>Raised on the tick a drift ends in a boost: stage (1–3) and boost seconds.</summary>
        public event System.Action<int, float> DriftBoosted;

        public bool IsBoosting { get; private set; }
        /// <summary>Nitro: more top speed and torque for a few seconds.</summary>
        public void ApplyBoost(float seconds) { boostUntilTick = SimulationDriver.Instance.Tick + SimulationDriver.SecondsToTicks(seconds); }

        public float SpeedKmh => currentSpeedKmh;
        /// <summary>Sideways speed (m/s) of the body: the slide the tyres are fighting. Read by audio and effects.</summary>
        public float SideSlipSpeed { get; private set; }
        public bool IsGrounded => isGrounded;
        public GroundSurface Surface => groundSurface;
        public int CarId { get; private set; }
        public int SimulationOrder => CarId;
        public DriveCommand LastCommand => lastCommand;
        public ICarInputSource InputSource => inputSource;
        public CarStatus Status => status != null ? status : (status = GetComponent<CarStatus>() ?? gameObject.AddComponent<CarStatus>());
        public CarWeapons Weapons => weapons != null ? weapons : (weapons = GetComponent<CarWeapons>() ?? gameObject.AddComponent<CarWeapons>());
        public CarCannon Cannon => cannon != null ? cannon : (cannon = GetComponent<CarCannon>());

        // --- Steering model for drivers. ApplySteering turns an angle into a yaw torque
        // (angle · v/8 · 120, capped at 2.5 · maxAngle · 120) against the body's angular damping,
        // so a steady turn settles at yaw = torque / (I_y · damping).

        /// <summary>Largest wheel angle (deg) at this speed: it narrows towards top speed.</summary>
        public float EffectiveMaxSteer(float speedKmh) =>
            Mathf.Lerp(maxSteerAngle, maxSteerAngle * 0.55f, Mathf.Clamp01(Mathf.Abs(speedKmh) / maxSpeed));

        /// <summary>Wheel angle (deg) that holds a steady turn of this curvature (rad/m), ignoring the torque cap.</summary>
        private float SteeringGain(float speedMs) => Mathf.Lerp(Mathf.Max(1f, lowSpeedSteeringBoost), 1f, Mathf.Clamp01(Mathf.Abs(speedMs) / 12f));

        private float AngleForCurvature(float curvature, float speedMs) =>
            curvature * rb.inertiaTensor.y * Mathf.Max(0.1f, rb.angularDamping) / (15f * SteeringGain(speedMs));

        /// <summary>Steering input (-1..1) that holds a steady turn of this signed curvature (rad/m, +right).</summary>
        public float SteerForCurvature(float curvature, float speedMs)
        {
            if (rb == null) return 0f;
            float max = EffectiveMaxSteer(speedMs * 3.6f);
            float angle = AngleForCurvature(curvature, speedMs);
            // Above the torque cap the same yaw needs more angle than the cap lets through: saturate.
            float capSpeed = speedMs > 0.5f ? 20f * max / Mathf.Max(0.01f, Mathf.Abs(angle)) : float.MaxValue;
            if (speedMs > capSpeed) angle = Mathf.Sign(angle) * max;
            return Mathf.Clamp(angle / max, -1f, 1f);
        }

        /// <summary>Highest speed (m/s) at which this car can still hold a turn of this curvature (rad/m).</summary>
        public float MaxSpeedForCurvature(float curvature)
        {
            if (rb == null) return maxSpeed / 3.6f;
            float k = Mathf.Abs(curvature);
            if (k < 1e-4f) return maxSpeed / 3.6f;
            float top = maxSpeed / 3.6f;
            for (float v = top; v > 3f; v -= 0.5f)
            {
                float max = EffectiveMaxSteer(v * 3.6f);
                float angle = AngleForCurvature(k, v);
                // Needs the angle within reach and the torque (angle · v/8) under the cap.
                if (angle <= max && angle * v / 8f <= 2.5f * max) return v;
            }
            return 3f;
        }
        public Rigidbody Body => rb != null ? rb : (rb = GetComponent<Rigidbody>());

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 1250f;
            rb.centerOfMass = centerOfMassOffset;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 2.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            AssignCarLayer();
            ResolveGroundMask();
            CreateWheelColliders();
            ApplyFrictionlessBodyMaterial();

            inputSource = GetComponent<ICarInputSource>();
            if (inputSource == null && playerControlled) inputSource = gameObject.AddComponent<PlayerDriverInput>();
            cannon = GetComponent<CarCannon>();
            weapons = GetComponent<CarWeapons>();
            if (weapons == null) weapons = gameObject.AddComponent<CarWeapons>();
            status = GetComponent<CarStatus>();
            if (status == null) status = gameObject.AddComponent<CarStatus>();
            progress = GetComponent<RaceProgress>();
            respawnProvider = GetComponent<IRespawnPoseProvider>();

            CarId = RaceDirector.Instance.RegisterCar(this);

            lastGroundedPos = transform.position + Vector3.up * 0.5f;
            lastGroundedRot = transform.rotation;
        }

        private void OnEnable() { SimulationDriver.Instance.Register(this); }

        private void OnDisable()
        {
            var driver = SimulationDriver.Current; if (driver != null) driver.Unregister(this);
            moveInput = 0f; steerInput = 0f; isBraking = false;
            if (cannon != null) cannon.SetWantsFire(false);
            if (weapons != null) weapons.SetWantsSecondary(false);
        }

        private void OnDestroy() { var director = RaceDirector.Current; if (director != null) director.UnregisterCar(this); }

        /// <summary>Swap the driver at runtime (spawner assigns player/AI/network sources).</summary>
        public void SetInputSource(ICarInputSource source) { inputSource = source; }

        // Cars live on the "Car" layer so shells, AI avoidance casts and respawn overlap checks can
        // address them by mask. Only objects still on Default are moved, so authored layers survive.
        private void AssignCarLayer()
        {
            int carLayer = LayerMask.NameToLayer("Car");
            if (carLayer < 0 || gameObject.layer != 0) return;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.gameObject.layer == 0) t.gameObject.layer = carLayer;
        }

        private void ResolveGroundMask()
        {
            trackLayer = LayerMask.NameToLayer("Track");
            groundLayer = LayerMask.NameToLayer("Ground");
            int carLayer = LayerMask.NameToLayer("Car");
            int projLayer = LayerMask.NameToLayer("Projectile");
            groundMask = Physics.DefaultRaycastLayers;
            if (carLayer >= 0) groundMask &= ~(1 << carLayer);
            if (projLayer >= 0) groundMask &= ~(1 << projLayer);
            // Decorative colliders (tire stacks, trees) must not count as ground: standing on them would
            // classify as Other and suppress the off-track reset timer.
            int decoLayer = LayerMask.NameToLayer("Decoration");
            if (decoLayer >= 0) groundMask &= ~(1 << decoLayer);
        }

        // The car is carried by frictionless spheres at the wheel positions instead of the flat
        // body box: a sphere rolls over kerbs and the road/grass step (about 8 cm on this track),
        // while a box edge rams straight into them and the car gets stuck off the track.
        private void CreateWheelColliders()
        {
            CreateWheelColliders(transform, new[] { frontLeftWheel, frontRightWheel, rearLeftWheel, rearRightWheel }, wheelRadius);
        }

        // Shared with TargetCar so that every car in the scene stands on the same kind of wheels.
        public static void CreateWheelColliders(Transform root, Transform[] wheels, float radius)
        {
            foreach (var wheel in wheels)
            {
                if (wheel == null) continue;

                var wheelCollider = new GameObject(wheel.name + "_Collider");
                wheelCollider.layer = root.gameObject.layer;
                wheelCollider.transform.SetParent(root, false);
                wheelCollider.transform.localPosition = root.InverseTransformPoint(wheel.position);

                var sphere = wheelCollider.AddComponent<SphereCollider>();
                sphere.radius = radius;
            }
        }

        // Tyre grip is simulated by this script (ApplyFriction / ApplyDownforce), so the body and
        // wheel colliders themselves must be frictionless. With Unity's default physics material
        // the static friction between the colliders and the road (~0.6 * m * g, about 7 kN) is
        // larger than the motor force, and the car never starts moving.
        private void ApplyFrictionlessBodyMaterial()
        {
            ApplyFrictionlessBodyMaterial(gameObject);
        }

        public static void ApplyFrictionlessBodyMaterial(GameObject car)
        {
            var bodyMaterial = new PhysicsMaterial("CarBody_NoFriction")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            foreach (var bodyCollider in car.GetComponentsInChildren<Collider>())
            {
                bodyCollider.sharedMaterial = bodyMaterial;
            }
        }

        private void Update()
        {
            UpdateWheelVisuals();
            UpdateAudio();
        }

        /// <summary>Fixed-rate step, called by <see cref="SimulationDriver"/>. All gameplay state changes happen here.</summary>
        public void SimulationStep(int tick)
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (weapons == null) weapons = GetComponent<CarWeapons>();
            if (cannon == null) cannon = GetComponent<CarCannon>();
            if (status == null) status = GetComponent<CarStatus>();

            var command = inputEnabled && inputSource != null
                ? inputSource.Sample(tick)
                : (lastCommand.Tick == tick ? lastCommand : DriveCommand.None(tick));
            ApplyCommand(command);
            IsBoosting = tick < boostUntilTick;

            if (command.ResetEdge || transform.position.y < -10f) ResetCar();

            GroundQuery();
            CalculateSpeed();
            if (driftEnabled) StepDrift(); else if (drift.Drifting) drift.Cancel();
            SideSlipSpeed = rb != null ? Mathf.Abs(Vector3.Dot(rb.linearVelocity, transform.right)) : 0f;
            ApplyMotor();
            ApplySteering();
            ApplyFriction();
            ApplyDownforce();

            if (cannon != null && !cannon.locked)   // locked by the mines-only rule
            {
                cannon.SetWantsFire(command.Fire);
                cannon.SimulationStep(tick);
            }

            if (weapons != null)
            {
                weapons.SetWantsSecondary(command.Secondary);
                weapons.SimulationStep(tick);
            }

            if (progress != null) progress.SimulationStep(tick);

            if (isGrounded && rb != null && rb.linearVelocity.magnitude < 5f && Vector3.Dot(transform.up, Vector3.up) > 0.7f)
            {
                lastGroundedPos = transform.position + Vector3.up * 0.5f;
                lastGroundedRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }
        }

        // The brake becomes the drift button: while drifting the car keeps its throttle and slides on low grip
        // with extra yaw into the turn; releasing the brake fires the charged boost.
        private void StepDrift()
        {
            int stage = drift.Stage;
            float boost = drift.Step(isBraking, steerInput, rb.linearVelocity.magnitude, isGrounded, Time.fixedDeltaTime);
            if (boost > 0f) { ApplyBoost(boost); IsBoosting = true; DriftBoosted?.Invoke(stage, boost); }
            if (!drift.Drifting) return;
            isBraking = false;
            if (moveInput < driftThrottle) moveInput = driftThrottle;
        }

        public void ApplyCommand(in DriveCommand command)
        {
            lastCommand = command;
            moveInput = Mathf.Clamp(command.Throttle, -1f, 1f);
            steerInput = Mathf.Clamp(command.Steer, -1f, 1f);
            isBraking = command.Brake;
        }

        // One masked ground query for everything that needs "am I on the road": grounding,
        // off-track drag and later respawn logic. Never hits cars or shells.
        private void GroundQuery()
        {
            isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.3f, -Vector3.up, out RaycastHit hit, 0.9f, groundMask, QueryTriggerInteraction.Ignore);
            if (!isGrounded) { groundSurface = GroundSurface.None; return; }
            int layer = hit.collider.gameObject.layer;
            groundSurface = layer == trackLayer ? GroundSurface.Track : (layer == groundLayer ? GroundSurface.Ground : GroundSurface.Other);
        }

        private void CalculateSpeed()
        {
            currentSpeedKmh = Vector3.Dot(rb.linearVelocity, transform.forward) * 3.6f;
        }

        private void ApplyMotor()
        {
            if (!isGrounded) return;

            // Speed limit check
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            float speedKmh = Mathf.Abs(forwardSpeed) * 3.6f;

            if (isBraking)
            {
                // Clamp the brake impulse to the current velocity so the car comes to a full
                // stop instead of jittering back and forth around zero.
                Vector3 velocity = rb.linearVelocity;
                float maxDeltaV = brakeForce / rb.mass * Time.fixedDeltaTime;
                float deltaV = Mathf.Min(velocity.magnitude, maxDeltaV);
                rb.AddForce(-velocity.normalized * (deltaV * rb.mass), ForceMode.Impulse);
                return;
            }

            if (groundSurface == GroundSurface.Ground)
            {
                ApplyOffTrackDrag(forwardSpeed, speedKmh);
                if (speedKmh >= offTrackMaxSpeedKmh && moveInput > 0.05f) { return; }
            }

            float statusSpeedMul = status != null ? status.SpeedMultiplier : 1f;
            float top = (IsBoosting ? maxSpeed * BoostSpeedFactor : maxSpeed) * statusSpeedMul;
            float torque = (IsBoosting ? motorTorque * BoostTorqueFactor : motorTorque) * statusSpeedMul;

            if (moveInput > 0.05f)
            {
                if (forwardSpeed < -0.5f)
                {
                    // Braking when moving backward
                    rb.AddForce(transform.forward * brakeForce, ForceMode.Force);
                }
                else if (speedKmh < top)
                {
                    float forceMultiplier = Mathf.Clamp01(1f - (speedKmh / top));
                    rb.AddForce(transform.forward * (torque * moveInput * (0.4f + 0.6f * forceMultiplier)), ForceMode.Force);
                }
            }
            else if (moveInput < -0.05f)
            {
                if (forwardSpeed > 0.5f)
                {
                    // Braking when moving forward
                    rb.AddForce(-transform.forward * brakeForce, ForceMode.Force);
                }
                else if (speedKmh < maxSpeed * 0.4f * statusSpeedMul)
                {
                    rb.AddForce(transform.forward * (reverseTorque * moveInput * statusSpeedMul), ForceMode.Force);
                }
            }
            else
            {
                ApplyCoastDeceleration(forwardSpeed);
            }
        }

        // Engine braking / rolling resistance. The body colliders are frictionless, so without
        // this the car would keep rolling almost forever after the throttle is released.
        private void ApplyCoastDeceleration(float forwardSpeed)
        {
            float maxDeltaV = coastDeceleration * Time.fixedDeltaTime;
            float deltaV = Mathf.Clamp(-forwardSpeed, -maxDeltaV, maxDeltaV);
            rb.AddForce(transform.forward * (deltaV * rb.mass), ForceMode.Impulse);
        }

        // Grass is slow: extra rolling drag plus a speed cap, so cutting corners never pays.
        private void ApplyOffTrackDrag(float forwardSpeed, float speedKmh)
        {
            float maxDeltaV = offTrackDeceleration * Time.fixedDeltaTime;
            float deltaV = Mathf.Clamp(-forwardSpeed, -maxDeltaV, maxDeltaV);
            rb.AddForce(transform.forward * (deltaV * rb.mass), ForceMode.Impulse);
        }

        private void ApplySteering()
        {
            if (!isGrounded) return;

            // Target steering angle decreases slightly at high speed for stability
            float speedFactor = Mathf.Clamp01(Mathf.Abs(currentSpeedKmh) / maxSpeed);
            float effectiveMaxSteer = Mathf.Lerp(maxSteerAngle, maxSteerAngle * 0.55f, speedFactor);
            float targetSteer = steerInput * effectiveMaxSteer;

            currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteer, steerSpeed * 60f * Time.fixedDeltaTime);

            float speedMagnitude = rb.linearVelocity.magnitude;
            if (speedMagnitude > 0.5f)
            {
                float direction = Vector3.Dot(rb.linearVelocity, transform.forward) >= 0 ? 1f : -1f;
                float turnTorque = currentSteerAngle * direction * (speedMagnitude / 8f);
                turnTorque = Mathf.Clamp(turnTorque, -effectiveMaxSteer * 2.5f, effectiveMaxSteer * 2.5f);
                rb.AddTorque(Vector3.up * (turnTorque * 120f * SteeringGain(speedMagnitude)), ForceMode.Force);
                if (drift.Drifting)
                {
                    // Hold a slide angle of ~30°: push the tail out below it, stop pushing beyond it.
                    float slip = Vector3.SignedAngle(transform.forward, new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z), Vector3.up) * -drift.Direction;
                    float want = Mathf.Clamp01((32f - slip) / 20f);
                    rb.AddTorque(Vector3.up * (drift.Direction * driftYawTorque * speedMagnitude * want), ForceMode.Force);
                }
            }
        }

        private void ApplyFriction()
        {
            if (!isGrounded) return;

            // Cancel lateral slide to simulate tire grip
            Vector3 sideVelocity = Vector3.Dot(rb.linearVelocity, transform.right) * transform.right;
            float statusGripMul = status != null ? status.GripMultiplier : 1f;
            float currentGrip = (drift.Drifting ? grip * driftGrip : isBraking ? grip * 0.4f : grip) * statusGripMul;
            Vector3 lateralFrictionForce = -sideVelocity * (rb.mass * 8f * currentGrip);
            rb.AddForce(lateralFrictionForce * Time.fixedDeltaTime, ForceMode.Impulse);
        }

        private void ApplyDownforce()
        {
            if (isGrounded)
            {
                float speed = rb.linearVelocity.magnitude;
                rb.AddForce(-transform.up * (downforce * speed), ForceMode.Force);
            }
        }

        private void UpdateWheelVisuals()
        {
            float rotationAmount = (currentSpeedKmh / 3.6f) * Time.deltaTime * (180f / Mathf.PI) / 0.35f;

            if (frontLeftWheel != null)
            {
                frontLeftWheel.localRotation = Quaternion.Euler(0f, currentSteerAngle, 0f);
                if (frontLeftWheel.childCount > 0)
                    frontLeftWheel.GetChild(0).Rotate(Vector3.right, rotationAmount, Space.Self);
            }

            if (frontRightWheel != null)
            {
                frontRightWheel.localRotation = Quaternion.Euler(0f, currentSteerAngle, 0f);
                if (frontRightWheel.childCount > 0)
                    frontRightWheel.GetChild(0).Rotate(Vector3.right, rotationAmount, Space.Self);
            }

            if (rearLeftWheel != null && rearLeftWheel.childCount > 0)
            {
                rearLeftWheel.GetChild(0).Rotate(Vector3.right, rotationAmount, Space.Self);
            }

            if (rearRightWheel != null && rearRightWheel.childCount > 0)
            {
                rearRightWheel.GetChild(0).Rotate(Vector3.right, rotationAmount, Space.Self);
            }
        }

        private void UpdateAudio()
        {
            if (engineAudio == null) return;
            float pitch = Mathf.Lerp(0.8f, 2.2f, Mathf.Abs(currentSpeedKmh) / maxSpeed);
            engineAudio.pitch = pitch;
        }

        /// <summary>Fired after any teleport/reset so drivers can drop stale recovery state.</summary>
        public event System.Action Teleported;

        /// <summary>Put the car at a pose with zero velocity and neutral steering.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            currentSteerAngle = 0f;
            drift.Cancel();
            transform.SetPositionAndRotation(position, rotation);
            rb.position = position;
            rb.rotation = rotation;
            if (progress != null) progress.NotifyTeleport();
            Teleported?.Invoke();
        }

        /// <summary>Back onto the track: the respawn provider decides where, else the last grounded pose.</summary>
        public void ResetCar()
        {
            if (status == null) status = GetComponent<CarStatus>();
            if (status != null) status.ClearAllStatuses();

            if (respawnProvider == null) respawnProvider = GetComponent<IRespawnPoseProvider>();
            if (respawnProvider != null && respawnProvider.TryGetRespawnPose(out var pos, out var rot))
                Teleport(pos, rot);
            else
                Teleport(lastGroundedPos + Vector3.up * 0.8f, lastGroundedRot);
        }
    }
}
