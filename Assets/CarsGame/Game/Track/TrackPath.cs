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

        private TrackSpline spline;
        private static TrackPath instance;

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

        public void Build()
        {
            int n = transform.childCount;
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++) pts[i] = transform.GetChild(i).position;
            var s = new TrackSpline(pts);
            spline = reversed ? s.Reversed() : s;
        }

        public float Project(Vector3 worldPos, ref int hint) => Spline.Project(worldPos, ref hint);
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
