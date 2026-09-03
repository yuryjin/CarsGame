using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Menu backdrop camera: slow dolly along the track spline with a few alternating shots. Owns the
    /// camera only in FlowState.Menu; GameFlow disables it before spawning a race so CarCameraFollow
    /// takes over without a fight, and it re-enables itself when the flow returns to the menu.
    /// </summary>
    public sealed class MenuCameraDirector : MonoBehaviour
    {
        public float speed = 9f;
        public float shotSeconds = 9f;
        public float menuFov = 62f;

        public static MenuCameraDirector Instance { get; private set; }

        private struct Shot { public float Lateral, Height, LookAhead, LookHeight; }
        private static readonly Shot[] Shots =
        {
            new Shot { Lateral = 0f, Height = 3f, LookAhead = 30f, LookHeight = 1.2f },
            new Shot { Lateral = -10f, Height = 6.5f, LookAhead = 24f, LookHeight = 1f },
            new Shot { Lateral = 8f, Height = 4.5f, LookAhead = 34f, LookHeight = 1.5f },
            new Shot { Lateral = 0f, Height = 13f, LookAhead = 48f, LookHeight = 0f },
        };

        private GameFlow flow;
        private CarCameraFollow follow;
        private Camera cam;
        private float distance = 60f;
        private float shotTimer;
        private int shot;
        private bool active;

        private void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            follow = GetComponent<CarCameraFollow>();
            flow = FindFirstObjectByType<GameFlow>();
        }

        private void OnEnable() { if (flow != null) flow.StateChanged += OnState; if (flow != null && flow.State == FlowState.Menu) Activate(true); }
        private void OnDisable() { if (flow != null) flow.StateChanged -= OnState; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void OnState(FlowState s) { Activate(s == FlowState.Menu); }

        /// <summary>Explicit hand-over: the race flow calls Activate(false) before it spawns cars.</summary>
        public void Activate(bool on)
        {
            active = on;
            if (follow != null) follow.enabled = !on;
            if (on && cam != null) cam.fieldOfView = menuFov;
            if (on) { shotTimer = 0f; SnapToShot(); }
        }

        private void SnapToShot()
        {
            var track = TrackPath.Instance; if (track == null) return;
            Pose(track, Shots[shot], out var p, out var r);
            transform.SetPositionAndRotation(p, r);
        }

        private void Pose(TrackPath track, Shot s, out Vector3 pos, out Quaternion rot)
        {
            pos = track.Position(distance) + track.Right(distance) * s.Lateral + Vector3.up * s.Height;
            Vector3 look = track.Position(distance + s.LookAhead) + Vector3.up * s.LookHeight;
            rot = Quaternion.LookRotation(look - pos, Vector3.up);
        }

        private void LateUpdate()
        {
            if (!active) return;
            var track = TrackPath.Instance; if (track == null) return;
            float dt = Time.unscaledDeltaTime;
            distance = track.Wrap(distance + speed * dt);
            shotTimer += dt;
            if (shotTimer > shotSeconds) { shotTimer = 0f; shot = (shot + 1) % Shots.Length; distance = track.Wrap(distance + 140f); SnapToShot(); return; }
            Pose(track, Shots[shot], out var p, out var r);
            transform.position = Vector3.Lerp(transform.position, p, 1f - Mathf.Exp(-4f * dt));
            transform.rotation = Quaternion.Slerp(transform.rotation, r, 1f - Mathf.Exp(-4f * dt));
        }
    }
}
