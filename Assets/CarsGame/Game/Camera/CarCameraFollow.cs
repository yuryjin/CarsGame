using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Chase camera. Sells speed with FOV and distance that grow with speed (plus a nitro kick),
    /// looks a little into the turn, leans up to a few degrees with the cornering load and, while the
    /// car slides, frames along the direction of travel. Visual only (LateUpdate); smoothing is
    /// frame-rate independent. <see cref="CameraImpulse"/> adds shake afterwards.
    /// </summary>
    public class CarCameraFollow : MonoBehaviour
    {
        public Transform target;
        public Rigidbody targetRigidbody;
        public Vector3 offset = new Vector3(0f, 2.2f, -5.2f);
        public float positionSmoothSpeed = 10f;
        public float rotationSmoothSpeed = 8f;
        public float lookAtHeightOffset = 1.0f;
        [Tooltip("Legacy: FOV added per m/s when no CarController is found on the target.")]
        public float speedFovMultiplier = 0.12f;

        [Header("Speed feel")]
        public float fovAtRest = 56f;
        public float fovAtTopSpeed = 72f;
        public float nitroFovKick = 8f;
        [Tooltip("FOV follows faster when it grows than when it shrinks (per second).")]
        public float fovAttack = 8f, fovRelease = 3f;
        [Tooltip("Extra distance behind the car at top speed (m); at rest the camera sits 0.4 m closer than offset.")]
        public float distanceAtTopSpeed = 1.3f;
        [Tooltip("Fixed field of view when speed zoom and corner lean are disabled.")]
        public float steadyFov = 64f;

        [Header("Corners")]
        [Tooltip("How far the aim point moves into the turn at full yaw rate (m).")]
        public float lookIntoTurn = 1.8f;
        [Tooltip("Sideways camera shift against the turn at full yaw rate (m), so the apex stays in view.")]
        public float shiftAgainstTurn = 0.6f;
        [Tooltip("Degrees of lean per m/s² of cornering load, capped by maxRoll.")]
        public float rollPerAccel = 0.3f, maxRoll = 4f;
        [Range(0f, 1f)] [Tooltip("While sliding, how much the camera frames the direction of travel instead of the bonnet.")]
        public float followVelocity = 0.35f;

        private Camera cam;
        private float initialFov;
        private CarController car;
        private PlayerDriverInput driver;
        private bool rear;
        private Transform carFor;
        private float roll, turn;
        private Vector3 up = Vector3.up;

        public float CurrentRoll => roll;
        private static bool DynamicView => PlatformGlue.Save == null || PlatformGlue.Save.Data.settings.dynamicCamera;

        private Vector3 ChaseOffset(bool dynamicView, float speedNorm)
        {
            var chase = offset;
            chase.z += dynamicView ? Mathf.Lerp(.4f, -distanceAtTopSpeed, speedNorm) : -.5f;
            int distance = PlatformGlue.Save?.Data.settings.cameraDistance ?? 0;
            float scale = distance < 0 ? 0.8f : distance > 0 ? 1.3f : 1f;
            chase.y = lookAtHeightOffset + (chase.y - lookAtHeightOffset) * scale;
            chase.z *= scale;
            return chase;
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null) initialFov = cam.fieldOfView;
        }

        private void OnDisable() => DetachTarget();
        private void OnDestroy() => DetachTarget();

        private void DetachTarget()
        {
            if (car != null) car.Teleported -= SnapToTarget;
            car = null;
            driver = null; rear = false;
            carFor = null;
        }

        /// <summary>Bind a new car and start from its clean chase pose, including after a wall ride.</summary>
        public void SetTarget(Transform next)
        {
            DetachTarget();
            target = next;
            carFor = next;
            targetRigidbody = next != null ? next.GetComponent<Rigidbody>() : null;
            car = next != null ? next.GetComponent<CarController>() : null;
            driver = next != null ? next.GetComponent<PlayerDriverInput>() : null;
            if (car != null) car.Teleported += SnapToTarget;
            SnapToTarget();
        }

        private Vector3 Heading(Vector3 normal)
        {
            Vector3 forward = Vector3.ProjectOnPlane(target.forward, normal);
            if (forward.sqrMagnitude < 1e-4f)
                forward = Vector3.ProjectOnPlane(Mathf.Abs(normal.z) < .9f ? Vector3.forward : Vector3.right, normal);
            return forward.normalized;
        }

        /// <summary>A recovery is a cut: never smooth from the old side of the track or an inverted horizon.</summary>
        public void SnapToTarget()
        {
            if (target == null || !isActiveAndEnabled) return;
            GetComponent<CameraImpulse>()?.ResetEnergy();
            up = car != null ? car.GravityUp : Vector3.up;
            roll = turn = 0f;
            Vector3 forward = Heading(up);
            rear = driver != null && driver.RearViewHeld;
            if (rear) forward = -forward;
            var frame = Quaternion.LookRotation(forward, up);
            transform.position = target.position + frame * ChaseOffset(DynamicView, 0f);
            transform.rotation = Quaternion.LookRotation(target.position + up * lookAtHeightOffset + forward * 1.5f - transform.position, up);
            if (cam != null) cam.fieldOfView = !DynamicView ? steadyFov : car != null ? fovAtRest : initialFov;
        }

        private static float Smooth(float speed, float dt) => 1f - Mathf.Exp(-speed * dt);

        private void LateUpdate()
        {
            if (target == null) { DetachTarget(); return; }
            float dt = Time.deltaTime;
            bool dynamicView = DynamicView;
            if (carFor != target) SetTarget(target);
            if (rear != (driver != null && driver.RearViewHeld)) SnapToTarget();

            // The car's own "up": world up, or the wall/ceiling normal while it wall rides; eased so the view rolls over smoothly.
            up = Vector3.Slerp(up, car != null ? car.GravityUp : Vector3.up, Smooth(5f, dt)).normalized;
            Vector3 velocity = targetRigidbody != null ? targetRigidbody.linearVelocity : Vector3.zero;
            Vector3 flatVel = Vector3.ProjectOnPlane(velocity, up);
            float speed = flatVel.magnitude;
            float top = car != null ? Mathf.Max(1f, car.maxSpeed / 3.6f) : 33f;
            float speedNorm = Mathf.Clamp01(speed / top);

            // Heading used for framing: the bonnet, pulled towards the direction of travel while sliding.
            Vector3 fwd = Heading(up);
            if (speed > 8f && Vector3.Dot(flatVel, fwd) > 0f) fwd = Vector3.Slerp(fwd, flatVel / speed, followVelocity).normalized;
            if (rear) fwd = -fwd;
            var frame = Quaternion.LookRotation(fwd, up);
            Vector3 right = frame * Vector3.right;

            // Yaw rate → turn amount (−1..1) and cornering load for the lean.
            float yawRate = targetRigidbody != null ? Vector3.Dot(targetRigidbody.angularVelocity, up) : 0f;   // rad/s, + = right
            float targetTurn = Mathf.Clamp(yawRate * Mathf.Rad2Deg / 90f, -1f, 1f);
            turn = Mathf.Lerp(turn, targetTurn, Smooth(4f, dt));
            float targetRoll = Mathf.Clamp(-speed * yawRate * rollPerAccel, -maxRoll, maxRoll);          // v·ω = lateral accel
            roll = dynamicView ? Mathf.Lerp(roll, targetRoll, Smooth(3f, dt)) : 0f;

            Vector3 localOffset = ChaseOffset(dynamicView, speedNorm);
            if (dynamicView && car != null && car.IsBoosting) localOffset.y -= 0.2f;
            Vector3 desiredPosition = target.position + frame * localOffset - right * (turn * shiftAgainstTurn);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Smooth(positionSmoothSpeed, dt));

            Vector3 lookTarget = target.position + up * lookAtHeightOffset + fwd * 1.5f + right * (turn * lookIntoTurn);
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, up) * Quaternion.AngleAxis(roll, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Smooth(rotationSmoothSpeed, dt));

            if (cam == null) return;
            float targetFov;
            if (!dynamicView) targetFov = steadyFov;
            else if (car != null) targetFov = Mathf.Lerp(fovAtRest, fovAtTopSpeed, speedNorm) + (car.IsBoosting ? nitroFovKick : 0f);
            else targetFov = Mathf.Clamp(initialFov + speed * speedFovMultiplier, 55f, 85f);
            float rate = targetFov > cam.fieldOfView ? fovAttack : fovRelease;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Smooth(rate, dt));
        }
    }
}
