using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingGame
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
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
        public float steerSpeed = 6f;
        public float grip = 0.85f; // Lateral grip factor
        public float downforce = 60f;
        public float coastDeceleration = 2.5f; // m/s^2 of engine braking / rolling resistance with no throttle
        public Vector3 centerOfMassOffset = new Vector3(0f, -0.4f, 0.1f);

        [Header("Audio (Optional)")]
        public AudioSource engineAudio;

        [Header("Control")]
        public bool playerControlled = true;

        [Header("State")]
        [SerializeField] private float currentSpeedKmh;
        [SerializeField] private float currentSteerAngle;
        [SerializeField] private bool isGrounded;

        private Rigidbody rb;
        private float moveInput;
        private float steerInput;
        private bool isBraking;
        private Vector3 lastGroundedPos;
        private Quaternion lastGroundedRot;

        public float SpeedKmh => currentSpeedKmh;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 1250f;
            rb.centerOfMass = centerOfMassOffset;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 2.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            CreateWheelColliders();
            ApplyFrictionlessBodyMaterial();

            lastGroundedPos = transform.position + Vector3.up * 0.5f;
            lastGroundedRot = transform.rotation;
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
            if (playerControlled) ReadInput();
            UpdateWheelVisuals();
            UpdateAudio();

            // Reset car if flipped or fallen
            if (transform.position.y < -10f ||
                (playerControlled && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame))
            {
                ResetCar();
            }
        }

        private void FixedUpdate()
        {
            CheckGrounded();
            CalculateSpeed();
            ApplyMotor();
            ApplySteering();
            ApplyFriction();
            ApplyDownforce();

            if (isGrounded && rb.linearVelocity.magnitude < 5f && Vector3.Dot(transform.up, Vector3.up) > 0.7f)
            {
                lastGroundedPos = transform.position + Vector3.up * 0.5f;
                lastGroundedRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            }
        }

        private void ReadInput()
        {
            moveInput = 0f;
            steerInput = 0f;
            isBraking = false;

            if (Keyboard.current != null)
            {
                var k = Keyboard.current;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) moveInput += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) moveInput -= 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) steerInput -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) steerInput += 1f;
                if (k.spaceKey.isPressed) isBraking = true;
            }

            if (Gamepad.current != null)
            {
                var g = Gamepad.current;
                float stickX = g.leftStick.x.ReadValue();
                if (Mathf.Abs(stickX) > 0.15f) steerInput = stickX;

                float rt = g.rightTrigger.ReadValue();
                float lt = g.leftTrigger.ReadValue();
                if (rt > 0.05f || lt > 0.05f)
                {
                    moveInput = rt - lt;
                }
                if (g.buttonSouth.isPressed) isBraking = true;
            }
        }

        public void SetInput(float throttle, float steering, bool braking)
        {
            moveInput = Mathf.Clamp(throttle, -1f, 1f);
            steerInput = Mathf.Clamp(steering, -1f, 1f);
            isBraking = braking;
        }

        private void CheckGrounded()
        {
            // Raycast down from car center
            isGrounded = Physics.Raycast(transform.position + Vector3.up * 0.3f, -Vector3.up, out RaycastHit hit, 0.9f);
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

            if (moveInput > 0.05f)
            {
                if (forwardSpeed < -0.5f)
                {
                    // Braking when moving backward
                    rb.AddForce(transform.forward * brakeForce, ForceMode.Force);
                }
                else if (speedKmh < maxSpeed)
                {
                    float forceMultiplier = Mathf.Clamp01(1f - (speedKmh / maxSpeed));
                    rb.AddForce(transform.forward * (motorTorque * moveInput * (0.4f + 0.6f * forceMultiplier)), ForceMode.Force);
                }
            }
            else if (moveInput < -0.05f)
            {
                if (forwardSpeed > 0.5f)
                {
                    // Braking when moving forward
                    rb.AddForce(-transform.forward * brakeForce, ForceMode.Force);
                }
                else if (speedKmh < maxSpeed * 0.4f)
                {
                    rb.AddForce(transform.forward * (reverseTorque * moveInput), ForceMode.Force);
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
                rb.AddTorque(Vector3.up * (turnTorque * 120f), ForceMode.Force);
            }
        }

        private void ApplyFriction()
        {
            if (!isGrounded) return;

            // Cancel lateral slide to simulate tire grip
            Vector3 sideVelocity = Vector3.Dot(rb.linearVelocity, transform.right) * transform.right;
            float currentGrip = isBraking ? grip * 0.4f : grip;
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

        public void ResetCar()
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            transform.position = lastGroundedPos + Vector3.up * 0.8f;
            transform.rotation = lastGroundedRot;
        }

        private void OnGUI()
        {
            if (!playerControlled) return;

            // Speedometer & Controls overlay
            GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.fontSize = 16;
            boxStyle.normal.textColor = Color.white;
            boxStyle.alignment = TextAnchor.MiddleLeft;

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.fontSize = 24;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.2f);

            GUIStyle hintStyle = new GUIStyle(GUI.skin.label);
            hintStyle.fontSize = 14;
            hintStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

            GUILayout.BeginArea(new Rect(20, Screen.height - 175, 320, 155), GUI.skin.box);
            GUILayout.Label($"{Mathf.RoundToInt(Mathf.Abs(currentSpeedKmh))} KM/H", titleStyle);
            GUILayout.Label($"Управление: [W/A/S/D] или [Стрелки]", hintStyle);
            GUILayout.Label($"Тормоз / Ручник: [Пробел]", hintStyle);
            GUILayout.Label($"Восстановление машины: [R]", hintStyle);
            GUILayout.Label($"Выстрел из пушки: [ЛКМ] / [Ctrl]", hintStyle);
            GUILayout.EndArea();
        }
    }
}
