using UnityEngine;
using RacingGame.World.Gen;

namespace RacingGame.EditorTools
{
    /// <summary>Static, vertex-coloured grandstand geometry, baked into the existing prop chunk.</summary>
    internal static class GrandstandGeometry
    {
        // Other props use zero vertex alpha. This small fill only lifts this structure's deep shadows.
        internal const float IndirectFill = .16f;
        private static Color32 C(float r, float g, float b) => MeshChunkWriter.Srgb(r, g, b, 1f);
        private static readonly Color32 Concrete = C(.64f, .66f, .65f);
        private static readonly Color32 Steel = C(.32f, .39f, .43f);
        private static readonly Color32 Rail = C(.69f, .74f, .73f);
        private static readonly Color32 Navy = C(.12f, .25f, .34f);
        private static readonly Color32 Cream = C(.86f, .85f, .74f);
        private static readonly Color32 Gold = C(.92f, .64f, .20f);
        private static readonly Color32[] Shirts =
        {
            C(.68f, .25f, .19f), C(.25f, .43f, .54f), C(.79f, .65f, .32f),
            C(.81f, .79f, .70f), C(.31f, .44f, .36f), C(.32f, .30f, .39f)
        };
        private static readonly Color32[] Skin =
        {
            C(.85f, .64f, .46f), C(.62f, .40f, .27f), C(.39f, .26f, .20f)
        };

        public static void Build(MeshChunkWriter w, GrandstandPlacement stand)
        {
            var m = Matrix4x4.TRS(stand.Position, Quaternion.LookRotation(stand.Forward), Vector3.one);
            float half = stand.Length * .5f;
            const int rows = 6, sectors = 4, seats = 7;
            const float rise = .85f, depth = 1.4f, aisle = 1.6f;
            float bay = (stand.Length - aisle * (sectors + 1)) / sectors;
            var random = new System.Random(99);
            void Box(Vector3 p, Vector3 size, Color32 color) => LowPoly.Box(w, m, p, size, color);
            void FrontPanel(Vector3 p, Vector2 size, Color32 color)
            {
                var right = Vector3.right * size.x * .5f;
                var up = Vector3.up * size.y * .5f;
                w.AddQuad(m.MultiplyPoint3x4(p + right - up), m.MultiplyPoint3x4(p + right + up),
                    m.MultiplyPoint3x4(p - right + up), m.MultiplyPoint3x4(p - right - up), color);
            }
            void Beam(Vector3 a, Vector3 b, float width, Color32 color)
            {
                var t = m * Matrix4x4.TRS((a + b) * .5f, Quaternion.FromToRotation(Vector3.up, b - a), Vector3.one);
                LowPoly.Box(w, t, Vector3.zero, new Vector3(width, (b - a).magnitude, width), color);
            }

            // Solid terraces, with three shallow stair treads per seating row in each aisle.
            for (int r = 0; r < rows; r++)
            {
                float floor = (r + 1) * rise, z = -r * depth;
                for (int s = 0; s < sectors; s++)
                {
                    float left = -half + aisle + s * (bay + aisle);
                    Box(new Vector3(left + bay * .5f, floor * .5f, z), new Vector3(bay, floor, depth), Concrete);
                    Box(new Vector3(left + bay * .5f, floor - .10f, z + .705f), new Vector3(bay, .13f, .035f), Cream);
                    for (int seat = 0; seat < seats; seat++)
                    {
                        float x = left + (seat + .5f) * bay / seats;
                        Color32 seatColor = s % 2 == 0 ? Navy : C(.43f, .53f, .56f);
                        Box(new Vector3(x, floor + .30f, z - .12f), new Vector3(.62f, .12f, .60f), seatColor);
                        Box(new Vector3(x, floor + .57f, z - .41f), new Vector3(.62f, .57f, .12f), seatColor);
                        if (random.NextDouble() > .78) continue;
                        var shirt = Shirts[random.Next(Shirts.Length)];
                        var skin = Skin[random.Next(Skin.Length)];
                        float h = .48f + (float)random.NextDouble() * .12f;
                        float sway = ((float)random.NextDouble() - .5f) * .12f;
                        // Seated silhouettes: knees, tapered shoulders and a distinct head.
                        Box(new Vector3(x, floor + .42f, z + .16f), new Vector3(.40f, .20f, .48f), Navy);
                        var body = m * Matrix4x4.TRS(new Vector3(x + sway, floor + .48f, z - .05f), Quaternion.Euler(0, 45, 0), Vector3.one);
                        LowPoly.Cylinder(w, body, Vector3.zero, .21f, .29f, h, 4, shirt);
                        var head = new Vector3(x + sway, floor + .48f + h, z - .03f);
                        LowPoly.Cylinder(w, m, head, .16f, .145f, .29f, 5, skin);
                        Box(head + Vector3.up * .28f, new Vector3(.29f, .10f, .27f), seat % 3 == 0 ? shirt : C(.20f, .17f, .15f));
                        if ((seat + s * seats + r * 3) % 11 == 0)
                            Beam(head + new Vector3(.23f, -.16f, 0), head + new Vector3(.42f, .34f, .05f), .12f, shirt);
                    }
                }
                for (int a = 0; a <= sectors; a++)
                {
                    float x = -half + aisle * .5f + a * (bay + aisle);
                    for (int step = 0; step < 3; step++)
                    {
                        float top = r * rise + (step + 1) * rise / 3f;
                        float sz = z + depth * .5f - (step + .5f) * depth / 3f;
                        Box(new Vector3(x, top * .5f, sz), new Vector3(aisle, top, depth / 3f), Concrete);
                        Box(new Vector3(x, top + .012f, sz + depth / 6f - .065f), new Vector3(aisle - .14f, .024f, .10f), Gold);
                    }
                }
            }

            // Railings follow the stairs and close the sides and rear landing.
            for (int a = 0; a <= sectors; a++)
            {
                float x = -half + aisle * .5f + a * (bay + aisle);
                for (int r = 0; r < rows; r++)
                {
                    float top = (r + 1) * rise;
                    Beam(new Vector3(x, top, -r * depth - .42f), new Vector3(x, top + .9f, -r * depth - .42f), .075f, Rail);
                }
                Beam(new Vector3(x, rise + .9f, -.42f), new Vector3(x, rows * rise + .9f, -(rows - 1) * depth - .42f), .085f, Rail);
            }
            float back = -(rows - 1) * depth - .7f;
            Box(new Vector3(0, 2.55f, back - .16f), new Vector3(stand.Length, 5.1f, .30f), Concrete);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * half;
                for (int r = 0; r < rows; r++)
                    Beam(new Vector3(x, (r + 1) * rise, -r * depth), new Vector3(x, (r + 1) * rise + 1f, -r * depth), .09f, Rail);
                Beam(new Vector3(x, rise + 1, 0), new Vector3(x, rows * rise + 1, -7), .09f, Rail);
            }
            Beam(new Vector3(-half, 6.1f, back), new Vector3(half, 6.1f, back), .10f, Rail);

            // A shallow cantilever canopy, pale soffit and visible triangular support frames.
            const float roofY = 8.05f, roofZ = -3.7f, pitch = -5f, roofDepth = 10.5f;
            var roofM = m * Matrix4x4.TRS(new Vector3(0, roofY, roofZ), Quaternion.Euler(pitch, 0, 0), Vector3.one);
            LowPoly.Box(w, roofM, Vector3.zero, new Vector3(stand.Length + 2, .18f, roofDepth), Navy);
            LowPoly.Box(w, roofM, new Vector3(0, -.105f, 0), new Vector3(stand.Length + 1.8f, .035f, roofDepth - .12f), Cream);
            LowPoly.Box(w, roofM, new Vector3(0, -.06f, roofDepth * .5f), new Vector3(stand.Length + 2, .38f, .16f), Navy);
            LowPoly.Box(w, roofM, new Vector3(0, -.06f, roofDepth * .5f + .09f), new Vector3(stand.Length + 2, .08f, .035f), Gold);
            for (int i = 0; i <= sectors; i++)
            {
                float x = -half + aisle * .5f + i * (bay + aisle);
                Beam(new Vector3(x, 5.1f, back), new Vector3(x, 6.1f, back), .09f, Rail);
                Beam(new Vector3(x, 0, back - .5f), new Vector3(x, 7.55f, back - .5f), .23f, Steel);
                Beam(new Vector3(x, 7.45f, back - .5f), new Vector3(x, 8.30f, 1.35f), .18f, Steel);
                Beam(new Vector3(x, 6.35f, back - .5f), new Vector3(x, 8.01f, -1.8f), .13f, Steel);
                Box(new Vector3(x, .18f, back - .5f), new Vector3(.62f, .36f, .62f), Concrete);
            }
            for (float z = -7.7f; z <= 1.4f; z += 3f)
            {
                float y = roofY + (z - roofZ) * Mathf.Tan(5f * Mathf.Deg2Rad) - .25f;
                Beam(new Vector3(-half - .8f, y, z), new Vector3(half + .8f, y, z), .12f, Steel);
            }

            // Track-facing panels leave every stair entrance open.
            for (int s = 0; s < sectors; s++)
            {
                float x = -half + aisle + s * (bay + aisle) + bay * .5f;
                Box(new Vector3(x, .63f, .83f), new Vector3(bay, 1.26f, .22f), Navy);
                Box(new Vector3(x, 1.28f, .83f), new Vector3(bay + .08f, .10f, .30f), Cream);
                FrontPanel(new Vector3(x, .38f, .975f), new Vector2(bay - .3f, .09f), Gold);
                for (int stripe = 0; stripe < 5; stripe++)
                    for (int row = 0; row < 2; row++)
                        if ((stripe + row) % 2 == 0)
                            FrontPanel(new Vector3(x + (stripe - 2) * .24f, .72f + row * .24f, .975f), new Vector2(.24f, .24f), Cream);
            }
        }
    }
}
