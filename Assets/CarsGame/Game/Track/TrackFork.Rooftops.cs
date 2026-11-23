using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame
{
    public sealed partial class TrackFork
    {
        private const int RoofRampStart = 25, RoofLip = 30, RoofLanding = 32;
        public float LaunchFraction => RoofLip / (float)Steps;
        public float LandingFraction => RoofLanding / (float)Steps;

        private float AimForLanding(CarController car, float progress, float speed)
        {
            // Look through the jump before the tyres leave the ramp. Short lookahead
            // makes a last-moment correction continue sideways through the air.
            Vector3 target = Vector3.ProjectOnPlane(Position(progress + .12f) - car.transform.position, Vector3.up);
            Vector3 velocity = Vector3.ProjectOnPlane(car.Body.linearVelocity, Vector3.up);
            float curvature = 2f * Vector3.Dot(car.transform.right, target) / Mathf.Max(1f, target.sqrMagnitude);
            float slide = Vector3.SignedAngle(velocity, target, Vector3.up);
            return Mathf.Clamp(car.SteerForCurvature(curvature, speed) + slide / 55f - car.Body.angularVelocity.y * .4f, -1f, 1f);
        }

        private float RoofRespawn(float d)
        {
            float travel = path.SignedDelta(ApproachDistance, d) / MainSpan;
            float fraction = path.reversed ? 1f - travel : travel;
            if (fraction < (RoofRampStart - 1f) / Steps || fraction > (RoofLanding + 3f) / Steps)
                return d;
            float safe = path.reversed ? 1f - (RoofLanding + 4f) / Steps : (RoofRampStart - 1f) / Steps;
            return path.Wrap(ApproachDistance + MainSpan * safe);
        }

        public bool UnderRoofGap(Vector3 position)
        {
            if (Course != MountainFork.Rooftops)
                return false;
            // A normal touchdown can already be below the raised lip. Only recover below both roofs.
            if (position.y >= Mathf.Min(points[RoofLip].y, points[RoofLanding].y) - 1.25f)
                return false;
            Vector3 span = Vector3.ProjectOnPlane(points[RoofLanding + 2] - points[RoofLip - 1], Vector3.up);
            Vector3 offset = Vector3.ProjectOnPlane(position - points[RoofLip - 1], Vector3.up);
            float fraction = Vector3.Dot(offset, span) / span.sqrMagnitude;
            float limit = RoadHalfWidth + 1.3f;
            return fraction >= 0f && fraction <= 1f && (offset - span * fraction).sqrMagnitude <= limit * limit;
        }

        private void BuildRooftops(Material material)
        {
            var blocks = new MeshChunkWriter("RoofBuildings");
            var paint = new MeshChunkWriter("RoofLandingMarks");
            var brick = new Color32(116, 78, 75, 0);
            var concrete = new Color32(83, 94, 104, 0);
            var yellow = new Color32(255, 208, 62, 0);
            var glass = new Color32(124, 183, 207, 0);
            for (int i = 12; i < 52; i++)
            {
                if (i >= RoofLip && i < RoofLanding)
                    continue;
                Vector3 a = points[i], b = points[i + 1], ar = Right(i), br = Right(i + 1);
                var mid = (a + b) * .5f;
                var frame = Matrix4x4.TRS(mid, Quaternion.LookRotation(b - a), Vector3.one);
                float depth = Mathf.Max(.6f, mid.y + .4f);
                float length = Vector3.Distance(a, b) + .1f;
                LowPoly.Box(blocks, frame, new Vector3(0f, -depth * .5f - .07f, 0f), new Vector3(RoadHalfWidth * 2f + .25f, depth, length), i < RoofLip ? brick : concrete);
                if (i == RoofLanding)
                {
                    var front = Matrix4x4.TRS(a, Quaternion.LookRotation(Vector3.ProjectOnPlane(b - a, Vector3.up)), Vector3.one);
                    for (int bay = -2; bay <= 2; bay++)
                        LowPoly.Box(blocks, front, new Vector3(bay * 1.7f, -.95f, -.14f), new Vector3(1.1f, .6f, .17f), glass);
                }
                if (i % 3 == 0)
                    for (int side = -1; side <= 1; side += 2)
                        LowPoly.Box(blocks, frame, new Vector3(side * (RoadHalfWidth + .15f), -2.35f, 0f), new Vector3(.07f, .6f, .75f), glass);
                if (i >= RoofLanding && i < RoofLanding + 4 || i == RoofLip - 1)
                {
                    Vector3 up = Vector3.up * .065f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float left = side < 0 ? -1.85f : 1.2f, right = left + .65f;
                        paint.AddQuad(a + ar * left + up, b + br * left + up, b + br * right + up, a + ar * right + up, yellow);
                    }
                    if (i == RoofLanding)
                        paint.AddQuad(a - ar * RoadHalfWidth + up, Vector3.Lerp(a, b, .22f) - br * RoadHalfWidth + up, Vector3.Lerp(a, b, .22f) + br * RoadHalfWidth + up, a + ar * RoadHalfWidth + up, yellow);
                }
            }
            int marker = RoofLanding + 2;
            var facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(points[marker + 1] - points[marker], Vector3.up));
            for (int side = -1; side <= 1; side += 2)
            {
                var edge = points[marker] + Right(marker) * (side * (RoadHalfWidth + .15f));
                LowPoly.Box(paint, Matrix4x4.TRS(edge, facing, Vector3.one), Vector3.up * 1.6f, new Vector3(.16f, 3.2f, .16f), yellow);
            }
            LowPoly.Box(paint, Matrix4x4.TRS(points[marker], facing, Vector3.one), Vector3.up * 3.2f,
                new Vector3(2f * RoadHalfWidth + .45f, .18f, .18f), yellow);
            AddMeshes(blocks, material, false);
            AddMeshes(paint, material, false);
        }
    }
}
