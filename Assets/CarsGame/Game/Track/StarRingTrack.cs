using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Track;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>
    /// "Star Ring": a wide neon circuit floating in space, generated from <see cref="StarRingLayout"/> and driven through
    /// the scene's <see cref="TrackPath"/> (AI, laps, respawn and the grid follow it). A broken bridge to jump, a boost
    /// pad in the middle of the road before it, two tunnels whose walls and ceilings can be driven at speed
    /// (<see cref="GravitySurface"/>), one semi-sharp corner with an open edge, and neutral turrets.
    /// </summary>
    public sealed class StarRingTrack : MonoBehaviour
    {
        public const float RailHeight = 1f, RailWidth = 0.5f, SkinDepth = 1.2f;
        public const float NarrowHalfWidth = 6f;
        public const float PadHalfWidth = 2f, PadLength = 6f, PadRadius = 2.2f;
        /// <summary>Tunnel cross-section: flat floor ±<see cref="TubeFloor"/>, fillets of <see cref="TubeFillet"/>, walls at the road edge.</summary>
        public const float TubeFloor = 5f, TubeFillet = 4f, TubeWallTop = 7f, TubeHeight = 11f;
        private const int FilletSteps = 6;

        private TrackPath path;
        private StarRingLayout layout;
        private Material solid, glow, shells, ownedSolid;
        private readonly List<GameObject> hidden = new List<GameObject>();
        private readonly List<Renderer> hiddenMarkers = new List<Renderer>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<SpaceTurret> turrets = new List<SpaceTurret>();
        private readonly List<BoostGate> pads = new List<BoostGate>();
        private readonly List<RaceProgress> observed = new List<RaceProgress>();
        private GameObject railsGroup, shouldersGroup, warningGroup, narrowEdgeGroup;
        public int Stage { get; private set; }

        public StarRingLayout Layout => layout;
        public IReadOnlyList<SpaceTurret> Turrets => turrets;
        public IReadOnlyList<BoostGate> Pads => pads;

        public static StarRingTrack Create(TrackPath path, RaceManager race)
        {
            var map = new GameObject("StarRingTrack").AddComponent<StarRingTrack>();
            map.path = path;
            map.layout = new StarRingLayout();
            path.UseLayout(map.layout.Points, StarRingLayout.HalfWidth, map.layout.StartLine);
            path.RespawnGuard = map.layout.SafeRespawn;
            path.Lures = new[] { new TrackPath.LaneLure { distance = map.layout.BoostPad, lane = 0f, reach = 60f } };

            var lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            map.solid = AbyssTrack.SceneryMaterial();
            if (map.solid == null) map.solid = map.ownedSolid = new Material(lit) { name = "StarRingSolid" };
            map.glow = new Material(map.solid) { name = "StarRingGlow" };
            map.glow.SetFloat("_EmissionBoost", 3f);
            map.shells = new Material(lit) { name = "TurretShell" };
            map.shells.SetColor(map.shells.HasProperty("_BaseColor") ? "_BaseColor" : "_Color", new Color(1f, 0.2f, 0.35f));
            AbyssTrack.HideScenery(map.hidden, AbyssTrack.Scenery);
            AbyssTrack.HideScenery(map.hidden, new[] { "Environment/Mountains" });
            // The path's marker dashes lie on the ground circuit; the TrackPath itself must stay active.
            foreach (var r in path.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; map.hiddenMarkers.Add(r); }

            map.Build(race);
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
            int n = Mathf.CeilToInt(layout.Length / StarRingLayout.Step);
            var list = new List<float>(n + 16);
            for (int i = 0; i < n; i++) list.Add(i * layout.Length / n);
            float t0 = layout.TunnelStart, t1 = layout.TunnelEnd, f = StarRingLayout.TunnelFlare;
            float u0 = layout.RuinTunnelStart, u1 = layout.RuinTunnelEnd;
            foreach (float d in new[] { layout.GapStart, layout.GapEnd, t0, t0 + f, t0 + 2f * f, t1 - 2f * f, t1 - f, t1,
                u0, u0 + f, u0 + 2f * f, u1 - 2f * f, u1 - f, u1 })
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
            // The lower fillet rises with the flare: at 0 the floor is still the flat road.
            if (tubeReveal[index] < 0f) p.y *= fillet;
            return p;
        }

        private static bool TubePresent(int index, float reveal) => tubeReveal[index] < 0f || tubeReveal[index] <= reveal + 1e-4f;

        private void Build(RaceManager race)
        {
            BuildTubeStrip();
            var road = new MeshChunkWriter("StarRoad");
            var shoulders = new MeshChunkWriter("StarShoulders");
            var tube = new MeshChunkWriter("StarTube");
            var skin = new MeshChunkWriter("StarSkin");
            var shoulderSkin = new MeshChunkWriter("StarShoulderSkin");
            var rails = new MeshChunkWriter("StarRails");
            var lights = new MeshChunkWriter("StarLights");
            var shoulderLights = new MeshChunkWriter("StarShoulderLights");
            var railLights = new MeshChunkWriter("StarRailLights");
            var warnings = new MeshChunkWriter("StarWarnings");
            var narrowEdge = new MeshChunkWriter("StarNarrowEdge");
            var structures = new MeshChunkWriter("StarRuins");

            var asphalt = new Color32(51, 48, 48, 0);
            var band = new Color32(68, 55, 49, 0);
            var underside = new Color32(31, 28, 31, 0);
            var panel = new Color32(76, 62, 55, 0);
            var edgeGlow = new Color32(255, 119, 50, 255);
            var railColor = new Color32(101, 77, 63, 0);
            var railGlow = new Color32(255, 85, 46, 255);

            var distances = RingDistances();
            Vector2 hairpin = layout.Hairpin;
            float openSide = StarRingLayout.HairpinOutside;
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
                        Hoop(lights, a, b, (mid < layout.TunnelEnd ? layout.TunnelEnd - mid : layout.RuinTunnelEnd - mid) < 25f
                            ? new Color32(255, 70, 70, 255) : edgeGlow);
                    continue;
                }

                // Flat road deck: the outer panels can disappear without changing the tunnel floor.
                float h = StarRingLayout.HalfWidth, f = TubeFloor;
                float n = NarrowHalfWidth;
                float[] xs = { -h, -n, -f, 0f, f, n, h };
                for (int k = 0; k + 1 < xs.Length; k++)
                {
                    var writer = k == 0 || k == xs.Length - 2 ? shoulders : road;
                    writer.AddQuad(At(a, xs[k], 0f), At(b, xs[k], 0f), At(b, xs[k + 1], 0f), At(a, xs[k + 1], 0f), stripe ? asphalt : band);
                }
                // The removable shoulders have their own colliders; the core stays solid on lap three.
                skin.AddQuad(At(a, -n, 0f), At(a, -n, -SkinDepth), At(b, -n, -SkinDepth), At(b, -n, 0f), underside);
                skin.AddQuad(At(b, n, 0f), At(b, n, -SkinDepth), At(a, n, -SkinDepth), At(a, n, 0f), underside);
                skin.AddQuad(At(a, n, -SkinDepth), At(b, n, -SkinDepth), At(b, -n, -SkinDepth), At(a, -n, -SkinDepth), underside);
                foreach (float side in new[] { -1f, 1f })
                {
                    float inner = side * n, outer = side * h;
                    shoulderSkin.AddQuad(At(a, inner, -SkinDepth), At(b, inner, -SkinDepth), At(b, outer, -SkinDepth), At(a, outer, -SkinDepth), underside);
                    Strip(warnings, a, b, Mathf.Min(outer - side * 1.1f, outer), Mathf.Max(outer - side * 1.1f, outer), 0.035f,
                        Mathf.FloorToInt(mid / 3f) % 2 == 0 ? new Color32(255, 50, 38, 140) : new Color32(115, 28, 29, 65));
                    Strip(narrowEdge, a, b, Mathf.Min(inner - side * 0.35f, inner), Mathf.Max(inner - side * 0.35f, inner), 0.035f, new Color32(255, 56, 38, 255));
                }
                if (Mathf.Abs(d1 - layout.GapStart) < 0.01f) Cap(skin, b, true, underside);
                if (Mathf.Abs(d0 - layout.GapEnd) < 0.01f) Cap(skin, a, false, underside);

                // Edge lights on the deck.
                Strip(shoulderLights, a, b, -h + 0.25f, -h + 0.45f, 0.02f, edgeGlow);
                Strip(shoulderLights, a, b, h - 0.45f, h - 0.25f, 0.02f, edgeGlow);

                // Rails, except over the tunnel mouths, the gap and the outside of the semi-sharp corner.
                bool open = mid > hairpin.x - 10f && mid < hairpin.y + 15f;
                foreach (float side in new[] { -1f, 1f })
                    if (!open || side != openSide) Rail(rails, railLights, a, b, side, railColor, railGlow);
                if (open)
                {
                    // A red and white kerb warns of the open edge.
                    var kerb = Mathf.FloorToInt(mid / 2f) % 2 == 0 ? new Color32(235, 50, 60, 120) : new Color32(240, 240, 240, 60);
                    float edge = openSide * h;
                    Strip(shoulderLights, a, b, Mathf.Min(edge, edge - openSide * 1.2f), Mathf.Max(edge, edge - openSide * 1.2f), 0.025f, kerb);
                }
            }

            DecorateBridge(lights);
            DecorateRuins(structures);
            BuildPads(lights);
            BuildStartLine(lights, structures);
            SpaceTrack.AddStars(lights, Centre(), 320, 420f);
            AddPlanet(lights);

            Add("Road", road, solid, true, false);
            shouldersGroup = Add("Shoulders", shoulders, solid, true, false);
            Add("ShoulderSkin", shoulderSkin, solid, false, false).transform.SetParent(shouldersGroup.transform, true);
            Add("ShoulderLights", shoulderLights, glow, false, false).transform.SetParent(shouldersGroup.transform, true);
            Add("Tube", tube, solid, true, true);
            Add("Skin", skin, solid, false, false);
            railsGroup = Add("Rails", rails, solid, true, false);
            Add("RailLights", railLights, glow, false, false).transform.SetParent(railsGroup.transform, true);
            Add("Ruins", structures, solid, false, false);
            Add("Lights", lights, glow, false, false);
            warningGroup = Add("Warnings", warnings, glow, false, false);
            narrowEdgeGroup = Add("NarrowEdge", narrowEdge, glow, false, false);
            SetStage(1);
            PlaceTurrets(race);
        }

        private static Vector2 Outward(Vector2 p)
        {
            Vector2 dir = p - new Vector2(0f, TubeHeight * 0.5f);
            return p + dir.normalized * 0.6f;
        }

        // End face of the deck at the gap: the slab visibly ends, and a short jump hits it.
        private static void Cap(MeshChunkWriter w, Ring r, bool facingForward, Color32 color)
        {
            float h = StarRingLayout.HalfWidth;
            Vector3 a = At(r, -h, 0f), b = At(r, h, 0f), c = At(r, h, -SkinDepth), d = At(r, -h, -SkinDepth);
            if (facingForward) w.AddQuad(b, a, d, c, color); else w.AddQuad(a, b, c, d, color);
        }

        private static void Strip(MeshChunkWriter w, Ring a, Ring b, float x0, float x1, float lift, Color32 color)
        {
            w.AddQuad(At(a, x0, lift), At(b, x0, lift), At(b, x1, lift), At(a, x1, lift), color);
        }

        private static void Rail(MeshChunkWriter solidW, MeshChunkWriter glowW, Ring a, Ring b, float side, Color32 color, Color32 top)
        {
            float inner = side * StarRingLayout.HalfWidth, outer = side * (StarRingLayout.HalfWidth + RailWidth);
            // The inner face looks at the road, the outer face away; the top carries a glowing line.
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
            Strip(glowW, a, b, Mathf.Min(inner, outer), Mathf.Max(inner, outer), RailHeight, top);
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
            float h = StarRingLayout.HalfWidth;
            foreach (float edge in new[] { layout.GapStart - 1.6f, layout.GapEnd + 0.1f })
            {
                Ring a = RingAt(edge), b = RingAt(edge + 1.5f);
                for (int k = 0; k < 9; k++)
                {
                    float x0 = -h + k * 2f;
                    Strip(w, a, b, x0, x0 + 2f, 0.03f, k % 2 == 0 ? yellow : dark);
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

        // The contested pad in the middle of the road before the jump, and a reward strip on the tunnel ceiling.
        private void BuildPads(MeshChunkWriter w)
        {
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
                Arrow(w, RingAt(d - PadLength * 0.5f + 1f + k * 2f), 0f, PadHalfWidth * 0.9f, 0.035f, k % 2 == 0 ? orange : gold, ceiling);
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
            float h = StarRingLayout.HalfWidth;
            Ring a = RingAt(layout.StartLine - 0.6f), line = RingAt(layout.StartLine), b = RingAt(layout.StartLine + 0.6f);
            for (int k = 0; k < 18; k++)
                for (int row = 0; row < 2; row++)
                    Strip(w, row == 0 ? a : line, row == 0 ? line : b, -h + k, -h + k + 1f, 0.03f,
                        (k + row) % 2 == 0 ? new Color32(240, 240, 250, 90) : new Color32(25, 25, 35, 0));
            // A glowing arch over the line.
            var rotation = Quaternion.LookRotation(Vector3.Cross(line.right, line.up), Vector3.up);
            foreach (float side in new[] { -1f, 1f })
                LowPoly.Box(rails, Matrix4x4.TRS(At(line, side * (h + 1.2f), 0f), rotation, Vector3.one), Vector3.up * 3.5f, new Vector3(0.8f, 7f, 0.8f), new Color32(88, 82, 130, 0));
            LowPoly.Box(w, Matrix4x4.TRS(At(line, 0f, 7f), rotation, Vector3.one), Vector3.zero, new Vector3(2f * h + 3.2f, 0.7f, 0.9f), new Color32(120, 80, 255, 255));
        }

        private Vector3 Centre()
        {
            var bounds = new Bounds(layout.Points[0], Vector3.zero);
            foreach (var p in layout.Points) bounds.Encapsulate(p);
            return bounds.center;
        }

        // A shattered ringed planet below the circuit, flat-shaded like the rest of the art.
        private void AddPlanet(MeshChunkWriter w)
        {
            Vector3 c = Centre() + new Vector3(-260f, -150f, 180f);
            const float radius = 90f;
            const int bands = 10;
            for (int i = 0; i < bands; i++)
            {
                float a0 = Mathf.PI * i / bands - Mathf.PI * 0.5f, a1 = Mathf.PI * (i + 1) / bands - Mathf.PI * 0.5f;
                float y0 = Mathf.Sin(a0) * radius, y1 = Mathf.Sin(a1) * radius;
                var tint = i % 3 == 0 ? new Color32(116, 69, 52, 80) : i % 3 == 1 ? new Color32(65, 56, 55, 60) : new Color32(145, 104, 73, 80);
                LowPoly.Cylinder(w, Matrix4x4.identity, c + Vector3.up * y0, Mathf.Cos(a0) * radius, Mathf.Cos(a1) * radius, y1 - y0, 16, tint, false);
            }
            var tilt = Matrix4x4.TRS(c, Quaternion.Euler(18f, 0f, 12f), Vector3.one);
            var ringColor = new Color32(225, 112, 61, 140);
            for (int k = 0; k < 32; k++)
            {
                float a0 = k * Mathf.PI * 2f / 32f, a1 = (k + 1) * Mathf.PI * 2f / 32f;
                if (k % 9 == 2 || k % 9 == 3) continue; // shattered ring
                Vector3 P(float a, float r) => tilt.MultiplyPoint3x4(new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                // Both faces: the ring is seen from above and below.
                w.AddQuad(P(a0, 130f), P(a1, 130f), P(a1, 175f), P(a0, 175f), ringColor);
                w.AddQuad(P(a0, 175f), P(a1, 175f), P(a1, 130f), P(a0, 130f), ringColor);
            }
        }

        // Rusted gantries, collapsed hulls and stranded pylons beyond the drivable edge.
        private void DecorateRuins(MeshChunkWriter w)
        {
            var rust = new Color32(104, 65, 48, 0);
            var dark = new Color32(39, 38, 39, 0);
            var pale = new Color32(124, 110, 89, 0);
            foreach (float d in new[] { 115f, layout.TunnelEnd + 35f, layout.Hairpin.y + 20f, layout.GapEnd + 55f, layout.RuinTunnelEnd + 25f })
            {
                Ring r = RingAt(d);
                Quaternion rot = Quaternion.LookRotation(Vector3.Cross(r.right, r.up), Vector3.up);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 basePoint = At(r, side * (StarRingLayout.HalfWidth + 13f), -2f);
                    LowPoly.Box(w, Matrix4x4.TRS(basePoint, rot * Quaternion.Euler(0f, side * 18f, side * 9f), Vector3.one),
                        Vector3.up * 2.5f, new Vector3(2.5f, 5f, 6f), rust);
                    LowPoly.Box(w, Matrix4x4.TRS(basePoint + Vector3.up * 4f, rot, Vector3.one),
                        Vector3.zero, new Vector3(3f, 0.35f, 7f), pale);
                    LowPoly.Box(w, Matrix4x4.TRS(basePoint + Vector3.up * 6.4f, rot * Quaternion.Euler(0f, 0f, side * 24f), Vector3.one),
                        Vector3.zero, new Vector3(0.35f, 4f, 0.5f), dark);
                }
            }
        }

        public void Follow(RaceManager race)
        {
            foreach (var progress in observed) if (progress != null) progress.LapCompleted -= OnLapCompleted;
            observed.Clear();
            if (race == null) return;
            foreach (var participant in race.Participants)
                if (participant != null && participant.Progress != null)
                {
                    observed.Add(participant.Progress);
                    participant.Progress.LapCompleted += OnLapCompleted;
                }
        }

        private void OnLapCompleted(int completed, double seconds) => SetStage(completed + 1);

        public void SetStage(int stage)
        {
            if (stage <= Stage) return;
            Stage = Mathf.Clamp(stage, 1, 3);
            railsGroup.SetActive(Stage == 1);
            shouldersGroup.SetActive(Stage < 3);
            warningGroup.SetActive(Stage == 2);
            narrowEdgeGroup.SetActive(Stage == 3);
            path.halfWidth = Stage < 3 ? StarRingLayout.HalfWidth : NarrowHalfWidth;
        }

        private void PlaceTurrets(RaceManager race)
        {
            // (distance, side): +1 right of the direction of travel. Beside the road, never over it.
            var spots = new List<Vector2>
            {
                new Vector2(Mid("sweep1"), -1f),
                new Vector2(layout.TunnelEnd + 20f, 1f),
                new Vector2(Mid("hairpin"), -StarRingLayout.HairpinOutside),
                new Vector2(layout.GapEnd + 22f, -1f),
                new Vector2(Mid("west"), 1f),
            };
            for (int i = 0; i < spots.Count; i++)
            {
                Ring r = RingAt(spots[i].x);
                float side = spots[i].y;
                Vector3 position = At(r, side * (StarRingLayout.HalfWidth + 4f), 3f);
                turrets.Add(SpaceTurret.Create(transform, position, -r.right * side, i, race, solid, glow, shells));
            }
        }

        private float Mid(string name) { var r = layout.Range(name); return (r.x + r.y) * 0.5f; }

        private GameObject Add(string groupName, MeshChunkWriter writer, Material material, bool collide, bool gravity)
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
            return group;
        }

        public void Restore()
        {
            Follow(null);
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
            foreach (var m in new[] { glow, shells, ownedSolid })
                if (m != null) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
        }
    }
}
