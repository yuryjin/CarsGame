using UnityEngine;
using RacingGame.Core.Track;
using RacingGame.World.Gen;

namespace RacingGame
{
    public sealed partial class TrackFork
    {
        private static TrackFork iceStation;
        private Collider icyRoad;
        private TrackSpline icySpline;
        private Bounds icyBounds;
        private const float IceStart = Entry + 28f, IceEnd = Exit - 26f;
        public const float IceGrip = .38f;

        public static bool IceAt(Collider road, Vector3 point)
        {
            if (iceStation == null || road != iceStation.icyRoad || !iceStation.icyBounds.Contains(point))
                return false;
            int hint = -1;
            float along = iceStation.icySpline.Project(point, ref hint, 0);
            return along >= IceStart && along <= IceEnd;
        }

        private void BuildIceStation(Material material, TrackSpline course, Collider road)
        {
            iceStation = this;
            icyRoad = road;
            icySpline = course;
            icyBounds = new Bounds(course.Position(IceStart), Vector3.zero);
            var ice = new MeshChunkWriter("StationIce");
            var shelter = new MeshChunkWriter("StationTunnel");
            var pale = new Color32(193, 235, 244, 0);
            var blue = new Color32(120, 200, 224, 0);
            var steel = new Color32(49, 86, 102, 0);
            for (float d = IceStart; d < IceEnd; d += 2f)
            {
                float next = Mathf.Min(d + 2f, IceEnd);
                Vector3 a = course.Position(d), b = course.Position(next);
                Vector3 ar = course.Right(d) * path.halfWidth, br = course.Right(next) * path.halfWidth;
                Vector3 lift = Vector3.up * .035f;
                icyBounds.Encapsulate(a - ar);
                icyBounds.Encapsulate(a + ar);
                icyBounds.Encapsulate(b - br);
                icyBounds.Encapsulate(b + br);
                ice.AddQuad(a - ar + lift, b - br + lift, b + br + lift, a + ar + lift, blue);
                if ((((int)(d - IceStart)) % 12) == 0)
                    ice.AddTriangle(a - ar * .85f + lift * 1.2f, b - br * .12f + lift * 1.2f,
                        b - br * .44f + lift * 1.2f, pale);
            }
            icyBounds.Expand(new Vector3(3f, 3f, 3f));
            for (int i = 12; i < 52; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                Matrix4x4 frame = Matrix4x4.TRS((a + b) * .5f, Quaternion.LookRotation(b - a, Vector3.up), Vector3.one);
                float length = Vector3.Distance(a, b) + .18f;
                LowPoly.Box(shelter, frame, Vector3.up * 4.45f, new Vector3(7.7f, .55f, length), pale);
                if ((i % 4) == 0)
                    for (int side = -1; side <= 1; side += 2)
                        LowPoly.Box(shelter, frame, new Vector3(side * 3.65f, 2.1f, 0f), new Vector3(.23f, 4.3f, .27f), steel);
            }
            AddMeshes(ice, material, false);
            AddMeshes(shelter, material, false);
        }
    }
}
