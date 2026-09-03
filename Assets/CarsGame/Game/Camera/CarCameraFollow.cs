using UnityEngine;

namespace RacingGame
{
    public class CarCameraFollow : MonoBehaviour
    {
        public Transform target;
        public Rigidbody targetRigidbody;
        public Vector3 offset = new Vector3(0f, 2.2f, -5.2f);
        public float positionSmoothSpeed = 10f;
        public float rotationSmoothSpeed = 8f;
        public float lookAtHeightOffset = 1.0f;
        public float speedFovMultiplier = 0.12f;

        private Camera cam;
        private float initialFov;

        private void Start()
        {
            cam = GetComponent<Camera>();
            if (cam != null) initialFov = cam.fieldOfView;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            // Calculate target world position based on target rotation
            Vector3 desiredPosition = target.TransformPoint(offset);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed * Time.deltaTime);

            // Look at target with offset
            Vector3 lookTarget = target.position + Vector3.up * lookAtHeightOffset + target.forward * 1.5f;
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);

            // Dynamic FOV based on speed
            if (cam != null && targetRigidbody != null)
            {
                float speed = targetRigidbody.linearVelocity.magnitude;
                float targetFov = Mathf.Clamp(initialFov + speed * speedFovMultiplier, 55f, 85f);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 4f * Time.deltaTime);
            }
        }
    }
}
