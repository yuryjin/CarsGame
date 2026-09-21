using UnityEngine;
using RacingGame.Core.Track;

namespace RacingGame.World.Gen
{
    [System.Serializable]
    public sealed class ChasmSettings
    {
        [Tooltip("Solid ground on each side of the centre line (barriers ~5.8 m, tyre stacks 8.2 m, lamps 8.6 m).")]
        public float shoulder = 10f;
        [Tooltip("Width of the chasm that runs along both sides of the whole track, beyond the shoulder.")]
        public float bandWidth = 14f;
        [Tooltip("Depth of the cliffs and the abyss floor below the ground.")]
        public float depth = 70f;

        [Header("Bridge")]
        [Tooltip("Distance along the loop of the bridge centre (the straight back section).")]
        public float bridgeCentre = 848f;
        public float bridgeHalfSpan = 13f;
        public float bridgeHalfWidth = 7.2f;
        [Tooltip("How far the canyon under the bridge runs to the right (infield) and left of the centre line.")]
        public float gorgeRight = 70f;
        public float gorgeLeft = 22f;

        [Header("Grandstand pad")]
        [Tooltip("The stand keeps a ledge of ground (lateral range to the right of the start straight).")]
        public float padNear = 14f;
        public float padFar = 30f;
        public float padHalfLength = 26f;
        public float padOffsetAlong = 6f;       // same as PropLayout's stand (start line + 6 m)
    }

    /// <summary>
    /// Where the ground is before the collapse. Fields are signed "solidity" in metres: &gt;= 0 is
    /// ground, &lt; 0 is abyss, and their zero line is where a cliff goes. Pure: spline + settings.
    /// </summary>
    public sealed class ChasmLayout
    {
        public readonly TrackSpline Spline;
        public readonly ChasmSettings Settings;
        private readonly Vector3 bridgeOrigin, bridgeAlong, bridgeRight;
        private readonly Vector3 padOrigin, padAlong, padRight;

        public ChasmLayout(TrackSpline spline, ChasmSettings settings, float startLineDistance)
        {
            Spline = spline;
            Settings = settings;
            bridgeOrigin = spline.Position(settings.bridgeCentre); bridgeOrigin.y = 0f;
            bridgeAlong = spline.Tangent(settings.bridgeCentre);
            bridgeRight = spline.Right(settings.bridgeCentre);
            float padD = startLineDistance + settings.padOffsetAlong;
            padOrigin = spline.Position(padD); padOrigin.y = 0f;
            padAlong = spline.Tangent(padD);
            padRight = spline.Right(padD);
        }

        public float DistanceToTrack(float x, float z) => TerrainMeshGen.DistanceToTrack(Spline, new Vector3(x, 0f, z));

        /// <summary>Bridge frame: u along the track, v to the right, from the bridge centre.</summary>
        public Vector2 BridgeLocal(float x, float z)
        {
            var rel = new Vector3(x - bridgeOrigin.x, 0f, z - bridgeOrigin.z);
            return new Vector2(Vector3.Dot(rel, bridgeAlong), Vector3.Dot(rel, bridgeRight));
        }

        public Matrix4x4 BridgeMatrix => Matrix4x4.TRS(bridgeOrigin, Quaternion.LookRotation(bridgeAlong, Vector3.up), Vector3.one);

        /// <summary>&gt; 0 outside the canyon under the bridge.</summary>
        public float GorgeOutside(float x, float z)
        {
            var p = BridgeLocal(x, z);
            var s = Settings;
            return Mathf.Max(Mathf.Abs(p.x) - s.bridgeHalfSpan, Mathf.Max(-s.gorgeLeft - p.y, p.y - s.gorgeRight));
        }

        /// <summary>&gt; 0 on the grandstand ledge.</summary>
        public float PadInside(float x, float z)
        {
            var rel = new Vector3(x - padOrigin.x, 0f, z - padOrigin.z);
            float u = Vector3.Dot(rel, padAlong), v = Vector3.Dot(rel, padRight);
            var s = Settings;
            return Mathf.Min(s.padHalfLength - Mathf.Abs(u), Mathf.Min(v - s.padNear, s.padFar - v));
        }

        /// <summary>The strip the track runs on (it survives the collapse). Cut by the canyon; the bridge deck spans that gap.</summary>
        public float Causeway(float x, float z, float dist) => Mathf.Min(Settings.shoulder - dist, GorgeOutside(x, z));

        /// <summary>The land beyond the side chasm, before the collapse.</summary>
        public float Terrain(float x, float z, float dist)
        {
            float land = Mathf.Min(dist - (Settings.shoulder + Settings.bandWidth), GorgeOutside(x, z));
            return Mathf.Max(land, PadInside(x, z));
        }

        public bool OnBridgeDeck(float x, float z)
        {
            var p = BridgeLocal(x, z);
            return Mathf.Abs(p.x) <= Settings.bridgeHalfSpan && Mathf.Abs(p.y) <= Settings.bridgeHalfWidth;
        }

        /// <summary>Is there ground at (x,z) before the collapse (props may stand here)?</summary>
        public bool IsSolid(float x, float z)
        {
            float dist = DistanceToTrack(x, z);
            return Causeway(x, z, dist) >= 0f || Terrain(x, z, dist) >= 0f || OnBridgeDeck(x, z);
        }
    }
}
