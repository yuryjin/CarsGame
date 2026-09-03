using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Track;

namespace RacingGame.World.Gen
{
    public struct TreePlacement { public Vector3 Position; public float Scale; public float Yaw; public int Shade; public float DistToTrack; }
    public struct TireStackPlacement { public Vector3 Position; public int BandColor; }
    public struct LampPlacement { public Vector3 Position; public Vector3 TowardsTrack; public Vector3 Along; public float LateralToRoad; }
    public struct GrandstandPlacement { public Vector3 Position; public Vector3 Forward; public float Length; }

    /// <summary>Where the props go. Deterministic from the spline + seed. No Unity objects touched.</summary>
    public sealed class PropLayout
    {
        public readonly List<TreePlacement> Trees = new List<TreePlacement>();
        public readonly List<TireStackPlacement> TireStacks = new List<TireStackPlacement>();
        public readonly List<LampPlacement> Lamps = new List<LampPlacement>();
        public GrandstandPlacement Grandstand;

        public const float ColliderTreeDistance = 25f;
        public const float ClearCorridor = 12f;      // nothing generated closer than this to the centre line (barriers sit at ~6 m)

        public static PropLayout Build(TrackSpline spline, IList<Vector3> authoredTrees, float startLineDistance, int seed, float groveRingMin = 55f, float groveRingMax = 240f, int newTreeCap = 90)
        {
            var layout = new PropLayout();
            var noise = new PropNoise(seed);
            var rnd = new System.Random(seed);
            float Next() => (float)rnd.NextDouble();

            // 1. Authored trees (scene positions) keep their spots, with regenerated scale/tint.
            var placed = new List<Vector3>();
            foreach (var p in authoredTrees)
            {
                float d = TerrainMeshGen.DistanceToTrack(spline, p);
                if (d < ClearCorridor) continue;
                layout.Trees.Add(new TreePlacement { Position = new Vector3(p.x, 0f, p.z), Scale = 0.85f + Next() * 0.4f, Yaw = Next() * 360f, Shade = rnd.Next(0, 4), DistToTrack = d });
                placed.Add(p);
            }

            // 2. Groves further out: rejection-sampled, clustered by noise, min spacing.
            int attempts = 0, added = 0;
            var centre = new Vector2(5f, 0f);
            while (added < newTreeCap && attempts++ < 4000)
            {
                float ang = Next() * Mathf.PI * 2f, rad = 40f + Next() * 320f;
                var p = new Vector3(centre.x + Mathf.Cos(ang) * rad, 0f, centre.y + Mathf.Sin(ang) * rad);
                float d = TerrainMeshGen.DistanceToTrack(spline, p);
                if (d < groveRingMin || d > groveRingMax) continue;
                if (noise.Value(p.x * 0.012f, p.z * 0.012f, 21) < 0.56f) continue;      // clusters
                bool tooClose = false;
                foreach (var q in placed) if ((q - p).sqrMagnitude < 8f * 8f) { tooClose = true; break; }
                if (tooClose) continue;
                placed.Add(p);
                layout.Trees.Add(new TreePlacement { Position = p, Scale = 0.9f + Next() * 0.6f, Yaw = Next() * 360f, Shade = rnd.Next(0, 4), DistToTrack = d });
                added++;
            }

            // 3. Tire stacks on the outside of the sharpest corners.
            var corners = SharpestCorners(spline, 6, 60f);
            foreach (var (d, turnSign) in corners)
            {
                Vector3 right = spline.Right(d), t = spline.Tangent(d);
                float side = turnSign > 0f ? -1f : 1f;      // turning right (sign>0) => outside is the left
                for (int k = -1; k <= 2; k++)
                {
                    Vector3 pos = spline.Position(d + k * 2.4f) + spline.Right(d + k * 2.4f) * (side * 8.2f);
                    pos.y = 0f;
                    layout.TireStacks.Add(new TireStackPlacement { Position = pos, BandColor = (k + 1 + corners.Count) % 3 });
                }
            }

            // 4. Lamp posts every ~47 m on the outer side (alternating sides where the loop is convex is overkill: right side).
            int lampCount = Mathf.Max(8, Mathf.RoundToInt(spline.Length / 47f));
            for (int i = 0; i < lampCount; i++)
            {
                float d = i * spline.Length / lampCount + 12f;
                Vector3 r = spline.Right(d), tan = spline.Tangent(d);
                Vector3 pos = spline.Position(d) + r * 8.6f; pos.y = 0f;
                layout.Lamps.Add(new LampPlacement { Position = pos, TowardsTrack = -r, Along = tan, LateralToRoad = 8.6f });
            }

            // 5. Grandstand along the start straight, left side.
            {
                float d = startLineDistance + 6f;
                Vector3 pos = spline.Position(d) - spline.Right(d) * 15f; pos.y = 0f;
                layout.Grandstand = new GrandstandPlacement { Position = pos, Forward = spline.Right(d), Length = 44f };
            }
            return layout;
        }

        /// <summary>Distances of the sharpest corners (local curvature maxima) with a minimum spacing; sign = turn direction.</summary>
        public static List<(float d, float sign)> SharpestCorners(TrackSpline spline, int count, float minSpacing)
        {
            var samples = new List<(float d, float curv, float sign)>();
            for (float d = 0f; d < spline.Length; d += 4f)
            {
                float c = spline.CurvatureAhead(d, 16f);
                Vector3 t0 = spline.Tangent(d), t1 = spline.Tangent(d + 12f);
                float sign = Mathf.Sign(Vector3.Cross(t0, t1).y);      // +: turning right (Unity is left-handed, y up)
                samples.Add((d + 8f, c, sign));
            }
            samples.Sort((a, b) => b.curv.CompareTo(a.curv));
            var chosen = new List<(float d, float sign)>();
            foreach (var s in samples)
            {
                if (chosen.Count >= count) break;
                bool near = false;
                foreach (var c in chosen) if (Mathf.Abs(spline.SignedDelta(c.d, s.d)) < minSpacing) { near = true; break; }
                if (!near) chosen.Add((spline.Wrap(s.d), s.sign));
            }
            return chosen;
        }
    }
}
