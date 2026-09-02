using UnityEngine;

namespace RacingGame
{
    [RequireComponent(typeof(CarController))]
    public sealed class OpponentDriver : MonoBehaviour
    {
        public CarController car;
        public Transform waypoints;
        public float targetSpeedKmh = 95f;
        public float laneOffset;

        private const float WaypointRadius = 9f;
        private Vector3[] route;
        private int waypointIndex;
        private float overturnedTime;

        private void Awake()
        {
            if (car == null) car = GetComponent<CarController>();
            car.playerControlled = false;

            if (waypoints == null || waypoints.childCount < 3)
            {
                Debug.LogError($"{name}: opponent route needs at least three waypoints.", this);
                enabled = false;
                return;
            }

            route = BuildRoute();
            bool validRoute = IsValidRoute(route);
            Debug.Assert(validRoute, $"{name}: opponent route contains duplicate waypoints.", this);
            if (!validRoute)
            {
                enabled = false;
                return;
            }

            int nearest = FindNearestWaypoint();
            waypointIndex = (nearest + 2) % route.Length;
            Vector3 forward = route[waypointIndex] - transform.position;
            forward.y = 0f;
            if (forward.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        private void Update()
        {
            if (!car.enabled) return;

            Vector3 toTarget = AdvancePassedWaypoints();
            Vector3 localTarget = transform.InverseTransformDirection(toTarget.normalized);
            float turnAngle = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;
            float steering = Mathf.Clamp(turnAngle / 35f, -1f, 1f);

            float corner = Mathf.InverseLerp(12f, 65f, Mathf.Abs(turnAngle));
            float desiredSpeed = Mathf.Lerp(targetSpeedKmh, targetSpeedKmh * 0.45f, corner);
            float speed = Mathf.Abs(car.SpeedKmh);
            bool braking = speed > desiredSpeed + 5f;
            car.SetInput(braking ? 0f : 1f, steering, braking);

            bool overturned = transform.position.y < -5f || Vector3.Dot(transform.up, Vector3.up) < 0.2f;
            overturnedTime = overturned ? overturnedTime + Time.deltaTime : 0f;
            if (overturnedTime > 2f)
            {
                car.ResetCar();
                waypointIndex = (FindNearestWaypoint() + 2) % route.Length;
                overturnedTime = 0f;
            }
        }

        private Vector3[] BuildRoute()
        {
            int count = waypoints.childCount;
            var points = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 previous = waypoints.GetChild((i - 1 + count) % count).position;
                Vector3 next = waypoints.GetChild((i + 1) % count).position;
                Vector3 tangent = next - previous;
                tangent.y = 0f;
                Vector3 right = tangent.sqrMagnitude > 0.01f
                    ? Vector3.Cross(Vector3.up, tangent.normalized)
                    : Vector3.right;
                points[i] = waypoints.GetChild(i).position + right * laneOffset;
            }

            return points;
        }

        private Vector3 AdvancePassedWaypoints()
        {
            Vector3 toTarget = Vector3.zero;

            for (int i = 0; i < route.Length; i++)
            {
                toTarget = route[waypointIndex] - transform.position;
                toTarget.y = 0f;
                Vector3 localTarget = transform.InverseTransformDirection(toTarget);
                if (toTarget.sqrMagnitude > WaypointRadius * WaypointRadius && localTarget.z > 0f) break;
                waypointIndex = (waypointIndex + 1) % route.Length;
            }

            return toTarget;
        }

        private int FindNearestWaypoint()
        {
            int nearest = 0;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < route.Length; i++)
            {
                Vector3 delta = route[i] - transform.position;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = i;
            }

            return nearest;
        }

        private static bool IsValidRoute(Vector3[] points)
        {
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 delta = points[(i + 1) % points.Length] - points[i];
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.25f) return false;
            }

            return true;
        }

        private void OnDisable()
        {
            if (car != null) car.SetInput(0f, 0f, false);
        }
    }
}
