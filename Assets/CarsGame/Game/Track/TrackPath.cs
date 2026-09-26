using UnityEngine;
using RacingGame.Core.Track;

namespace RacingGame
{
    /// <summary>
    /// Scene wrapper for <see cref="TrackSpline"/>: builds the loop from the ordered children of
    /// this transform (the Track_Markings dashes) at Awake. Single source of truth for "where is
    /// the track" — AI, race progress, respawn and the start grid all read it.
    /// </summary>
    public sealed class TrackPath : MonoBehaviour
    {
        [Tooltip("Drive the loop in the opposite direction (reverse events).")]
        public bool reversed;
        [Tooltip("Half of the drivable width in metres (kerb to centre line).")]
        public float halfWidth = 4.6f;
        [Tooltip("Distance along the loop where the start/finish line sits.")]
        public float startLineDistance = 40f;

        [Tooltip("Corner-speed profile and racing line for the AI (rebuilt with the spline).")]
        public TrackProfile.Settings profileSettings = TrackProfile.Settings.Default;

        private TrackSpline spline;
        private TrackProfile profile;
        private static TrackPath instance;
        public TrackFork Fork { get; internal set; }

        /// <summary>A lane the AI heads for when a road feature (a boost pad) is within <c>reach</c> metres ahead.</summary>
        public struct LaneLure { public float distance, lane, reach; }
        /// <summary>Lures of the current layout (null = none). Set by a generated track, cleared with it.</summary>
        public LaneLure[] Lures;
        /// <summary>Maps the distance a car left the road at to a safe respawn distance (null = unchanged).</summary>
        public System.Func<float, float> RespawnGuard;

        private Vector3[] layout;
        private float authoredHalfWidth, authoredStartLine;

        public static TrackPath Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<TrackPath>();
                return instance;
            }
        }

        public TrackSpline Spline
        {
            get { if (spline == null) Build(); return spline; }
        }

        public float Length => Spline.Length;

        private void Awake()
        {
            instance = this;
            Build();
        }

        private void OnDestroy() { if (instance == this) instance = null; }

        /// <summary>Drive a generated loop instead of the scene's markers until <see cref="ClearLayout"/>.</summary>
        public void UseLayout(Vector3[] points, float width, float startLine)
        {
            if (layout == null) { authoredHalfWidth = halfWidth; authoredStartLine = startLineDistance; }
            layout = points;
            halfWidth = width;
            startLineDistance = startLine;
            Build();
        }

        public void ClearLayout()
        {
            Lures = null;
            RespawnGuard = null;
            if (layout == null) return;
            layout = null;
            halfWidth = authoredHalfWidth;
            startLineDistance = authoredStartLine;
            Build();
        }

        public bool HasLayout => layout != null;

        public void Build()
        {
            Vector3[] pts = layout;
            if (pts == null)
            {
                int n = transform.childCount;
                pts = new Vector3[n];
                for (int i = 0; i < n; i++) pts[i] = transform.GetChild(i).position;
            }
            var s = new TrackSpline(pts);
            spline = reversed ? s.Reversed() : s;
            profile = new TrackProfile(spline, profileSettings);
        }

        /// <summary>Target speed and racing line along the current direction.</summary>
        public TrackProfile Profile { get { if (profile == null) Build(); return profile; } }

        public float Project(Vector3 worldPos, ref int hint)
        {
            float main = Spline.Project(worldPos, ref hint, Fork != null ? 0 : 6);
            if (Fork != null)
            {
                // Like the main spline, project nearby off-road positions too: falling off the bridge
                // must not jump over a lap gate before recovery can return the car to the long route.
                float branch = Fork.Project(worldPos, out float error);
                if (error < (worldPos - Position(main)).sqrMagnitude) return branch;
            }
            return main;
        }

        public bool OnFork(Vector3 position)
        {
            if (Fork == null) return false;
            Fork.Project(position, out float error);
            return error < (TrackFork.HalfWidth + 1f) * (TrackFork.HalfWidth + 1f);
        }
        public Vector3 Position(float d) => Spline.Position(d);
        public Vector3 Tangent(float d) => Spline.Tangent(d);
        public Vector3 Right(float d) => Spline.Right(d);
        public float Wrap(float d) => Spline.Wrap(d);
        public float SignedDelta(float from, float to) => Spline.SignedDelta(from, to);
        public float CurvatureAhead(float d, float window) => Spline.CurvatureAhead(d, window);

        /// <summary>Pose on the centre line (plus lane offset) facing along the track.</summary>
        public void Pose(float d, float laneOffset, out Vector3 position, out Quaternion rotation)
        {
            Vector3 t = Tangent(d);
            position = Position(d) + Right(d) * laneOffset;
            rotation = Quaternion.LookRotation(t, Vector3.up);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (transform.childCount < 3) return;
            try { if (spline == null || !Application.isPlaying) Build(); } catch { return; }
            Gizmos.color = Color.cyan;
            float step = 5f;
            Vector3 prev = Position(0f);
            for (float d = step; d <= Length + step; d += step)
            {
                Vector3 p = Position(d);
                Gizmos.DrawLine(prev + Vector3.up * 0.2f, p + Vector3.up * 0.2f);
                prev = p;
            }
            Gizmos.color = Color.yellow;
            Pose(startLineDistance, 0f, out var sp, out var sr);
            Gizmos.DrawLine(sp - sr * Vector3.right * halfWidth + Vector3.up * 0.3f, sp + sr * Vector3.right * halfWidth + Vector3.up * 0.3f);
        }
#endif
    }
}
