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
        private Transform carFor;
        private float roll, turn;

        public float CurrentRoll => roll;

        private void Start()
        {
            cam = GetComponent<Camera>();
            if (cam != null) initialFov = cam.fieldOfView;
        }

        private static float Smooth(float speed, float dt) => 1f - Mathf.Exp(-speed * dt);

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;
            if (carFor != target) { carFor = target; car = target.GetComponent<CarController>(); }

            Vector3 velocity = targetRigidbody != null ? targetRigidbody.linearVelocity : Vector3.zero;
            Vector3 flatVel = new Vector3(velocity.x, 0f, velocity.z);
            float speed = flatVel.magnitude;
            float top = car != null ? Mathf.Max(1f, car.maxSpeed / 3.6f) : 33f;
            float speedNorm = Mathf.Clamp01(speed / top);

            // Heading used for framing: the bonnet, pulled towards the direction of travel while sliding.
            Vector3 fwd = target.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            if (speed > 8f && Vector3.Dot(flatVel, fwd) > 0f) fwd = Vector3.Slerp(fwd, flatVel / speed, followVelocity).normalized;
            var frame = Quaternion.LookRotation(fwd, Vector3.up);
            Vector3 right = frame * Vector3.right;

            // Yaw rate → turn amount (−1..1) and cornering load for the lean.
            float yawRate = targetRigidbody != null ? targetRigidbody.angularVelocity.y : 0f;              // rad/s, + = right
            float targetTurn = Mathf.Clamp(yawRate * Mathf.Rad2Deg / 90f, -1f, 1f);
            turn = Mathf.Lerp(turn, targetTurn, Smooth(4f, dt));
            float targetRoll = Mathf.Clamp(-speed * yawRate * rollPerAccel, -maxRoll, maxRoll);          // v·ω = lateral accel
            roll = Mathf.Lerp(roll, targetRoll, Smooth(3f, dt));

            Vector3 localOffset = offset;
            localOffset.z = Mathf.Lerp(offset.z + 0.4f, offset.z - distanceAtTopSpeed, speedNorm);
            if (car != null && car.IsBoosting) localOffset.y -= 0.2f;
            Vector3 desiredPosition = target.position + frame * localOffset - right * (turn * shiftAgainstTurn);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, Smooth(positionSmoothSpeed, dt));

            Vector3 lookTarget = target.position + Vector3.up * lookAtHeightOffset + fwd * 1.5f + right * (turn * lookIntoTurn);
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up) * Quaternion.AngleAxis(roll, Vector3.forward);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, Smooth(rotationSmoothSpeed, dt));

            if (cam == null) return;
            float targetFov;
            if (car != null) targetFov = Mathf.Lerp(fovAtRest, fovAtTopSpeed, speedNorm) + (car.IsBoosting ? nitroFovKick : 0f);
            else targetFov = Mathf.Clamp(initialFov + speed * speedFovMultiplier, 55f, 85f);
            float rate = targetFov > cam.fieldOfView ? fovAttack : fovRelease;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Smooth(rate, dt));
        }
    }
}
