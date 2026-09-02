using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    public static class ProceduralCarBuilder
    {
        public static GameObject BuildSleekSportsCar(
            Material bodyPaintMat,
            Material carbonDarkMat,
            Material glassMat,
            Material tireMat,
            Material rimMat,
            Material brakeMat,
            Material headlightMat,
            Material taillightMat,
            Material interiorMat)
        {
            GameObject carRoot = new GameObject("Player_Car");
            carRoot.tag = "Player";

            // Main Visual Root
            GameObject visualRoot = new GameObject("Car_Visuals");
            visualRoot.transform.parent = carRoot.transform;
            visualRoot.transform.localPosition = Vector3.zero;
            visualRoot.transform.localRotation = Quaternion.identity;

            // 1. Sleek Aerodynamic Body Mesh (Sculpted sports car with tapered nose, wheel arches, waistline, and rear deck)
            GameObject bodyObj = new GameObject("Car_Body");
            bodyObj.transform.parent = visualRoot.transform;
            bodyObj.transform.localPosition = Vector3.zero;
            bodyObj.transform.localRotation = Quaternion.identity;

            MeshFilter bodyMf = bodyObj.AddComponent<MeshFilter>();
            MeshRenderer bodyMr = bodyObj.AddComponent<MeshRenderer>();
            bodyMf.sharedMesh = GenerateCarBodyMesh();
            bodyMr.sharedMaterial = bodyPaintMat;

            // 2. Cockpit Canopy / Glass (Smooth aerodynamic bubble, no z-fighting, sitting cleanly on top)
            GameObject canopyObj = new GameObject("Car_Canopy");
            canopyObj.transform.parent = visualRoot.transform;
            canopyObj.transform.localPosition = Vector3.zero;
            canopyObj.transform.localRotation = Quaternion.identity;

            MeshFilter canopyMf = canopyObj.AddComponent<MeshFilter>();
            MeshRenderer canopyMr = canopyObj.AddComponent<MeshRenderer>();
            canopyMf.sharedMesh = GenerateCanopyMesh();
            canopyMr.sharedMaterial = glassMat;

            // 3. Carbon Aero Kit (Front Splitter, Side Skirts, Rear Diffuser, Aerodynamic Wing)
            GameObject aeroObj = new GameObject("Car_AeroKit");
            aeroObj.transform.parent = visualRoot.transform;
            aeroObj.transform.localPosition = Vector3.zero;
            aeroObj.transform.localRotation = Quaternion.identity;

            MeshFilter aeroMf = aeroObj.AddComponent<MeshFilter>();
            MeshRenderer aeroMr = aeroObj.AddComponent<MeshRenderer>();
            aeroMf.sharedMesh = GenerateAeroKitMesh();
            aeroMr.sharedMaterial = carbonDarkMat;

            // 4. Headlights and Taillights (Recessed cleanly into the body)
            GameObject lightsObj = new GameObject("Car_Lights");
            lightsObj.transform.parent = visualRoot.transform;
            lightsObj.transform.localPosition = Vector3.zero;
            lightsObj.transform.localRotation = Quaternion.identity;

            MeshFilter lightsMf = lightsObj.AddComponent<MeshFilter>();
            MeshRenderer lightsMr = lightsObj.AddComponent<MeshRenderer>();
            lightsMf.sharedMesh = GenerateLightsMesh();
            lightsMr.sharedMaterials = new Material[] { headlightMat, taillightMat };

            // 5. Side Mirrors
            CreateSideMirror(visualRoot.transform, carbonDarkMat, glassMat, isLeft: true);
            CreateSideMirror(visualRoot.transform, carbonDarkMat, glassMat, isLeft: false);

            // 6. Dual Exhausts
            CreateExhaust(visualRoot.transform, carbonDarkMat, rimMat, -0.4f);
            CreateExhaust(visualRoot.transform, carbonDarkMat, rimMat, 0.4f);

            return carRoot;
        }

        private static Mesh GenerateCarBodyMesh()
        {
            Mesh mesh = new Mesh { name = "SportsCar_Body_Mesh" };

            // 9 Cross-sections from Front to Rear
            // Each ring has 8 vertices:
            // 0: Bottom-Left, 1: Mid-Left, 2: Upper-Left, 3: Center-Top,
            // 4: Upper-Right, 5: Mid-Right, 6: Bottom-Right, 7: Bottom-Center
            float[] zSections = new float[] { 2.15f, 1.75f, 1.25f, 0.45f, -0.40f, -1.25f, -1.75f, -2.05f };
            float[] halfWidths = new float[] { 0.70f, 0.88f, 0.94f, 0.88f, 0.89f, 0.96f, 0.90f, 0.76f };
            float[] bottomYs = new float[]   { 0.18f, 0.17f, 0.17f, 0.17f, 0.17f, 0.17f, 0.18f, 0.22f };
            float[] waistYs = new float[]    { 0.30f, 0.40f, 0.48f, 0.50f, 0.52f, 0.54f, 0.52f, 0.44f };
            float[] topYs = new float[]      { 0.38f, 0.52f, 0.60f, 0.63f, 0.64f, 0.65f, 0.62f, 0.52f };

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            int numRings = zSections.Length;

            for (int r = 0; r < numRings; r++)
            {
                float z = zSections[r];
                float hw = halfWidths[r];
                float bY = bottomYs[r];
                float wY = waistYs[r];
                float tY = topYs[r];

                // Profile points across X
                Vector3 p0 = new Vector3(-hw * 0.85f, bY, z);
                Vector3 p1 = new Vector3(-hw, wY, z);
                Vector3 p2 = new Vector3(-hw * 0.70f, tY, z);
                Vector3 p3 = new Vector3(0f, tY + 0.03f, z);
                Vector3 p4 = new Vector3(hw * 0.70f, tY, z);
                Vector3 p5 = new Vector3(hw, wY, z);
                Vector3 p6 = new Vector3(hw * 0.85f, bY, z);
                Vector3 p7 = new Vector3(0f, bY - 0.01f, z);

                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
                verts.Add(p4); verts.Add(p5); verts.Add(p6); verts.Add(p7);

                for (int i = 0; i < 8; i++)
                {
                    uvs.Add(new Vector2((float)i / 7f, (float)r / (numRings - 1)));
                }
            }

            // Connect rings
            for (int r = 0; r < numRings - 1; r++)
            {
                int cur = r * 8;
                int next = (r + 1) * 8;

                for (int i = 0; i < 8; i++)
                {
                    int c0 = cur + i;
                    int c1 = cur + ((i + 1) % 8);
                    int n0 = next + i;
                    int n1 = next + ((i + 1) % 8);

                    tris.Add(c0); tris.Add(c1); tris.Add(n0);
                    tris.Add(n0); tris.Add(c1); tris.Add(n1);
                }
            }

            // Front Nose Cap
            int frontStart = 0;
            int frontCenter = verts.Count;
            verts.Add(new Vector3(0f, (bottomYs[0] + topYs[0]) * 0.5f, zSections[0] + 0.12f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < 8; i++)
            {
                int nextI = (i + 1) % 8;
                tris.Add(frontCenter);
                tris.Add(frontStart + i);
                tris.Add(frontStart + nextI);
            }

            // Rear Tail Cap
            int rearStart = (numRings - 1) * 8;
            int rearCenter = verts.Count;
            verts.Add(new Vector3(0f, (bottomYs[numRings - 1] + topYs[numRings - 1]) * 0.5f, zSections[numRings - 1] - 0.10f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int i = 0; i < 8; i++)
            {
                int nextI = (i + 1) % 8;
                tris.Add(rearCenter);
                tris.Add(rearStart + nextI);
                tris.Add(rearStart + i);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Mesh GenerateCanopyMesh()
        {
            Mesh mesh = new Mesh { name = "SportsCar_Canopy_Mesh" };

            // 5 rings defining streamlined glass greenhouse / cockpit
            float[] zSecs = new float[] { 0.95f, 0.45f, -0.20f, -0.80f, -1.30f };
            float[] halfWs = new float[] { 0.55f, 0.68f, 0.70f, 0.65f, 0.52f };
            float[] baseYs = new float[] { 0.62f, 0.63f, 0.64f, 0.64f, 0.63f };
            float[] roofYs = new float[] { 0.76f, 1.05f, 1.08f, 1.02f, 0.70f };

            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();

            int rings = zSecs.Length;
            for (int r = 0; r < rings; r++)
            {
                float z = zSecs[r];
                float hw = halfWs[r];
                float by = baseYs[r];
                float ry = roofYs[r];

                // 6 points across top arch
                Vector3 p0 = new Vector3(-hw, by, z);
                Vector3 p1 = new Vector3(-hw * 0.85f, Mathf.Lerp(by, ry, 0.6f), z);
                Vector3 p2 = new Vector3(-hw * 0.45f, ry, z);
                Vector3 p3 = new Vector3(hw * 0.45f, ry, z);
                Vector3 p4 = new Vector3(hw * 0.85f, Mathf.Lerp(by, ry, 0.6f), z);
                Vector3 p5 = new Vector3(hw, by, z);

                verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3); verts.Add(p4); verts.Add(p5);
                for (int i = 0; i < 6; i++) uvs.Add(new Vector2((float)i / 5f, (float)r / (rings - 1)));
            }

            for (int r = 0; r < rings - 1; r++)
            {
                int cur = r * 6;
                int next = (r + 1) * 6;

                for (int i = 0; i < 5; i++)
                {
                    int c0 = cur + i;
                    int c1 = cur + i + 1;
                    int n0 = next + i;
                    int n1 = next + i + 1;

                    tris.Add(c0); tris.Add(n0); tris.Add(c1);
                    tris.Add(c1); tris.Add(n0); tris.Add(n1);
                }
            }

            // Windshield Front Face
            int fC = verts.Count;
            verts.Add(new Vector3(0f, (baseYs[0] + roofYs[0]) * 0.5f, zSecs[0] + 0.05f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 5; i++)
            {
                tris.Add(fC);
                tris.Add(i + 1);
                tris.Add(i);
            }

            // Rear Window Face
            int rStart = (rings - 1) * 6;
            int rC = verts.Count;
            verts.Add(new Vector3(0f, (baseYs[rings - 1] + roofYs[rings - 1]) * 0.5f, zSecs[rings - 1] - 0.05f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < 5; i++)
            {
                tris.Add(rC);
                tris.Add(rStart + i);
                tris.Add(rStart + i + 1);
            }

            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh GenerateAeroKitMesh()
        {
            Mesh mesh = new Mesh { name = "SportsCar_AeroKit_Mesh" };
            List<Vector3> verts = new List<Vector3>();
            List<int> tris = new List<int>();

            void AddBox(Vector3 center, Vector3 size, Quaternion rot)
            {
                Vector3 half = size * 0.5f;
                Vector3[] corners = new Vector3[]
                {
                    new Vector3(-half.x, -half.y, -half.z),
                    new Vector3(half.x, -half.y, -half.z),
                    new Vector3(half.x, -half.y, half.z),
                    new Vector3(-half.x, -half.y, half.z),
                    new Vector3(-half.x, half.y, -half.z),
                    new Vector3(half.x, half.y, -half.z),
                    new Vector3(half.x, half.y, half.z),
                    new Vector3(-half.x, half.y, half.z)
                };

                int startIdx = verts.Count;
                for (int i = 0; i < 8; i++)
                {
                    verts.Add(center + rot * corners[i]);
                }

                int[] boxTris = new int[]
                {
                    // Bottom
                    startIdx+0, startIdx+1, startIdx+2, startIdx+0, startIdx+2, startIdx+3,
                    // Top
                    startIdx+4, startIdx+6, startIdx+5, startIdx+4, startIdx+7, startIdx+6,
                    // Front
                    startIdx+3, startIdx+2, startIdx+6, startIdx+3, startIdx+6, startIdx+7,
                    // Back
                    startIdx+0, startIdx+5, startIdx+1, startIdx+0, startIdx+4, startIdx+5,
                    // Left
                    startIdx+0, startIdx+3, startIdx+7, startIdx+0, startIdx+7, startIdx+4,
                    // Right
                    startIdx+1, startIdx+5, startIdx+6, startIdx+1, startIdx+6, startIdx+2
                };
                tris.AddRange(boxTris);
            }

            // Front Carbon Splitter
            AddBox(new Vector3(0f, 0.12f, 2.12f), new Vector3(1.72f, 0.04f, 0.32f), Quaternion.identity);

            // Left Side Skirt
            AddBox(new Vector3(-0.90f, 0.13f, 0.0f), new Vector3(0.12f, 0.04f, 2.2f), Quaternion.identity);

            // Right Side Skirt
            AddBox(new Vector3(0.90f, 0.13f, 0.0f), new Vector3(0.12f, 0.04f, 2.2f), Quaternion.identity);

            // Rear Carbon Diffuser
            AddBox(new Vector3(0f, 0.18f, -2.05f), new Vector3(1.68f, 0.06f, 0.35f), Quaternion.Euler(-12f, 0, 0));

            // Rear Spoiler Struts
            AddBox(new Vector3(-0.52f, 0.72f, -1.82f), new Vector3(0.04f, 0.36f, 0.12f), Quaternion.Euler(15f, 0, 0));
            AddBox(new Vector3(0.52f, 0.72f, -1.82f), new Vector3(0.04f, 0.36f, 0.12f), Quaternion.Euler(15f, 0, 0));

            // Rear GT Wing Blade
            AddBox(new Vector3(0f, 0.91f, -1.88f), new Vector3(1.76f, 0.04f, 0.38f), Quaternion.Euler(-7f, 0, 0));
            // Wing Endplates
            AddBox(new Vector3(-0.88f, 0.91f, -1.88f), new Vector3(0.02f, 0.20f, 0.44f), Quaternion.Euler(-7f, 0, 0));
            AddBox(new Vector3(0.88f, 0.91f, -1.88f), new Vector3(0.02f, 0.20f, 0.44f), Quaternion.Euler(-7f, 0, 0));

            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh GenerateLightsMesh()
        {
            Mesh mesh = new Mesh { name = "SportsCar_Lights_Mesh", subMeshCount = 2 };
            List<Vector3> verts = new List<Vector3>();
            List<int> headTris = new List<int>();
            List<int> tailTris = new List<int>();

            void AddQuad(Vector3 center, Vector2 size, Quaternion rot, bool isTail)
            {
                int start = verts.Count;
                Vector3 r = rot * Vector3.right * (size.x * 0.5f);
                Vector3 u = rot * Vector3.up * (size.y * 0.5f);

                verts.Add(center - r - u);
                verts.Add(center + r - u);
                verts.Add(center + r + u);
                verts.Add(center - r + u);

                if (isTail)
                {
                    tailTris.Add(start); tailTris.Add(start + 2); tailTris.Add(start + 1);
                    tailTris.Add(start); tailTris.Add(start + 3); tailTris.Add(start + 2);
                }
                else
                {
                    headTris.Add(start); headTris.Add(start + 1); headTris.Add(start + 2);
                    headTris.Add(start); headTris.Add(start + 2); headTris.Add(start + 3);
                }
            }

            // Sleek Angled Headlights
            AddQuad(new Vector3(-0.62f, 0.44f, 1.95f), new Vector2(0.28f, 0.08f), Quaternion.Euler(-15f, 25f, -10f), false);
            AddQuad(new Vector3(0.62f, 0.44f, 1.95f), new Vector2(0.28f, 0.08f), Quaternion.Euler(-15f, -25f, 10f), false);

            // Sleek Modern LED Lightbar / Taillights
            AddQuad(new Vector3(-0.58f, 0.52f, -2.06f), new Vector2(0.40f, 0.06f), Quaternion.Euler(5f, -15f, 0f), true);
            AddQuad(new Vector3(0.58f, 0.52f, -2.06f), new Vector2(0.40f, 0.06f), Quaternion.Euler(5f, 15f, 0f), true);
            AddQuad(new Vector3(0.0f, 0.53f, -2.08f), new Vector2(0.60f, 0.03f), Quaternion.Euler(5f, 0f, 0f), true);

            mesh.SetVertices(verts);
            mesh.SetTriangles(headTris, 0);
            mesh.SetTriangles(tailTris, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateSideMirror(Transform parent, Material carbonMat, Material glassMat, bool isLeft)
        {
            float side = isLeft ? -1f : 1f;
            GameObject mirror = new GameObject(isLeft ? "Mirror_L" : "Mirror_R");
            mirror.transform.parent = parent;
            mirror.transform.localPosition = new Vector3(side * 0.76f, 0.65f, 0.62f);
            mirror.transform.localRotation = Quaternion.Euler(0, side * 15f, 0);

            // Stem
            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stem.transform.parent = mirror.transform;
            stem.transform.localPosition = Vector3.zero;
            stem.transform.localScale = new Vector3(0.08f, 0.03f, 0.05f);
            stem.GetComponent<MeshRenderer>().sharedMaterial = carbonMat;
            Object.DestroyImmediate(stem.GetComponent<Collider>());

            // Housing
            GameObject housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.transform.parent = mirror.transform;
            housing.transform.localPosition = new Vector3(side * 0.12f, 0.02f, 0f);
            housing.transform.localScale = new Vector3(0.18f, 0.09f, 0.14f);
            housing.GetComponent<MeshRenderer>().sharedMaterial = carbonMat;
            Object.DestroyImmediate(housing.GetComponent<Collider>());

            // Glass
            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glass.transform.parent = mirror.transform;
            glass.transform.localPosition = new Vector3(side * 0.12f, 0.02f, -0.071f);
            glass.transform.localRotation = Quaternion.Euler(0, 180f, 0);
            glass.transform.localScale = new Vector3(0.16f, 0.08f, 1f);
            glass.GetComponent<MeshRenderer>().sharedMaterial = glassMat;
            Object.DestroyImmediate(glass.GetComponent<Collider>());
        }

        private static void CreateExhaust(Transform parent, Material carbonMat, Material chromeMat, float xPos)
        {
            GameObject ex = new GameObject($"Exhaust_{xPos}");
            ex.transform.parent = parent;
            ex.transform.localPosition = new Vector3(xPos, 0.22f, -2.06f);

            GameObject tip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tip.transform.parent = ex.transform;
            tip.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            tip.transform.localScale = new Vector3(0.12f, 0.06f, 0.12f);
            tip.GetComponent<MeshRenderer>().sharedMaterial = chromeMat;
            Object.DestroyImmediate(tip.GetComponent<Collider>());
        }

        public static Transform CreateHighDetailWheel(string name, Transform parent, Vector3 localPos, Material tireMat, Material rimMat, Material brakeMat)
        {
            GameObject pivot = new GameObject(name);
            pivot.transform.parent = parent;
            pivot.transform.localPosition = localPos;
            pivot.transform.localRotation = Quaternion.identity;

            GameObject spinner = new GameObject("Spinner");
            spinner.transform.parent = pivot.transform;
            spinner.transform.localPosition = Vector3.zero;
            spinner.transform.localRotation = Quaternion.identity;

            // 1. Tire with clean geometry
            GameObject tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tire.name = "Tire";
            tire.transform.parent = spinner.transform;
            tire.transform.localPosition = Vector3.zero;
            tire.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            tire.transform.localScale = new Vector3(0.68f, 0.18f, 0.68f);
            tire.GetComponent<MeshRenderer>().sharedMaterial = tireMat;
            Object.DestroyImmediate(tire.GetComponent<Collider>());

            // 2. Alloy Wheel Rim
            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Rim";
            rim.transform.parent = spinner.transform;
            float outSign = localPos.x > 0 ? 0.015f : -0.015f;
            rim.transform.localPosition = new Vector3(outSign, 0, 0);
            rim.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            rim.transform.localScale = new Vector3(0.50f, 0.185f, 0.50f);
            rim.GetComponent<MeshRenderer>().sharedMaterial = rimMat;
            Object.DestroyImmediate(rim.GetComponent<Collider>());

            // 3. Multi-spoke detail
            int spokes = 5;
            for (int s = 0; s < spokes; s++)
            {
                float angle = s * (360f / spokes);
                GameObject spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spoke.name = $"Spoke_{s}";
                spoke.transform.parent = spinner.transform;
                spoke.transform.localPosition = new Vector3(outSign * 1.05f, 0, 0);
                spoke.transform.localRotation = Quaternion.Euler(angle, 0, 0);
                spoke.transform.localScale = new Vector3(0.035f, 0.44f, 0.05f);
                spoke.GetComponent<MeshRenderer>().sharedMaterial = rimMat;
                Object.DestroyImmediate(spoke.GetComponent<Collider>());
            }

            // 4. Brake Disc & Red Caliper (Attached to pivot, doesn't spin wildly)
            GameObject brakeDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            brakeDisc.name = "BrakeDisc";
            brakeDisc.transform.parent = pivot.transform;
            brakeDisc.transform.localPosition = new Vector3(-outSign * 0.3f, 0, 0);
            brakeDisc.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            brakeDisc.transform.localScale = new Vector3(0.42f, 0.02f, 0.42f);
            brakeDisc.GetComponent<MeshRenderer>().sharedMaterial = rimMat;
            Object.DestroyImmediate(brakeDisc.GetComponent<Collider>());

            GameObject caliper = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caliper.name = "BrakeCaliper";
            caliper.transform.parent = pivot.transform;
            caliper.transform.localPosition = new Vector3(-outSign * 0.2f, 0.15f, 0.05f);
            caliper.transform.localScale = new Vector3(0.08f, 0.12f, 0.16f);
            caliper.GetComponent<MeshRenderer>().sharedMaterial = brakeMat;
            Object.DestroyImmediate(caliper.GetComponent<Collider>());

            return pivot.transform;
        }
    }
}
