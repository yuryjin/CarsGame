using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Core.Track
{
    /// <summary>
    /// A separate wasteland circuit as pure data: straights and arcs (two straights are solved so the loop
    /// closes), sampled every <see cref="Step"/> metres with an elevation profile, plus the distances of
    /// its features (broken bridge, boost pad, tunnel, semi-sharp corner). Distances use the same XZ
    /// chord length as <see cref="TrackSpline"/>, so they index the spline built from <see cref="Points"/>.
    /// </summary>
    public sealed class WastelandLayout
    {
        public const float Step = 2f;
        public const float HalfWidth = 10f;
        public const float DeckHeight = 10f, LandingHeight = 8f;
        public const float TakeoffDeck = 42f, GapLength = 17f;
        /// <summary>Length of the kicker in front of the gap and its slope at the lip (tan 6°).</summary>
        public const float KickLength = 8f, KickSlope = 0.105f;
        public const float HairpinRadius = 36f;
        /// <summary>Tunnel: fillet rise and then the walls/ceiling reveal, each this long, at both ends.</summary>
        public const float TunnelFlare = 15f;
        /// <summary>Run-up a car needs from standstill to clear the gap (checked in the tests).</summary>
        public const float JumpRunUp = 115f;

        private enum Kind { Straight, Arc }

        private struct Segment
        {
            public Kind kind; public float length, radius, degrees; public string name;
        }

        private static Segment S(float length, string name = "") => new Segment { kind = Kind.Straight, length = length, name = name };
        private static Segment A(float radius, float degrees, string name = "") => new Segment { kind = Kind.Arc, radius = radius, degrees = degrees, name = name };

        // Clockwise seen from above (+ = right turn). "west" and "home" are solved so the loop closes.
        private static Segment[] Plan() => new[]
        {
            S(90f, "start"), A(90f, 90f, "sweep1"), S(65f, "approach"), S(105f, "bridge"), A(95f, 90f, "sweep2"),
            S(40f), A(75f, -35f, "esse1"), A(75f, 35f, "esse2"), S(150f, "tunnel"), A(90f, 90f, "sweep3"),
            S(0f, "west"), A(HairpinRadius, 90f, "hairpin"), S(70f), A(160f, 20f, "kink1"), A(160f, -20f, "kink2"), S(0f, "home"),
        };

        public Vector3[] Points { get; }
        public float Length { get; }
        private readonly Dictionary<string, Vector2> ranges = new Dictionary<string, Vector2>();

        /// <summary>[from, to) distance of a named segment.</summary>
        public Vector2 Range(string name) => ranges[name];

        public float StartLine => 60f;
        public float GapStart => Range("bridge").x + TakeoffDeck;
        public float GapEnd => GapStart + GapLength;
        public float BoostPad => Range("approach").x + 30f;
        public float TunnelStart => Range("tunnel").x;
        public float TunnelEnd => Range("tunnel").y;
        public Vector2 Hairpin => Range("hairpin");

        public WastelandLayout()
        {
            var plan = Plan();
            int west = Array.FindIndex(plan, s => s.name == "west"), home = Array.FindIndex(plan, s => s.name == "home");
            // The end point is linear in the two free straights: solve end(west, home) = origin.
            Vector2 e0 = EndPoint(plan), ex, ey;
            plan[west].length = 1f; ex = EndPoint(plan) - e0; plan[west].length = 0f;
            plan[home].length = 1f; ey = EndPoint(plan) - e0; plan[home].length = 0f;
            float det = ex.x * ey.y - ex.y * ey.x;
            plan[west].length = (-e0.x * ey.y + e0.y * ey.x) / det;
            plan[home].length = (-ex.x * e0.y + ex.y * e0.x) / det;
            if (plan[west].length < 20f || plan[home].length < 20f) throw new InvalidOperationException("Star Ring does not close with positive straights.");

            var flat = new List<Vector2>();
            var distances = new List<float>();
            Walk(plan, flat, distances);
            Length = distances[distances.Count - 1] + Vector2.Distance(flat[flat.Count - 1], flat[0]);
            Points = new Vector3[flat.Count];
            for (int i = 0; i < flat.Count; i++) Points[i] = new Vector3(flat[i].x, Height(distances[i]), flat[i].y);
        }

        private static Vector2 Heading(float radians) => new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));

        private static Vector2 EndPoint(Segment[] plan)
        {
            var points = new List<Vector2>();
            return Walk(plan, points, null);
        }

        // Emits the start point of every chord; returns the point after the last one.
        private static Vector2 Walk(Segment[] plan, List<Vector2> points, List<float> distances, Dictionary<string, Vector2> named = null)
        {
            Vector2 p = Vector2.zero;
            float heading = 0f, d = 0f;
            foreach (var s in plan)
            {
                float start = d;
                if (s.kind == Kind.Straight)
                {
                    int n = Mathf.Max(1, Mathf.RoundToInt(s.length / Step));
                    for (int i = 0; i < n; i++)
                    {
                        points.Add(p); distances?.Add(d);
                        p += Heading(heading) * (s.length / n); d += s.length / n;
                    }
                }
                else
                {
                    float total = s.degrees * Mathf.Deg2Rad;
                    int n = Mathf.Max(1, Mathf.RoundToInt(s.radius * Mathf.Abs(total) / Step));
                    float turn = total / n, chord = 2f * s.radius * Mathf.Sin(Mathf.Abs(turn) * 0.5f);
                    for (int i = 0; i < n; i++)
                    {
                        points.Add(p); distances?.Add(d);
                        p += Heading(heading + turn * 0.5f) * chord; heading += turn; d += chord;
                    }
                }
                if (named != null && !string.IsNullOrEmpty(s.name)) named[s.name] = new Vector2(start, d);
            }
            return p;
        }

        private void Walk(Segment[] plan, List<Vector2> points, List<float> distances) => Walk(plan, points, distances, ranges);

        /// <summary>Road height at a distance: climb to the bridge, kicker, lower landing deck, descent, then flat.</summary>
        public float Height(float d)
        {
            Vector2 climb = Range("sweep1"), bridge = Range("bridge"), descent = Range("sweep2");
            if (d < climb.x) return 0f;
            if (d < climb.y) return DeckHeight * Mathf.SmoothStep(0f, 1f, (d - climb.x) / (climb.y - climb.x));
            if (d < GapStart)
            {
                float s = d - (GapStart - KickLength);
                return s > 0f ? DeckHeight + KickSlope / (2f * KickLength) * s * s : DeckHeight;
            }
            if (d < GapEnd) return Mathf.Lerp(LipHeight, LandingHeight, (d - GapStart) / GapLength);
            if (d < bridge.y) return LandingHeight;
            if (d < descent.y) return LandingHeight * (1f - Mathf.SmoothStep(0f, 1f, (d - descent.x) / (descent.y - descent.x)));
            return 0f;
        }

        public static float LipHeight => DeckHeight + KickSlope * KickLength * 0.5f;

        public bool InGap(float d) => d > GapStart && d < GapEnd;

        /// <summary>Tunnel shape at a distance: fillet rise (0..1) and wall/ceiling reveal (0..1).</summary>
        public void TunnelShape(float d, out float fillet, out float reveal)
        {
            float into = Mathf.Min(d - TunnelStart, TunnelEnd - d);
            fillet = Mathf.Clamp01(into / TunnelFlare);
            reveal = Mathf.Clamp01((into - TunnelFlare) / TunnelFlare);
        }

        public bool InTunnel(float d) => d >= TunnelStart && d <= TunnelEnd;

        /// <summary>
        /// Where a car that left the road near d may reappear: never over the gap, and with enough run-up to
        /// clear it again; everywhere else unchanged.
        /// </summary>
        public float SafeRespawn(float d)
        {
            float runUp = GapStart - JumpRunUp;
            return d > runUp && d < GapEnd + 3f ? runUp : d;
        }
    }
}
