using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Track;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>
    /// A wide post-apocalyptic circuit above a ruined wasteland, generated from <see cref="WastelandLayout"/> and driven through
    /// the scene's <see cref="TrackPath"/> (AI, laps, respawn and the grid follow it). A broken bridge to jump, a boost
    /// pad in the middle of the road before it, a tunnel whose walls and ceiling can be driven at speed
    /// (<see cref="GravitySurface"/>), one semi-sharp corner with an open edge.
    /// </summary>
    public sealed class WastelandTrack : MonoBehaviour
    {
        public const float RailHeight = 1f, RailWidth = 0.5f, SkinDepth = 1.2f;
        public const float PadHalfWidth = 2f, PadLength = 6f, PadRadius = 2.2f;
        /// <summary>Tunnel cross-section: flat floor ±<see cref="TubeFloor"/>, fillets of <see cref="TubeFillet"/>, walls to the road edge.</summary>
        public const float TubeFloor = 5f, TubeFillet = 4f, TubeWallTop = 7f, TubeHeight = 11f;
        private const int FilletSteps = 6;

        private TrackPath path;
        private WastelandLayout layout;
        private Material solid, glow, ownedSolid;
        private readonly List<GameObject> hidden = new List<GameObject>();
        private readonly List<Renderer> hiddenMarkers = new List<Renderer>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<BoostGate> pads = new List<BoostGate>();

        public WastelandLayout Layout => layout;
        public IReadOnlyList<BoostGate> Pads => pads;

        public static WastelandTrack Create(TrackPath path)
        {
            var map = new GameObject("WastelandTrack").AddComponent<WastelandTrack>();
            map.path = path;
            map.layout = new WastelandLayout();
            path.UseLayout(map.layout.Points, WastelandLayout.HalfWidth, map.layout.StartLine);
            path.RespawnGuard = map.layout.SafeRespawn;
            path.Lures = new[] { new TrackPath.LaneLure { distance = map.layout.BoostPad, lane = 0f, reach = 60f } };

            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            map.solid = AbyssTrack.SceneryMaterial();
            if (map.solid == null) map.solid = map.ownedSolid = new Material(lit) { name = "WastelandSolid" };
            map.glow = new Material(map.solid) { name = "WastelandGlow" };
            map.glow.SetFloat("_EmissionBoost", 3f);
            AbyssTrack.HideScenery(map.hidden, AbyssTrack.Scenery);
            AbyssTrack.HideScenery(map.hidden, new[] { "Environment/Mountains" });
            // The path's marker dashes lie on the ground circuit; the TrackPath itself must stay active.
            foreach (var r in path.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; map.hiddenMarkers.Add(r); }

            map.Build();
            return map;
        }

        // ---------- geometry ----------

        private struct Ring { public float d; public Vector3 centre, right, up; }

        private Ring RingAt(float d)
        {
            Vector3 c = path.Position(d);
            c.y = layout.Height(d);
            return new Ring { d = d, centre = c, right = path.Right(d), up = Vector3.up };
        }

        private static Vector3 At(Ring r, float x, float y) => r.centre + r.right * x + r.up * y;

        /// <summary>Ring distances: every ~2 m plus the exact edges of the gap and the tunnel phases.</summary>
        private List<float> RingDistances()
        {
            int n = Mathf.CeilToInt(layout.Length / WastelandLayout.Step);
            var list = new List<float>(n + 16);
            for (int i = 0; i < n; i++) list.Add(i * layout.Length / n);
            float t0 = layout.TunnelStart, t1 = layout.TunnelEnd, f = WastelandLayout.TunnelFlare;
            foreach (float d in new[] { layout.GapStart, layout.GapEnd, t0, t0 + f, t0 + 2f * f, t1 - 2f * f, t1 - f, t1 })
                if (!list.Exists(x => Mathf.Abs(x - d) < 0.05f)) list.Add(d);
            list.Sort();
            list.Add(layout.Length);   // closes the loop onto ring 0
            return list;
        }

        // Half of the tunnel strip (right side) from the floor centre to the ceiling centre, with the arc length
        // of every point above the lower fillet (the part the mouth reveals gradually).
        private static void TubeHalf(List<Vector2> points, List<float> reveal)
        {
            points.Add(new Vector2(0f, 0f)); reveal.Add(-1f);
            points.Add(new Vector2(TubeFloor, 0f)); reveal.Add(-1f);
            float wallX = TubeFloor + TubeFillet;
            for (int k = 1; k <= FilletSteps; k++)
            {
                float a = (-90f + 90f * k / FilletSteps) * Mathf.Deg2Rad;
                points.Add(new Vector2(TubeFloor + Mathf.Cos(a) * TubeFillet, TubeFillet + Mathf.Sin(a) * TubeFillet)); reveal.Add(-1f);
            }
            float wall = TubeWallTop - TubeFillet, arc = Mathf.PI * 0.5f * TubeFillet, ceiling = TubeFloor;
            float total = wall + arc + ceiling;
            points.Add(new Vector2(wallX, TubeWallTop)); reveal.Add(wall / total);
            for (int k = 1; k <= FilletSteps; k++)
            {
                float a = 90f * k / FilletSteps * Mathf.Deg2Rad;
                points.Add(new Vector2(TubeFloor + Mathf.Cos(a) * TubeFillet, TubeWallTop + Mathf.Sin(a) * TubeFillet)); reveal.Add((wall + arc * k / FilletSteps) / total);
            }
            points.Add(new Vector2(0f, TubeHeight)); reveal.Add(1f);
        }

        private static readonly List<Vector2> tubeStrip = new List<Vector2>();
        private static readonly List<float> tubeReveal = new List<float>();

        // Left ceiling centre → down the left wall → across the floor → up to the right ceiling centre.
        // Walking the strip in this order makes every face look into the tube.
        private static void BuildTubeStrip()
        {
            if (tubeStrip.Count > 0) return;
            var half = new List<Vector2>(); var reveal = new List<float>();
            TubeHalf(half, reveal);
            for (int i = half.Count - 1; i >= 1; i--) { tubeStrip.Add(new Vector2(-half[i].x, half[i].y)); tubeReveal.Add(reveal[i]); }
            for (int i = 0; i < half.Count; i++) { tubeStrip.Add(half[i]); tubeReveal.Add(reveal[i]); }
        }

        private static Vector2 TubePoint(int index, float fillet)
        {
            Vector2 p = tubeStrip[index];
            // Lower fillet rises with the flare: at 0 the floor is still the flat road.
            if (tubeReveal[index] < 0f) p.y *= fillet;
            return p;
        }

        private static bool TubePresent(int index, float reveal) => tubeReveal[index] < 0f || tubeReveal[index] <= reveal + 1e-4f;

        private void Build()
        {
            BuildTubeStrip();
            var road = new MeshChunkWriter("WastelandRoad");
            var tube = new MeshChunkWriter("WastelandTube");
            var skin = new MeshChunkWriter("WastelandSkin");
            var rails = new MeshChunkWriter("WastelandRails");
            var lights = new MeshChunkWriter("WastelandLights");

            var asphalt = new Color32(57, 53, 48, 0);
            var band = new Color32(68, 61, 51, 0);
            var underside = new Color32(68, 42, 32, 0);
            var panel = new Color32(87, 64, 45, 0);
            var edgeGlow = new Color32(255, 149, 46, 255);
            var railColor = new Color32(112, 63, 39, 0);
            var railGlow = new Color32(255, 92, 34, 255);

            var distances = RingDistances();
            Vector2 hairpin = layout.Hairpin;
            for (int i = 0; i + 1 < distances.Count; i++)
            {
                float d0 = distances[i], d1 = distances[i + 1], mid = (d0 + d1) * 0.5f;
                if (layout.InGap(mid)) continue;
                Ring a = RingAt(d0), b = RingAt(d1);
                bool stripe = Mathf.FloorToInt(mid / 10f) % 2 == 0;
                if (layout.InTunnel(mid))
                {
                    layout.TunnelShape(d0, out float fa, out float ra);
                    layout.TunnelShape(d1, out float fb, out float rb);
                    for (int j = 0; j + 1 < tubeStrip.Count; j++)
                    {
                        if (!TubePresent(j, ra) || !TubePresent(j + 1, ra) || !TubePresent(j, rb) || !TubePresent(j + 1, rb)) continue;
                        Vector2 pa = TubePoint(j, fa), qa = TubePoint(j + 1, fa), pb = TubePoint(j, fb), qb = TubePoint(j + 1, fb);
                        tube.AddQuad(At(a, pa.x, pa.y), At(b, pb.x, pb.y), At(b, qb.x, qb.y), At(a, qa.x, qa.y), stripe ? panel : asphalt);
                        // Outer shell, facing out, so the tube is solid from outside too.
                        Vector2 oa = Outward(pa), ob = Outward(pb), oq = Outward(qb), op = Outward(qa);
                        skin.AddQuad(At(a, op.x, op.y), At(b, oq.x, oq.y), At(b, ob.x, ob.y), At(a, oa.x, oa.y), underside);
                    }
                    // Glowing hoops every 12 m, the last 25 m red: time to come down before the exit.
                    if (ra >= 1f && rb >= 1f && Mathf.Repeat(mid, 12f) < d1 - d0)
                        Hoop(lights, a, b, layout.TunnelEnd - mid < 25f ? new Color32(255, 70, 70, 255) : edgeGlow);
                    continue;
                }

                // Flat road deck in four panels (±TubeFloor meets the tunnel strip's floor points).
                float h = WastelandLayout.HalfWidth, f = TubeFloor;
                float[] xs = { -h, -f, 0f, f, h };
                for (int k = 0; k + 1 < xs.Length; k++)
                    road.AddQuad(At(a, xs[k], 0f), At(b, xs[k], 0f), At(b, xs[k + 1], 0f), At(a, xs[k + 1], 0f), stripe ? asphalt : band);
                // Underside and sides: the deck is a slab floating in the void.
                skin.AddQuad(At(a, -h, 0f), At(a, -h, -SkinDepth), At(b, -h, -SkinDepth), At(b, -h, 0f), underside);
                skin.AddQuad(At(b, h, 0f), At(b, h, -SkinDepth), At(a, h, -SkinDepth), At(a, h, 0f), underside);
                skin.AddQuad(At(a, h, -SkinDepth), At(b, h, -SkinDepth), At(b, -h, -SkinDepth), At(a, -h, -SkinDepth), underside);
                if (Mathf.Abs(d1 - layout.GapStart) < 0.01f) Cap(skin, b, true, underside);
                if (Mathf.Abs(d0 - layout.GapEnd) < 0.01f) Cap(skin, a, false, underside);

                // Edge lights on the deck.
                Strip(lights, a, b, -h + 0.25f, -h + 0.45f, 0.02f, edgeGlow);
                Strip(lights, a, b, h - 0.45f, h - 0.25f, 0.02f, edgeGlow);

                // Rails, except over the tunnel mouths, the gap and the outside of the semi-sharp corner.
                bool openLeft = mid > hairpin.x - 10f && mid < hairpin.y + 15f;   // right-hand corner: the outside is the left
                if (!openLeft) Rail(rails, lights, a, b, -1f, railColor, railGlow);
                Rail(rails, lights, a, b, 1f, railColor, railGlow);
                if (openLeft)
                {
                    // Red and white kerb warns of the open edge.
                    var kerb = Mathf.FloorToInt(mid / 2f) % 2 == 0 ? new Color32(235, 50, 60, 120) : new Color32(240, 240, 240, 60);
                    Strip(lights, a, b, -h, -h + 1.2f, 0.025f, kerb);
                }
            }

            DecorateBridge(lights);
            BuildPads(lights);
            BuildStartLine(lights, rails);
            BuildWasteland();

            Add("Road", road, solid, true, false);
            Add("Tube", tube, solid, true, true);
            Add("Skin", skin, solid, false, false);
            Add("Rails", rails, solid, true, false);
            Add("Lights", lights, glow, false, false);
        }

        private static Vector2 Outward(Vector2 p)
        {
            Vector2 dir = p - new Vector2(0f, TubeHeight * 0.5f);
            return p + dir.normalized * 0.6f;
        }

        // End face of the deck at the gap: the slab visibly ends, and a short jump hits it.
        private static void Cap(MeshChunkWriter w, Ring r, bool facingForward, Color32 color)
        {
            float h = WastelandLayout.HalfWidth;
            Vector3 a = At(r, -h, 0f), b = At(r, h, 0f), c = At(r, h, -SkinDepth), d = At(r, -h, -SkinDepth);
            if (facingForward) w.AddQuad(b, a, d, c, color); else w.AddQuad(a, b, c, d, color);
        }

        private static void Strip(MeshChunkWriter w, Ring a, Ring b, float x0, float x1, float lift, Color32 color)
        {
            w.AddQuad(At(a, x0, lift), At(b, x0, lift), At(b, x1, lift), At(a, x1, lift), color);
        }

        private static void Rail(MeshChunkWriter solidW, MeshChunkWriter glowW, Ring a, Ring b, float side, Color32 color, Color32 top)
        {
            float inner = side * WastelandLayout.HalfWidth, outer = side * (WastelandLayout.HalfWidth + RailWidth);
            // Inner face looks at the road, outer face away; the top carries a glowing line.
            if (side < 0f)
            {
                solidW.AddQuad(At(a, inner, 0f), At(a, inner, RailHeight), At(b, inner, RailHeight), At(b, inner, 0f), color);
                solidW.AddQuad(At(b, outer, 0f), At(b, outer, RailHeight), At(a, outer, RailHeight), At(a, outer, 0f), color);
            }
            else
            {
                solidW.AddQuad(At(b, inner, 0f), At(b, inner, RailHeight), At(a, inner, RailHeight), At(a, inner, 0f), color);
                solidW.AddQuad(At(a, outer, 0f), At(a, outer, RailHeight), At(b, outer, RailHeight), At(b, outer, 0f), color);
            }
            float lo = Mathf.Min(inner, outer), hi = Mathf.Max(inner, outer);
            Strip(glowW, a, b, lo, hi, RailHeight, top);
        }

        private static void Hoop(MeshChunkWriter w, Ring a, Ring b, Color32 color)
        {
            // A thin band just inside the tube wall, one quad per strip facet.
            Ring r = a; r.centre = Vector3.Lerp(a.centre, b.centre, 0.5f);
            Vector3 along = (b.centre - a.centre).normalized * 0.3f;
            for (int j = 0; j + 1 < tubeStrip.Count; j++)
            {
                Vector2 p = Inward(tubeStrip[j]), q = Inward(tubeStrip[j + 1]);
                Vector3 pw = At(r, p.x, p.y), qw = At(r, q.x, q.y);
                w.AddQuad(pw - along, pw + along, qw + along, qw - along, color);
            }
        }

        private static Vector2 Inward(Vector2 p) => p + (new Vector2(0f, TubeHeight * 0.5f) - p).normalized * 0.04f;

        // Hazard stripes at the lip and the landing edge, arrows on the run-up.
        private void DecorateBridge(MeshChunkWriter w)
        {
            var yellow = new Color32(255, 200, 40, 255);
            var dark = new Color32(30, 28, 40, 0);
            float h = WastelandLayout.HalfWidth;
            foreach (float edge in new[] { layout.GapStart - 1.6f, layout.GapEnd + 0.1f })
            {
                Ring a = RingAt(edge), b = RingAt(edge + 1.5f);
                for (int k = 0; k < 9; k++)
                {
                    float x0 = -h + k * 2f, x1 = x0 + 2f;
                    Strip(w, a, b, x0, x1, 0.03f, k % 2 == 0 ? yellow : dark);
                }
            }
            for (int k = 1; k <= 3; k++) Arrow(w, RingAt(layout.GapStart - 10f - k * 9f), 0f, 1.6f, 0.03f, yellow, false);
        }

        private static void Arrow(MeshChunkWriter w, Ring r, float x, float size, float lift, Color32 color, bool ceiling)
        {
            Vector3 fwd = Vector3.Cross(r.right, r.up);
            Vector3 c = ceiling ? At(r, x, TubeHeight - lift) : At(r, x, lift);
            Vector3 tip = c + fwd * size, left = c - r.right * size - fwd * size * 0.3f, right = c + r.right * size - fwd * size * 0.3f;
            Vector3 notch = c + fwd * size * 0.1f;
            if (ceiling) { w.AddTriangle(left, notch, tip, color); w.AddTriangle(notch, right, tip, color); }
            else { w.AddTriangle(left, tip, notch, color); w.AddTriangle(notch, tip, right, color); }
        }

        private void BuildPads(MeshChunkWriter w)
        {
            // The contested pad in the middle of the road before the jump, and a reward strip on the tunnel ceiling.
            AddPad(w, layout.BoostPad, false);
            AddPad(w, (layout.TunnelStart + layout.TunnelEnd) * 0.5f, true);
        }

        private void AddPad(MeshChunkWriter w, float d, bool ceiling)
        {
            Ring r = RingAt(d);
            Vector3 fwd = Vector3.Cross(r.right, r.up);
            var orange = new Color32(255, 140, 30, 255);
            var gold = new Color32(255, 225, 80, 255);
            for (int k = 0; k < 3; k++)
            {
                Ring at = RingAt(d - PadLength * 0.5f + 1f + k * 2f);
                Arrow(w, at, 0f, PadHalfWidth * 0.9f, 0.035f, k % 2 == 0 ? orange : gold, ceiling);
            }
            float surface = ceiling ? TubeHeight : 0f;
            Vector3 up = ceiling ? -Vector3.up : Vector3.up;
            var go = new GameObject(ceiling ? "CeilingBoostPad" : "BoostPad", typeof(BoostGate), typeof(BoxCollider));
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(At(r, 0f, surface) + up * 1.5f, Quaternion.LookRotation(fwd, up));
            var trigger = go.GetComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(PadHalfWidth * 2.2f, 3f, PadLength);
            var gate = go.GetComponent<BoostGate>();
            gate.radius = PadRadius;
            pads.Add(gate);
        }

        private void BuildStartLine(MeshChunkWriter w, MeshChunkWriter rails)
        {
            float h = WastelandLayout.HalfWidth;
            Ring a = RingAt(layout.StartLine - 0.6f), b = RingAt(layout.StartLine + 0.6f);
            for (int k = 0; k < 18; k++)
                for (int row = 0; row < 2; row++)
                {
                    Ring ra = row == 0 ? a : RingAt(layout.StartLine), rb = row == 0 ? RingAt(layout.StartLine) : b;
                    Strip(w, ra, rb, -h + k, -h + k + 1f, 0.03f, (k + row) % 2 == 0 ? new Color32(240, 240, 250, 90) : new Color32(25, 25, 35, 0));
                }
            // A glowing arch over the line.
            Ring s = RingAt(layout.StartLine);
            var rotation = Quaternion.LookRotation(Vector3.Cross(s.right, s.up), Vector3.up);
            foreach (float side in new[] { -1f, 1f })
                LowPoly.Box(rails, Matrix4x4.TRS(At(s, side * (h + 1.2f), 0f), rotation, Vector3.one), Vector3.up * 3.5f, new Vector3(0.8f, 7f, 0.8f), new Color32(112, 63, 39, 0));
            LowPoly.Box(w, Matrix4x4.TRS(At(s, 0f, 7f), rotation, Vector3.one), Vector3.zero, new Vector3(2f * h + 3.2f, 0.7f, 0.9f), new Color32(255, 153, 54, 255));
        }

        // Dust far below the road has no collider: falling off the bridge or the open corner still costs a reset.
        private void BuildWasteland()
        {
            var dust = new MeshChunkWriter("WastelandDust");
            var scrap = new MeshChunkWriter("WastelandScrap");
            var bounds = new Bounds(layout.Points[0], Vector3.zero);
            foreach (var point in layout.Points) bounds.Encapsulate(point);
            var random = new System.Random(1937);
            float x0 = Mathf.Floor((bounds.min.x - 150f) / 60f) * 60f;
            float x1 = Mathf.Ceil((bounds.max.x + 150f) / 60f) * 60f;
            float z0 = Mathf.Floor((bounds.min.z - 150f) / 60f) * 60f;
            float z1 = Mathf.Ceil((bounds.max.z + 150f) / 60f) * 60f;
            for (float x = x0; x < x1; x += 60f)
                for (float z = z0; z < z1; z += 60f)
                {
                    var tint = random.Next(3) == 0 ? new Color32(88, 60, 42, 0) : new Color32(105, 72, 47, 0);
                    dust.AddQuad(new Vector3(x, -35f, z), new Vector3(x, -35f, z + 60f),
                        new Vector3(x + 60f, -35f, z + 60f), new Vector3(x + 60f, -35f, z), tint);
                }
            // Rusted bridge supports and broken roadside silhouettes stay clear of the drivable width.
            foreach (float d in new[] { layout.GapStart - 6f, layout.GapEnd + 7f })
                foreach (float side in new[] { -1f, 1f })
                {
                    Ring r = RingAt(d);
                    float top = r.centre.y - 1f;
                    Vector3 centre = At(r, side * (WastelandLayout.HalfWidth + 2.5f), (-35f + top) * 0.5f - r.centre.y);
                    LowPoly.Box(scrap, Matrix4x4.identity, centre, new Vector3(2f, top + 35f, 2f), new Color32(100, 54, 35, 0));
                }
            for (int i = 0; i < 38; i++)
            {
                float d = (i + 0.5f) * layout.Length / 38f;
                Ring r = RingAt(d);
                float side = i % 2 == 0 ? -1f : 1f;
                float distance = 22f + (float)random.NextDouble() * 30f;
                float height = 6f + (float)random.NextDouble() * 17f;
                Vector3 basePoint = At(r, side * distance, -3f);
                var rotation = Quaternion.Euler(0f, random.Next(0, 180), random.Next(-12, 13));
                LowPoly.Box(scrap, Matrix4x4.TRS(basePoint, rotation, Vector3.one), Vector3.up * (height * 0.5f),
                    new Vector3(3f + random.Next(0, 4), height, 3f + random.Next(0, 4)),
                    i % 3 == 0 ? new Color32(105, 58, 37, 0) : new Color32(88, 79, 65, 0));
            }
            Add("Dust", dust, solid, false, false);
            Add("Scrap", scrap, solid, false, false);
        }

        private void Add(string groupName, MeshChunkWriter writer, Material material, bool collide, bool gravity)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(transform, false);
            int layer = LayerMask.NameToLayer("Track");
            foreach (var mesh in writer.Finish())
            {
                meshes.Add(mesh);
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(group.transform, false);
                if (layer >= 0 && collide) go.layer = layer;
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                if (!collide) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                if (gravity) go.AddComponent<GravitySurface>();
            }
        }

        public void Restore()
        {
            if (path != null) { path.ClearLayout(); path = null; }
            foreach (var go in hidden) if (go != null) go.SetActive(true);
            hidden.Clear();
            foreach (var r in hiddenMarkers) if (r != null) r.enabled = true;
            hiddenMarkers.Clear();
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Restore();
            foreach (var mesh in meshes)
                if (mesh != null) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            foreach (var m in new[] { glow, ownedSolid })
                if (m != null) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
        }
    }
}
