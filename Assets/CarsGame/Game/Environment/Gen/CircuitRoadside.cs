using RacingGame.Core.Track;
using UnityEngine;

namespace RacingGame.World.Gen
{
    internal static class CircuitRoadside
    {
        // Distances on the authored forward mountain circuit, including in reverse races.
        internal const float TowerDistance = 470f, PitsDistance = 710f, SignAdvance = 50f;
        private static readonly Color32 Steel = new Color32(58, 70, 76, 0);
        private static readonly Color32 Cream = new Color32(234, 222, 187, 0);
        private static readonly Color32 Rust = new Color32(164, 71, 44, 0);
        private static readonly Color32 Rim = new Color32(187, 185, 165, 0);
        private static readonly Color32 Amber = new Color32(255, 185, 51, 45);

        private static Matrix4x4 Frame(TrackSpline circuit, float d, float offset = 0f, int direction = 1)
        {
            return Matrix4x4.TRS(circuit.Position(d) + circuit.Right(d) * offset,
                Quaternion.LookRotation(circuit.Tangent(d) * direction), Vector3.one);
        }

        private static void Beam(MeshChunkWriter w, Matrix4x4 frame, Vector3 a, Vector3 b, float width, Color32 color)
        {
            var beam = frame * Matrix4x4.TRS((a + b) * 0.5f, Quaternion.FromToRotation(Vector3.up, b - a), Vector3.one);
            LowPoly.Box(w, beam, Vector3.zero, new Vector3(width, (b - a).magnitude, width), color);
        }

        internal static MeshChunkWriter ForkSigns(TrackSpline circuit, float halfWidth, bool hasRepair)
        {
            var signs = new MeshChunkWriter("ForkSigns");
            Sign(signs, Frame(circuit, TrackFork.Entry - SignAdvance), halfWidth, 1, hasRepair);
            Sign(signs, Frame(circuit, TrackFork.Exit + SignAdvance, 0f, -1), halfWidth, -1, hasRepair);
            return signs;
        }

        private static void Sign(MeshChunkWriter w, Matrix4x4 frame, float halfWidth, int bridgeSide, bool hasRepair)
        {
            float post = halfWidth + 2.3f;
            void Box(Vector3 p, Vector3 size, Color32 color) => LowPoly.Box(w, frame, p, size, color);
            Box(new Vector3(post, 0.22f, 0f), new Vector3(.85f, .6f, .85f), Rim);
            Box(new Vector3(post, 2.9f, 0f), new Vector3(.26f, 5.8f, .26f), Steel);
            Box(new Vector3((post + 2f) * .5f, 5f, 0f), new Vector3(post - 1.8f, .24f, .24f), Steel);
            Beam(w, frame, new Vector3(post, 3.7f, 0f), new Vector3(post - 1.9f, 5f, 0f), .16f, Steel);
            const float cx = 2f, cy = 5f;
            Box(new Vector3(cx, cy, -.2f), new Vector3(5.3f, 2.4f, .22f), Cream);
            Box(new Vector3(cx, cy, -.34f), new Vector3(5.12f, 2.22f, .08f), new Color32(29, 49, 55, 0));
            Vector3 Face(Vector2 v) => frame.MultiplyPoint3x4(new Vector3(cx + v.x, cy + v.y, -.389f));
            void Stroke(float x1, float y1, float x2, float y2, float width, Color32 color)
            {
                var a = new Vector2(x1, y1); var b = new Vector2(x2, y2);
                var n = Vector2.Perpendicular((b - a).normalized) * (width * .5f);
                w.AddQuad(Face(a+n), Face(b+n), Face(b-n), Face(a-n), color);
            }
            var light = new Color32(230, 244, 215, 50);
            float bridge = bridgeSide * 1.27f, road = -bridge;
            // Each route gets a direction arrow above a pictogram that also reads without colour.
            void Arrow(float x, float side, Color32 color)
            {
                Stroke(x - side*.29f, .18f, x + side*.28f, .78f, .15f, color);
                Stroke(x - side*.12f, .78f, x + side*.29f, .78f, .15f, color);
                Stroke(x + side*.29f, .79f, x + side*.29f, .38f, .15f, color);
            }
            Arrow(bridge, bridgeSide, Amber); Arrow(road, -bridgeSide, light);
            Stroke(bridge-.7f,-.52f,bridge+.7f,-.52f,.14f,Amber);
            foreach (int side in new[] {-1, 1})
            {
                float pier = bridge + side*.48f;
                Stroke(pier,-.82f,pier,-.12f,.13f,Amber);
                Stroke(pier,-.16f,bridge,-.47f,.1f,Amber);
            }
            if (hasRepair)
            {
                Stroke(road-.40f,-.79f,road+.13f,-.30f,.19f,light);
                Stroke(road+.10f,-.33f,road+.37f,-.36f,.15f,light);
                Stroke(road+.35f,-.36f,road+.50f,-.16f,.15f,light);
                Stroke(road+.10f,-.34f,road+.03f,-.10f,.15f,light);
                Stroke(road+.03f,-.10f,road+.16f,.02f,.15f,light);
            }
            else
            {
                Stroke(road-.32f,-.77f,road-.2f,-.07f,.12f,light);
                Stroke(road+.32f,-.77f,road+.2f,-.07f,.12f,light);
            }
        }

        internal static MeshChunkWriter WaterTower(TrackSpline circuit)
        {
            var w = new MeshChunkWriter("CircuitWaterTower");
            var frame = Frame(circuit, TowerDistance, -19f);
            void Ring(float y, float r0, float r1, float height, Color32 color) =>
                LowPoly.Cylinder(w, frame, Vector3.up * y, r0, r1, height, 16, color);
            Vector3 Leg(int i, float r, float y)
            {
                float a = i * Mathf.PI / 3f;
                return new Vector3(r*Mathf.Cos(a), y, r*Mathf.Sin(a));
            }
            for (int i = 0; i < 6; i++)
            {
                var foot = Leg(i, 3.4f, 0f);
                LowPoly.Box(w, frame, foot, new Vector3(.94f,.65f,.94f), Rim);
                Beam(w, frame, foot, Leg(i,2.45f,9f), .30f, Steel);
                Beam(w, frame, Leg(i,3.25f,1.7f), Leg(i+1,2.65f,7.7f), .14f, Steel);
                Beam(w, frame, Leg(i+1,3.25f,1.7f), Leg(i,2.65f,7.7f), .14f, Steel);
            }
            Ring(0f,.3f,.3f,8.3f,Steel);
            Ring(8.1f,2.3f,4.4f,2.1f,Rust);
            Ring(10.2f,4.4f,4.4f,2.3f,Rust);
            Ring(12.5f,4.4f,4.4f,1.15f,Cream);
            Ring(13.65f,4.4f,4.4f,1.15f,Rust);
            Ring(14.8f,4.65f,.42f,1.65f,Steel);
            Ring(16.45f,.4f,.35f,.52f,Rim);
            return w;
        }

        internal static MeshChunkWriter PitWalkway(TrackSpline circuit)
        {
            var w = new MeshChunkWriter("CircuitPitWalkway");
            var frame = Frame(circuit, PitsDistance);
            void Box(float x,float y,float z,float sx,float sy,float sz,Color32 c) =>
                LowPoly.Box(w,frame,new Vector3(x,y,z),new Vector3(sx,sy,sz),c);
            var glass = new Color32(79,134,143,12);
            foreach (int side in new[] {-1, 1})
            {
                float x = side*13f;
                Box(x,2.7f,0f,6.6f,5.6f,14.5f,Cream);
                Box(x,5.63f,0f,7.1f,.45f,15f,Rust);
                Box(x,6.65f,0f,4.2f,2.2f,4.2f,Steel);
                for (int bay = -1; bay <= 1; bay++)
                {
                    Box(x-side*3.33f,1.75f,bay*4.5f,.14f,3.5f,3.8f,Steel);
                    Box(x-side*3.42f,2.65f,bay*4.5f,.08f,.56f,3.34f,glass);
                }
            }
            Box(0f,6f,0f,26f,.48f,3.3f,Steel);
            Box(0f,7.2f,0f,26f,2f,2.85f,glass);
            Box(0f,8.35f,0f,26.6f,.34f,3.45f,Rust);
            for (int x = -12; x <= 12; x += 3)
                Box(x,7.18f,0f,.18f,2.22f,3f,Cream);
            Box(0f,6.48f,0f,26f,.16f,3.04f,Cream);
            return w;
        }
    }
}
