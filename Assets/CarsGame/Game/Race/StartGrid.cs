using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Staggered two-column grid behind the start line, laid out along the track spline so it
    /// works for any layout or direction. Slot 0 is pole.
    /// </summary>
    public sealed class StartGrid : MonoBehaviour
    {
        [Tooltip("Distance (m) from the start line back to the pole slot.")]
        public float poleSetback = 7f;
        [Tooltip("Distance (m) between rows.")]
        public float rowSpacing = 7f;
        [Tooltip("Lateral offset (m) of each column from the centre line.")]
        public float columnOffset = 2.3f;
        [Tooltip("Height above the centre line to drop the car from.")]
        public float dropHeight = 0.5f;

        public void GetSlot(int index, TrackPath path, out Vector3 position, out Quaternion rotation, out float laneOffset)
        {
            int row = index / 2;
            laneOffset = (index % 2 == 0) ? -columnOffset : columnOffset;
            float d = path.Wrap(path.startLineDistance - poleSetback - rowSpacing * row);
            path.Pose(d, laneOffset, out position, out rotation);
            position += Vector3.up * dropHeight;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var path = FindAnyObjectByType<TrackPath>();
            if (path == null || path.transform.childCount < 3) return;
            try { path.Build(); } catch { return; }
            Gizmos.color = Color.green;
            for (int i = 0; i < 6; i++)
            {
                GetSlot(i, path, out var p, out var r, out _);
                Gizmos.matrix = Matrix4x4.TRS(p, r, Vector3.one);
                Gizmos.DrawWireCube(new Vector3(0f, 0.45f, 0.05f), new Vector3(1.85f, 0.7f, 4.2f));
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
#endif
    }
}
