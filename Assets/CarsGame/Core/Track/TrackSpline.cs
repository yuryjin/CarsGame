using System;
using UnityEngine;

namespace RacingGame.Core.Track
{
    /// <summary>
    /// Closed polyline track model: ordered points, cumulative distance, projection of a world
    /// position onto the loop and tangent/right sampling. Pure data so it can be unit-tested and,
    /// later, evaluated on a server. Heights are kept from the points; projection is done in XZ.
    /// </summary>
    public sealed class TrackSpline
    {
        private readonly Vector3[] points;
        private readonly float[] cumulative;   // distance at points[i], cumulative[n] == Length
        private readonly Vector3[] tangents;   // unit tangent of segment i (points[i] -> points[i+1])
        private readonly float[] segmentLength;

        public float Length { get; }
        public int PointCount => points.Length;

        public TrackSpline(Vector3[] loopPoints)
        {
            if (loopPoints == null || loopPoints.Length < 3) throw new ArgumentException("A track needs at least three points.");
            points = (Vector3[])loopPoints.Clone();
            int n = points.Length;
            cumulative = new float[n + 1];
            tangents = new Vector3[n];
            segmentLength = new float[n];
            float d = 0f;
            for (int i = 0; i < n; i++)
            {
                cumulative[i] = d;
                Vector3 delta = points[(i + 1) % n] - points[i];
                delta.y = 0f;
                float len = delta.magnitude;
                if (len < 0.01f) throw new ArgumentException($"Duplicate track points at index {i}.");
                segmentLength[i] = len;
                tangents[i] = delta / len;
                d += len;
            }
            cumulative[n] = d;
            Length = d;
        }

        /// <summary>Wrap a distance into [0, Length).</summary>
        public float Wrap(float d)
        {
            d %= Length;
            return d < 0f ? d + Length : d;
        }

        /// <summary>Shortest signed distance from a to b along the loop, in (-Length/2, Length/2].</summary>
        public float SignedDelta(float from, float to)
        {
            float d = Wrap(to - from);
            return d > Length * 0.5f ? d - Length : d;
        }

        public Vector3 Point(int index) => points[((index % points.Length) + points.Length) % points.Length];

        private int SegmentAt(float d, out float t)
        {
            d = Wrap(d);
            // Binary search the segment containing d.
            int lo = 0, hi = points.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (cumulative[mid] <= d) lo = mid; else hi = mid - 1;
            }
            t = segmentLength[lo] > 0f ? (d - cumulative[lo]) / segmentLength[lo] : 0f;
            return lo;
        }

        public Vector3 Position(float d)
        {
            int i = SegmentAt(d, out float t);
            return Vector3.LerpUnclamped(points[i], Point(i + 1), t);
        }

        /// <summary>Blend zone (m) on each side of a point where the tangent turns towards the next segment.</summary>
        public const float TangentBlendDistance = 4f;

        /// <summary>Unit tangent (XZ plane), turned smoothly within <see cref="TangentBlendDistance"/> of a point so it does not jump there.</summary>
        public Vector3 Tangent(float d)
        {
            int i = SegmentAt(d, out float t);
            Vector3 a = tangents[i];
            float len = segmentLength[i];
            float blend = Mathf.Min(TangentBlendDistance, len * 0.5f);
            float s0 = t * len;          // distance past the segment start
            float s1 = len - s0;         // distance before the segment end
            Vector3 blended = a;
            if (s0 < blend)
            {
                Vector3 prev = tangents[(i - 1 + tangents.Length) % tangents.Length];
                blended = Vector3.Lerp(prev, a, 0.5f + 0.5f * (s0 / blend));
            }
            else if (s1 < blend)
            {
                Vector3 next = tangents[(i + 1) % tangents.Length];
                blended = Vector3.Lerp(a, next, 0.5f * (1f - s1 / blend));
            }
            blended.y = 0f;
            return blended.sqrMagnitude > 1e-6f ? blended.normalized : a;
        }

        public Vector3 Right(float d) => Vector3.Cross(Vector3.up, Tangent(d));

        /// <summary>
        /// Distance along the loop of the closest point to <paramref name="worldPos"/> (XZ).
        /// <paramref name="segmentHint"/> narrows the search to ±window segments around the
        /// previous result; pass -1 (or set window ≤ 0) for a full search.
        /// </summary>
        public float Project(Vector3 worldPos, ref int segmentHint, int window = 6)
        {
            int n = points.Length;
            int start = 0, count = n;
            if (segmentHint >= 0 && window > 0 && window * 2 + 1 < n)
            {
                start = segmentHint - window;
                count = window * 2 + 1;
            }

            float bestDist = float.MaxValue;
            float bestD = 0f;
            int bestSeg = 0;
            for (int k = 0; k < count; k++)
            {
                int i = ((start + k) % n + n) % n;
                Vector3 a = points[i];
                Vector3 ab = tangents[i] * segmentLength[i];
                Vector3 ap = worldPos - a; ap.y = 0f;
                float t = segmentLength[i] > 0f ? Mathf.Clamp01(Vector3.Dot(ap, tangents[i]) / segmentLength[i]) : 0f;
                Vector3 closest = a + ab * t;
                Vector3 diff = worldPos - closest; diff.y = 0f;
                float dist = diff.sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestD = cumulative[i] + t * segmentLength[i];
                    bestSeg = i;
                }
            }
            segmentHint = bestSeg;
            return Wrap(bestD);
        }

        /// <summary>Lateral offset (metres, +right) of a world position from the centre line at distance d.</summary>
        public float LateralOffset(Vector3 worldPos, float d)
        {
            Vector3 delta = worldPos - Position(d); delta.y = 0f;
            return Vector3.Dot(delta, Right(d));
        }

        /// <summary>
        /// Mean absolute heading change per metre (degrees/m) over [d, d+window]. Used by the AI
        /// to choose a corner speed before the corner arrives.
        /// </summary>
        public float CurvatureAhead(float d, float window)
        {
            if (window <= 0.5f) return 0f;
            const float step = 3f;
            float total = 0f;
            Vector3 prev = Tangent(d);
            for (float s = step; s <= window; s += step)
            {
                Vector3 next = Tangent(d + s);
                total += Vector3.Angle(prev, next);
                prev = next;
            }
            return total / window;
        }

        /// <summary>Same loop, opposite direction (for reverse events).</summary>
        public TrackSpline Reversed()
        {
            var rev = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) rev[i] = points[points.Length - 1 - i];
            return new TrackSpline(rev);
        }
    }
}
