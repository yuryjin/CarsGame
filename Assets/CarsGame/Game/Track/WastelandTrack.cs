using System.Collections.Generic;
using UnityEngine;
using RacingGame.Core.Track;
using RacingGame.World;
using RacingGame.World.Gen;

namespace RacingGame
{
    /// <summary>
    /// A wide post-apocalyptic circuit above a ruined city, generated from <see cref="WastelandLayout"/> and driven through
    /// the scene's <see cref="TrackPath"/> (AI, laps, respawn and the grid follow it). A broken bridge to jump, a boost
    /// pad in the middle of the road before it, three tunnels whose walls and ceiling can be driven at speed
    /// (<see cref="GravitySurface"/>), one semi-sharp corner with an open edge.
    /// The road decays with the race: once the leader completes lap 1 the rails retract and the outer strips glow red,
    /// after lap 2 those strips break off and fall, leaving ±<see cref="WastelandLayout.NarrowHalfWidth"/> m.
    /// </summary>
    public sealed class WastelandTrack : MonoBehaviour
    {
        public const float RailHeight = 1f, RailWidth = 0.5f, SkinDepth = 1.2f;
        public const float PadHalfWidth = 2f, PadLength = 6f, PadRadius = 2.2f;
        /// <summary>Tunnel cross-section: flat floor ±<see cref="TubeFloor"/>, fillets of <see cref="TubeFillet"/>, walls to the road edge.</summary>
        public const float TubeFloor = 5f, TubeFillet = 4f, TubeWallTop = 7f, TubeHeight = 11f;
        /// <summary>Ruins stand on the dust this far below the base of the road.</summary>
        public const float GroundY = -35f;
        /// <summary>Road length of one breakable edge piece (each side is its own piece).</summary>
        public const float EdgeChunk = 40f;
        /// <summary>Rails sink into the deck over this many seconds.</summary>
        public const float RailSinkSeconds = 1.2f;
        /// <summary>Warning between "the road narrows" and the edges losing their colliders; pieces then fall over ~1 s.</summary>
        public const float CollapseDelay = 2.5f, CollapseSpread = 0.9f;
        private const int FilletSteps = 6;

        private static readonly Color32 RailsOffColor = new Color32(255, 170, 60, 255);
        private static readonly Color32 NarrowColor = new Color32(255, 70, 60, 255);

        private TrackPath path;
        private WastelandLayout layout;
        private Material solid, glow, warn, ownedSolid;
        private readonly List<GameObject> hidden = new List<GameObject>();
        private readonly List<Renderer> hiddenMarkers = new List<Renderer>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<BoostGate> pads = new List<BoostGate>();

        private sealed class EdgePiece
        {
            public MeshChunkWriter road, decor, glow, warn;
            public GameObject root, warnGroup;
            public readonly List<Collider> colliders = new List<Collider>();
            public Vector3 centre, axis;
            public float delay, speed, spin, fallen;
            public bool falling;
        }

        private readonly Dictionary<int, EdgePiece> edgeMap = new Dictionary<int, EdgePiece>();
        private readonly List<EdgePiece> edges = new List<EdgePiece>();
        private GameObject barriers, narrowLights;
        private readonly List<Collider> barrierColliders = new List<Collider>();
        private Transform ash;
        private RaceManager race;
        private bool raceSearched;
        private float railTime, collapseTime;

        public WastelandLayout Layout => layout;
        public IReadOnlyList<BoostGate> Pads => pads;
        /// <summary>0 = rails up, 1 = rails gone and the edges glow, 2 = the edges are breaking off / gone.</summary>
        public int Stage { get; private set; }
        /// <summary>True once every edge piece has lost its collider.</summary>
        public bool EdgesGone { get; private set; }
        public int EdgePieceCount => edges.Count;

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
            map.warn = new Material(map.glow) { name = "WastelandWarn" };
            AbyssTrack.HideScenery(map.hidden, AbyssTrack.Scenery);
            AbyssTrack.HideScenery(map.hidden, new[] { "Environment/Mountains" });
            // The path's marker dashes lie on the ground circuit; the TrackPath itself must stay active.
            foreach (var r in path.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; map.hiddenMarkers.Add(r); }

            map.Build();
            return map;
        }

        // ---------- race stages ----------

        private void Update()
        {
            if (!raceSearched) { race = FindAnyObjectByType<RaceManager>(); raceSearched = true; }
            if (race != null && (race.State == RaceState.Racing || race.State == RaceState.Finishing))
            {
                int leader = 0;
                foreach (var p in race.Participants)
                    if (p != null && p.Progress != null) leader = Mathf.Max(leader, p.Progress.LapsCompleted);
                SetStage(WastelandLayout.StageFor(leader), true);
            }
            Tick(Time.deltaTime);
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (ash != null && cam != null) ash.position = cam.transform.position + Vector3.up * 14f + cam.transform.forward * 20f;
        }

        /// <summary>Advance to a stage (never back). Public so tests can drive it without racing laps.</summary>
        public void SetStage(int stage, bool announce = false)
        {
            while (Stage < Mathf.Min(stage, 2))
            {
                Stage++;
                if (Stage == 1) RetractRails(announce);
                else BeginCollapse(announce);
            }
        }

        private void RetractRails(bool announce)
        {
            railTime = 0f;
            foreach (var c in barrierColliders) if (c != null) c.enabled = false;
            foreach (var piece in edges) if (piece.warnGroup != null) piece.warnGroup.SetActive(true);
            if (announce) Announce("hud.wasteland.rails_off", RailsOffColor, "crash_light", 0.8f);
        }

        private void BeginCollapse(bool announce)
        {
            collapseTime = 0f;
            // The AI, pickups and respawn keep to the surviving road from the warning on.
            path.halfWidth = WastelandLayout.NarrowHalfWidth;
            if (narrowLights != null) narrowLights.SetActive(true);
            if (announce) Announce("hud.wasteland.narrowing", NarrowColor, "crash_heavy", 0.55f);
        }

        private static void Announce(string key, Color32 color, string sfx, float pitch)
        {
            var hud = FindAnyObjectByType<HudScreen>();
            if (hud != null) hud.ShowEvent(Loc.Get(key), color);
            var player = SfxPlayer.Current;
            if (player != null) player.PlayUi(SfxLibrary.Get(sfx), 0.9f, pitch);
        }

        /// <summary>Animations and the timed collider switch-off. Called every frame; tests call it directly.</summary>
        public void Tick(float dt)
        {
            if (Stage >= 1 && barriers != null && barriers.activeSelf)
            {
                railTime += dt;
                float t = Mathf.Clamp01(railTime / RailSinkSeconds);
                barriers.transform.localPosition = Vector3.down * ((RailHeight + 0.4f) * t * t);
                if (t >= 1f) barriers.SetActive(false);
            }
            if (Stage == 1) warn.SetFloat("_EmissionBoost", 0.55f + 0.3f * Mathf.Sin(Time.time * 3f));   // a hint, not an alarm
            if (Stage < 2) return;

            collapseTime += dt;
            if (collapseTime < CollapseDelay)
            {
                // Faster, brighter pulse: the edges are about to go.
                warn.SetFloat("_EmissionBoost", 2.2f + 1.8f * Mathf.Sin(Time.time * 14f));
                return;
            }
            warn.SetFloat("_EmissionBoost", 3f);
            bool allGone = true;
            foreach (var piece in edges)
            {
                if (piece.root == null || !piece.root.activeSelf) continue;
                if (!piece.falling)
                {
                    if (collapseTime < CollapseDelay + piece.delay) { allGone = false; continue; }
                    piece.falling = true;
                    foreach (var c in piece.colliders) if (c != null) c.enabled = false;
                }
                piece.speed += 16f * dt;
                float step = piece.speed * dt;
                piece.fallen += step;
                Vector3 centre = piece.centre + Vector3.down * piece.fallen;
                piece.root.transform.position += Vector3.down * step;
                piece.root.transform.RotateAround(centre, piece.axis, piece.spin * dt);
                if (piece.fallen > 70f) piece.root.SetActive(false);
            }
            EdgesGone = allGone;
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
        private static Vector3 Forward(Ring r) => Vector3.Cross(r.right, r.up);

        /// <summary>Ring distances: every ~2 m plus the exact edges of the gap and the tunnel phases.</summary>
        private List<float> RingDistances()
        {
            int n = Mathf.CeilToInt(layout.Length / WastelandLayout.Step);
            var list = new List<float>(n + 32);
            for (int i = 0; i < n; i++) list.Add(i * layout.Length / n);
            float f = WastelandLayout.TunnelFlare;
            var exact = new List<float> { layout.GapStart, layout.GapEnd };
            foreach (var t in layout.Tunnels) exact.AddRange(new[] { t.x, t.x + f, t.x + 2f * f, t.y - 2f * f, t.y - f, t.y });
            foreach (float d in exact)
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

        // Tube panels, alternating stripe and hoop colour per tunnel: rusted steel, sodium-lit sewer, toxic concrete.
        private static readonly Color32[] TubePanel = { new Color32(122, 92, 66, 0), new Color32(132, 76, 50, 0), new Color32(112, 116, 104, 0) };
        private static readonly Color32[] TubeBase = { new Color32(84, 76, 68, 0), new Color32(92, 62, 46, 0), new Color32(84, 88, 80, 0) };
        private static readonly Color32[] TubeHoop = { new Color32(255, 149, 46, 255), new Color32(255, 206, 80, 255), new Color32(90, 255, 50, 255) };

        /// <summary>The breakable edge piece of one side (-1 left, +1 right) around distance d.</summary>
        private EdgePiece Edge(float d, float side)
        {
            int index = Mathf.FloorToInt(Mathf.Repeat(d, layout.Length) / EdgeChunk) * 2 + (side < 0f ? 0 : 1);
            if (!edgeMap.TryGetValue(index, out var piece))
            {
                piece = new EdgePiece
                {
                    road = new MeshChunkWriter("EdgeRoad" + index), decor = new MeshChunkWriter("EdgeDecor" + index),
                    glow = new MeshChunkWriter("EdgeGlow" + index), warn = new MeshChunkWriter("EdgeWarn" + index),
                };
                float middle = (Mathf.Floor(Mathf.Repeat(d, layout.Length) / EdgeChunk) + 0.5f) * EdgeChunk;
                piece.centre = At(RingAt(middle), side * (WastelandLayout.HalfWidth + WastelandLayout.NarrowHalfWidth) * 0.5f, 0f);
                edgeMap.Add(index, piece);
                edges.Add(piece);
            }
            return piece;
        }

        private void Build()
        {
            BuildTubeStrip();
            var road = new MeshChunkWriter("WastelandRoad");
            var wear = new MeshChunkWriter("WastelandWear");
            var tube = new MeshChunkWriter("WastelandTube");
            var skin = new MeshChunkWriter("WastelandSkin");
            var rails = new MeshChunkWriter("WastelandRails");
            var railGlow = new MeshChunkWriter("WastelandRailGlow");
            var lights = new MeshChunkWriter("WastelandLights");
            var narrow = new MeshChunkWriter("WastelandNarrowLights");

            var asphalt = new Color32(57, 53, 48, 0);
            var band = new Color32(68, 61, 51, 0);
            var underside = new Color32(68, 42, 32, 0);
            var edgeGlow = new Color32(255, 149, 46, 255);
            var railColor = new Color32(112, 63, 39, 0);
            var railTop = new Color32(255, 92, 34, 255);
            var warnRed = new Color32(170, 42, 30, 190);
            var warnDark = new Color32(104, 34, 26, 90);

            var random = new System.Random(4127);
            var distances = RingDistances();
            Vector2 hairpin = layout.Hairpin;
            float h = WastelandLayout.HalfWidth, n = WastelandLayout.NarrowHalfWidth, f = TubeFloor;
            for (int i = 0; i + 1 < distances.Count; i++)
            {
                float d0 = distances[i], d1 = distances[i + 1], mid = (d0 + d1) * 0.5f;
                if (layout.InGap(mid)) continue;
                Ring a = RingAt(d0), b = RingAt(d1);
                bool stripe = Mathf.FloorToInt(mid / 10f) % 2 == 0;
                int tunnel = layout.TunnelIndex(mid);
                if (tunnel >= 0)
                {
                    layout.TunnelShape(d0, out float fa, out float ra);
                    layout.TunnelShape(d1, out float fb, out float rb);
                    for (int j = 0; j + 1 < tubeStrip.Count; j++)
                    {
                        if (!TubePresent(j, ra) || !TubePresent(j + 1, ra) || !TubePresent(j, rb) || !TubePresent(j + 1, rb)) continue;
                        Vector2 pa = TubePoint(j, fa), qa = TubePoint(j + 1, fa), pb = TubePoint(j, fb), qb = TubePoint(j + 1, fb);
                        tube.AddQuad(At(a, pa.x, pa.y), At(b, pb.x, pb.y), At(b, qb.x, qb.y), At(a, qa.x, qa.y), stripe ? TubePanel[tunnel] : TubeBase[tunnel]);
                        // Outer shell, facing out, so the tube is solid from outside too.
                        Vector2 oa = Outward(pa), ob = Outward(pb), oq = Outward(qb), op = Outward(qa);
                        skin.AddQuad(At(a, op.x, op.y), At(b, oq.x, oq.y), At(b, ob.x, ob.y), At(a, oa.x, oa.y), underside);
                    }
                    // Glowing hoops every 8 m, the last 25 m red: time to come down before the exit.
                    var hoop = layout.Tunnels[tunnel].y - mid < 25f ? new Color32(255, 70, 70, 255) : TubeHoop[tunnel];
                    if (ra >= 1f && rb >= 1f && Mathf.Repeat(mid, 8f) < d1 - d0) Hoop(lights, a, b, hoop);
                    // Guide lines along both walls and the ceiling show the way round the tube.
                    if (ra >= 1f && rb >= 1f) GuideLines(lights, a, b, new Color32(hoop.r, hoop.g, hoop.b, 170));
                    continue;
                }

                // Surviving deck in four panels (±TubeFloor meets the tunnel strip's floor points) ...
                float[] xs = { -n, -f, 0f, f, n };
                for (int k = 0; k + 1 < xs.Length; k++)
                    road.AddQuad(At(a, xs[k], 0f), At(b, xs[k], 0f), At(b, xs[k + 1], 0f), At(a, xs[k + 1], 0f), stripe ? asphalt : band);
                // ... its slab sides at ±NarrowHalfWidth (hidden under the edges until they fall) and underside.
                skin.AddQuad(At(a, -n, 0f), At(a, -n, -SkinDepth), At(b, -n, -SkinDepth), At(b, -n, 0f), underside);
                skin.AddQuad(At(b, n, 0f), At(b, n, -SkinDepth), At(a, n, -SkinDepth), At(a, n, 0f), underside);
                skin.AddQuad(At(a, n, -SkinDepth), At(b, n, -SkinDepth), At(b, -n, -SkinDepth), At(a, -n, -SkinDepth), underside);
                bool capStart = Mathf.Abs(d1 - layout.GapStart) < 0.01f, capEnd = Mathf.Abs(d0 - layout.GapEnd) < 0.01f;
                if (capStart) Cap(skin, b, -n, n, true, underside);
                if (capEnd) Cap(skin, a, -n, n, false, underside);
                Strip(narrow, a, b, -n + 0.25f, -n + 0.45f, 0.02f, edgeGlow);
                Strip(narrow, a, b, n - 0.45f, n - 0.25f, 0.02f, edgeGlow);
                RoadWear(wear, a, b, mid, random);

                // The breakable outer strips, one piece per side per EdgeChunk metres.
                foreach (float side in new[] { -1f, 1f })
                {
                    var piece = Edge(mid, side);
                    float x0 = side < 0f ? -h : n, x1 = side < 0f ? -n : h;
                    piece.road.AddQuad(At(a, x0, 0f), At(b, x0, 0f), At(b, x1, 0f), At(a, x1, 0f), stripe ? asphalt : band);
                    if (side < 0f) piece.decor.AddQuad(At(a, -h, 0f), At(a, -h, -SkinDepth), At(b, -h, -SkinDepth), At(b, -h, 0f), underside);
                    else piece.decor.AddQuad(At(b, h, 0f), At(b, h, -SkinDepth), At(a, h, -SkinDepth), At(a, h, 0f), underside);
                    piece.decor.AddQuad(At(a, x1, -SkinDepth), At(b, x1, -SkinDepth), At(b, x0, -SkinDepth), At(a, x0, -SkinDepth), underside);
                    if (capStart) Cap(piece.decor, b, x0, x1, true, underside);
                    if (capEnd) Cap(piece.decor, a, x0, x1, false, underside);
                    // Edge lights, and the red warning the strip wears once the rails are gone.
                    if (side < 0f) Strip(piece.glow, a, b, -h + 0.25f, -h + 0.45f, 0.02f, edgeGlow);
                    else Strip(piece.glow, a, b, h - 0.45f, h - 0.25f, 0.02f, edgeGlow);
                    var red = Mathf.FloorToInt(mid / 3f) % 2 == 0 ? warnRed : warnDark;
                    if (side < 0f) Strip(piece.warn, a, b, -h + 0.55f, -n - 0.05f, 0.016f, red);
                    else Strip(piece.warn, a, b, n + 0.05f, h - 0.55f, 0.016f, red);
                }

                // Rails, except over the tunnel mouths, the gap and the outside of the semi-sharp corner.
                bool openLeft = mid > hairpin.x - 10f && mid < hairpin.y + 15f;   // right-hand corner: the outside is the left
                if (!openLeft) Rail(rails, railGlow, a, b, -1f, railColor, railTop);
                Rail(rails, railGlow, a, b, 1f, railColor, railTop);
                if (openLeft)
                {
                    // Red and white kerb warns of the open edge.
                    var kerb = Mathf.FloorToInt(mid / 2f) % 2 == 0 ? new Color32(235, 50, 60, 120) : new Color32(240, 240, 240, 60);
                    Strip(Edge(mid, -1f).glow, a, b, -h, -h + 1.2f, 0.025f, kerb);
                }
            }

            DecorateBridge(lights, skin);
            BuildPads(lights);
            BuildStartLine(lights);
            BuildPortals(skin, lights);
            BuildLamps(random);
            BuildWasteland();

            Add("Road", road, solid, true, false);
            Add("Wear", wear, solid, false, false);
            Add("Tube", tube, solid, true, true);
            Add("Skin", skin, solid, false, false);
            Add("Lights", lights, glow, false, false);
            barriers = new GameObject("Barriers");
            barriers.transform.SetParent(transform, false);
            barrierColliders.AddRange(Add("Rails", rails, solid, true, false, barriers.transform).GetComponentsInChildren<Collider>());
            Add("RailGlow", railGlow, glow, false, false, barriers.transform);
            narrowLights = Add("NarrowLights", narrow, glow, false, false);
            narrowLights.SetActive(false);

            var edgeRoot = new GameObject("Edges").transform;
            edgeRoot.SetParent(transform, false);
            var spin = new System.Random(88);
            foreach (var piece in edges)
            {
                piece.root = new GameObject("Edge");
                piece.root.transform.SetParent(edgeRoot, false);
                piece.colliders.AddRange(Add("Road", piece.road, solid, true, false, piece.root.transform).GetComponentsInChildren<Collider>());
                Add("Decor", piece.decor, solid, false, false, piece.root.transform);
                Add("Glow", piece.glow, glow, false, false, piece.root.transform);
                piece.warnGroup = Add("Warn", piece.warn, warn, false, false, piece.root.transform);
                piece.warnGroup.SetActive(false);
                piece.delay = (float)spin.NextDouble() * CollapseSpread;
                piece.axis = Quaternion.Euler(0f, (float)spin.NextDouble() * 360f, 0f) * Vector3.forward;
                piece.spin = 6f + (float)spin.NextDouble() * 10f;
                piece.road = piece.decor = piece.glow = piece.warn = null;
            }
        }

        private static Vector2 Outward(Vector2 p)
        {
            Vector2 dir = p - new Vector2(0f, TubeHeight * 0.5f);
            return p + dir.normalized * 0.6f;
        }

        // End face of the deck at the gap: the slab visibly ends, and a short jump hits it.
        private static void Cap(MeshChunkWriter w, Ring r, float x0, float x1, bool facingForward, Color32 color)
        {
            Vector3 a = At(r, x0, 0f), b = At(r, x1, 0f), c = At(r, x1, -SkinDepth), d = At(r, x0, -SkinDepth);
            if (facingForward) w.AddQuad(b, a, d, c, color); else w.AddQuad(a, b, c, d, color);
        }

        private static void Strip(MeshChunkWriter w, Ring a, Ring b, float x0, float x1, float lift, Color32 color)
        {
            w.AddQuad(At(a, x0, lift), At(b, x0, lift), At(b, x1, lift), At(a, x1, lift), color);
        }

        /// <summary>A strip cell on the surviving deck or, if it lies on an outer strip, on that side's edge piece.</summary>
        private void DeckStrip(MeshChunkWriter inner, Ring a, Ring b, float x0, float x1, float lift, Color32 color)
        {
            float centre = (x0 + x1) * 0.5f;
            var w = Mathf.Abs(centre) > WastelandLayout.NarrowHalfWidth ? Edge((a.d + b.d) * 0.5f, Mathf.Sign(centre)).glow : inner;
            Strip(w, a, b, x0, x1, lift, color);
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

        private static void GuideLines(MeshChunkWriter w, Ring a, Ring b, Color32 color)
        {
            float wall = TubeFloor + TubeFillet - 0.04f, y = (TubeFillet + TubeWallTop) * 0.5f, ceiling = TubeHeight - 0.04f;
            // Walls: vertical bands facing the tube axis.
            w.AddQuad(At(a, -wall, y - 0.15f), At(a, -wall, y + 0.15f), At(b, -wall, y + 0.15f), At(b, -wall, y - 0.15f), color);
            w.AddQuad(At(b, wall, y - 0.15f), At(b, wall, y + 0.15f), At(a, wall, y + 0.15f), At(a, wall, y - 0.15f), color);
            // Ceiling: two bands facing down.
            foreach (float x in new[] { -2.5f, 2.5f })
                w.AddQuad(At(a, x + 0.15f, ceiling), At(b, x + 0.15f, ceiling), At(b, x - 0.15f, ceiling), At(a, x - 0.15f, ceiling), color);
        }

        private static Vector2 Inward(Vector2 p) => p + (new Vector2(0f, TubeHeight * 0.5f) - p).normalized * 0.04f;

        // Cracks, tar patches and drifted sand on the surviving deck: flat, no collider, never inside a tunnel.
        private static void RoadWear(MeshChunkWriter w, Ring a, Ring b, float mid, System.Random random)
        {
            const float lift = 0.012f;
            float n = WastelandLayout.NarrowHalfWidth;
            int roll = random.Next(100);
            if (roll < 12)
            {
                float x = -5.5f + (float)random.NextDouble() * 11f, x2 = x + ((float)random.NextDouble() - 0.5f) * 1.6f;
                Vector3 p0 = At(a, x, lift), p1 = At(b, x2, lift), side = a.right * 0.07f;
                w.AddQuad(p0 - side, p1 - side, p1 + side, p0 + side, new Color32(24, 22, 20, 0));
            }
            else if (roll < 16)
            {
                float x = -5f + (float)random.NextDouble() * 8f, width = 1.2f + (float)random.NextDouble() * 2.2f;
                Strip(w, a, b, x, x + width, lift, new Color32(38, 35, 33, 0));
            }
            // Sand blown against the inner line of the doomed strips, thicker in places.
            float drift = 0.4f + 0.9f * Mathf.PerlinNoise(mid * 0.05f, 3.1f);
            var sand = new Color32(134, 104, 70, 0);
            Strip(w, a, b, -n, -n + drift, lift, sand);
            drift = 0.4f + 0.9f * Mathf.PerlinNoise(mid * 0.05f, 7.7f);
            Strip(w, a, b, n - drift, n, lift, sand);
        }

        // Hazard stripes at the lip and the landing edge, arrows on the run-up, snapped rebar in the broken ends.
        private void DecorateBridge(MeshChunkWriter w, MeshChunkWriter skin)
        {
            var yellow = new Color32(255, 200, 40, 255);
            var dark = new Color32(30, 28, 40, 0);
            float h = WastelandLayout.HalfWidth;
            foreach (float edge in new[] { layout.GapStart - 1.6f, layout.GapEnd + 0.1f })
            {
                Ring a = RingAt(edge), b = RingAt(edge + 1.5f);
                for (int k = 0; k < 20; k++)
                {
                    float x0 = -h + k, x1 = x0 + 1f;
                    DeckStrip(w, a, b, x0, x1, 0.03f, (k / 2) % 2 == 0 ? yellow : dark);
                }
            }
            for (int k = 1; k <= 3; k++) Arrow(w, RingAt(layout.GapStart - 10f - k * 9f), 0f, 1.6f, 0.03f, yellow, false);

            var rebar = new Color32(70, 40, 28, 0);
            var random = new System.Random(311);
            foreach (bool start in new[] { true, false })
            {
                Ring r = RingAt(start ? layout.GapStart : layout.GapEnd);
                Vector3 fwd = Forward(r) * (start ? 1f : -1f);
                for (float x = -WastelandLayout.NarrowHalfWidth + 0.6f; x < WastelandLayout.NarrowHalfWidth; x += 1.3f)
                {
                    float length = 0.8f + (float)random.NextDouble() * 1.6f, droop = (float)random.NextDouble() * 35f;
                    var rotation = Quaternion.LookRotation(Quaternion.AngleAxis(droop, r.right * (start ? 1f : -1f)) * fwd, Vector3.up);
                    Vector3 root = At(r, x, -0.35f - (float)random.NextDouble() * 0.6f);
                    LowPoly.Box(skin, Matrix4x4.TRS(root, rotation, Vector3.one), Vector3.forward * (length * 0.5f), new Vector3(0.1f, 0.1f, length), rebar);
                }
            }
        }

        private static void Arrow(MeshChunkWriter w, Ring r, float x, float size, float lift, Color32 color, bool ceiling)
        {
            Vector3 fwd = Forward(r);
            Vector3 c = ceiling ? At(r, x, TubeHeight - lift) : At(r, x, lift);
            Vector3 tip = c + fwd * size, left = c - r.right * size - fwd * size * 0.3f, right = c + r.right * size - fwd * size * 0.3f;
            Vector3 notch = c + fwd * size * 0.1f;
            if (ceiling) { w.AddTriangle(left, notch, tip, color); w.AddTriangle(notch, right, tip, color); }
            else { w.AddTriangle(left, tip, notch, color); w.AddTriangle(notch, tip, right, color); }
        }

        private void BuildPads(MeshChunkWriter w)
        {
            // The contested pad in the middle of the road before the jump, and reward strips on the ceilings of the
            // two straight tunnels (the S-bend tube has none: its walls are the reward).
            AddPad(w, layout.BoostPad, false);
            for (int t = 0; t < 2; t++) AddPad(w, (layout.Tunnels[t].x + layout.Tunnels[t].y) * 0.5f, true);
        }

        private void AddPad(MeshChunkWriter w, float d, bool ceiling)
        {
            Ring r = RingAt(d);
            Vector3 fwd = Forward(r);
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
            gate.ceiling = ceiling;
            pads.Add(gate);
        }

        private void BuildStartLine(MeshChunkWriter w)
        {
            float h = WastelandLayout.HalfWidth;
            Ring a = RingAt(layout.StartLine - 0.6f), s = RingAt(layout.StartLine), b = RingAt(layout.StartLine + 0.6f);
            for (int k = 0; k < 20; k++)
                for (int row = 0; row < 2; row++)
                    DeckStrip(w, row == 0 ? a : s, row == 0 ? s : b, -h + k, -h + k + 1f, 0.03f,
                        (k + row) % 2 == 0 ? new Color32(240, 240, 250, 90) : new Color32(25, 25, 35, 0));
            // A glowing gantry over the line on scrap columns that reach down to the dust.
            var arch = new MeshChunkWriter("WastelandArch");
            var rotation = Quaternion.LookRotation(Forward(s), Vector3.up);
            var rust = new Color32(112, 63, 39, 0);
            foreach (float side in new[] { -1f, 1f })
            {
                LowPoly.Box(arch, Matrix4x4.TRS(At(s, side * (h + 1.2f), 0f), rotation, Vector3.one), Vector3.up * ((7f + GroundY) * 0.5f), new Vector3(0.8f, 7f - GroundY, 0.8f), rust);
                LowPoly.Box(arch, Matrix4x4.TRS(At(s, side * (h + 1.2f), 0f), rotation, Vector3.one), Vector3.up * -4f, new Vector3(1.8f, 0.6f, 1.8f), rust);
            }
            LowPoly.Box(w, Matrix4x4.TRS(At(s, 0f, 7f), rotation, Vector3.one), Vector3.zero, new Vector3(2f * h + 3.2f, 0.7f, 0.9f), new Color32(255, 153, 54, 255));
            // Hanging tyres and a chained skull plate under the gantry.
            var black = new Color32(24, 22, 22, 0);
            for (int k = -2; k <= 2; k++)
                LowPoly.Cylinder(arch, Matrix4x4.TRS(At(s, k * 3.2f, 6.1f), rotation * Quaternion.Euler(0f, 0f, 90f), Vector3.one), Vector3.down * 0.18f, 0.45f, 0.45f, 0.36f, 8, black);
            Add("Arch", arch, solid, false, false);
        }

        // Rusted frames around both mouths of every tube, hazard-striped lintel on top.
        private void BuildPortals(MeshChunkWriter solidW, MeshChunkWriter glowW)
        {
            var concrete = new Color32(92, 84, 74, 0);
            var yellow = new Color32(255, 190, 40, 255);
            var dark = new Color32(28, 26, 26, 0);
            float x = TubeFloor + TubeFillet + 1.3f, top = TubeHeight + 1.6f;
            foreach (var t in layout.Tunnels)
                foreach (float d in new[] { t.x + 2f * WastelandLayout.TunnelFlare, t.y - 2f * WastelandLayout.TunnelFlare })
                {
                    Ring r = RingAt(d);
                    var m = Matrix4x4.TRS(r.centre, Quaternion.LookRotation(Forward(r), Vector3.up), Vector3.one);
                    foreach (float side in new[] { -1f, 1f })
                        LowPoly.Box(solidW, m, new Vector3(side * x, (top + GroundY) * 0.5f, 0f), new Vector3(1.4f, top - GroundY, 1.6f), concrete);
                    LowPoly.Box(solidW, m, new Vector3(0f, top, 0f), new Vector3(2f * x + 1.4f, 1.8f, 1.6f), concrete);
                    for (int k = 0; k < 12; k++)
                    {
                        float x0 = -x + k * (2f * x / 12f);
                        LowPoly.Box(glowW, m, new Vector3(x0 + x / 12f, top, 0f), new Vector3(2f * x / 12f, 0.9f, 1.7f), k % 2 == 0 ? yellow : dark);
                    }
                }
        }

        // Street lamps on brackets outside the edges, some bent or dead; they fall with their edge piece.
        private void BuildLamps(System.Random random)
        {
            var pole = new Color32(64, 58, 54, 0);
            var lamp = new Color32(255, 180, 90, 255);
            var dead = new Color32(40, 36, 34, 0);
            float h = WastelandLayout.HalfWidth + RailWidth + 0.5f;
            int count = 0;
            for (float d = 20f; d < layout.Length - 10f; d += 34f)
            {
                if (layout.InTunnel(d - 12f) || layout.InTunnel(d + 12f) || (d > layout.GapStart - 20f && d < layout.GapEnd + 20f)) continue;
                if (Mathf.Abs(d - layout.StartLine) < 12f) continue;
                float side = count++ % 2 == 0 ? -1f : 1f;
                var piece = Edge(d, side);
                Ring r = RingAt(d);
                bool broken = random.Next(3) == 0;
                float lean = broken ? 14f + (float)random.NextDouble() * 20f : 0f;
                var rotation = Quaternion.LookRotation(Forward(r), Vector3.up) * Quaternion.Euler(0f, 0f, side * lean);
                var m = Matrix4x4.TRS(At(r, side * h, 0f), rotation, Vector3.one);
                LowPoly.Box(piece.decor, m, new Vector3(0f, 2.9f, 0f), new Vector3(0.3f, 8.2f, 0.3f), pole);
                LowPoly.Box(piece.decor, m, new Vector3(-side * 1.1f, 7f, 0f), new Vector3(2.2f, 0.2f, 0.2f), pole);
                LowPoly.Box(broken ? piece.decor : piece.glow, m, new Vector3(-side * 2.1f, 6.8f, 0f), new Vector3(0.8f, 0.25f, 0.45f), broken ? dead : lamp);
            }
        }

        // ---------- the ruined world below ----------

        private static readonly Color32[] Concrete =
        {
            new Color32(92, 86, 78, 0), new Color32(76, 70, 64, 0), new Color32(104, 90, 74, 0), new Color32(70, 60, 54, 0), new Color32(86, 72, 60, 0),
        };
        private static readonly Color32 Rust = new Color32(105, 58, 37, 0), RustDark = new Color32(78, 44, 30, 0), Soot = new Color32(34, 30, 28, 0);
        private static readonly Color32 Fire = new Color32(255, 128, 32, 255), FireCore = new Color32(255, 220, 90, 255);

        /// <summary>Horizontal distance from p to the nearest point of the road centre line.</summary>
        private float Clearance(Vector3 p)
        {
            float best = float.MaxValue;
            var points = layout.Points;
            for (int i = 0; i < points.Length; i += 2)
            {
                float dx = points[i].x - p.x, dz = points[i].z - p.z;
                best = Mathf.Min(best, dx * dx + dz * dz);
            }
            return Mathf.Sqrt(best);
        }

        // Dust far below the road has no collider: falling off the bridge, the open corner or a broken edge costs a reset.
        private void BuildWasteland()
        {
            var dust = new MeshChunkWriter("WastelandDust");
            var scrap = new MeshChunkWriter("WastelandScrap");
            var embers = new MeshChunkWriter("WastelandEmbers");
            var bounds = new Bounds(layout.Points[0], Vector3.zero);
            foreach (var point in layout.Points) bounds.Encapsulate(point);
            var random = new System.Random(1937);
            float x0 = Mathf.Floor((bounds.min.x - 320f) / 60f) * 60f;
            float x1 = Mathf.Ceil((bounds.max.x + 320f) / 60f) * 60f;
            float z0 = Mathf.Floor((bounds.min.z - 320f) / 60f) * 60f;
            float z1 = Mathf.Ceil((bounds.max.z + 320f) / 60f) * 60f;
            for (float x = x0; x < x1; x += 60f)
                for (float z = z0; z < z1; z += 60f)
                {
                    int roll = random.Next(6);
                    var tint = roll == 0 ? new Color32(88, 60, 42, 0) : roll == 1 ? new Color32(74, 62, 54, 0) : new Color32(105, 72, 47, 0);
                    dust.AddQuad(new Vector3(x, GroundY, z), new Vector3(x, GroundY, z + 60f),
                        new Vector3(x + 60f, GroundY, z + 60f), new Vector3(x + 60f, GroundY, z), tint);
                }
            // Rusted bridge supports stay clear of the drivable width.
            foreach (float d in new[] { layout.GapStart - 6f, layout.GapEnd + 7f })
                foreach (float side in new[] { -1f, 1f })
                {
                    Ring r = RingAt(d);
                    float top = r.centre.y - 1f;
                    Vector3 centre = At(r, side * (WastelandLayout.HalfWidth + 2.5f), (GroundY + top) * 0.5f - r.centre.y);
                    LowPoly.Box(scrap, Matrix4x4.identity, centre, new Vector3(2f, top - GroundY, 2f), new Color32(100, 54, 35, 0));
                }
            // Burnt-out towers close to the road (their tops just above the deck) ...
            for (int i = 0; i < 38; i++)
            {
                float d = (i + 0.5f) * layout.Length / 38f;
                Ring r = RingAt(d);
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 basePoint = At(r, side * (24f + (float)random.NextDouble() * 30f), 0f);
                basePoint.y = GroundY;
                float width = 5f + random.Next(0, 6), depth = 5f + random.Next(0, 6);
                if (Clearance(basePoint) < WastelandLayout.HalfWidth + 6f + Mathf.Max(width, depth) * 0.75f) continue;
                Ruin(scrap, embers, basePoint, width, depth, r.centre.y - GroundY + 4f + (float)random.NextDouble() * 20f, random);
            }
            // ... and a broken skyline further out, a few inside the loop.
            Vector3 middle = bounds.center;
            float reach = Mathf.Max(bounds.extents.x, bounds.extents.z);
            for (int placed = 0, attempt = 0; placed < 34 && attempt < 400; attempt++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                float distance = placed % 5 == 4 ? (float)random.NextDouble() * reach : reach + 50f + (float)random.NextDouble() * 220f;
                var basePoint = new Vector3(middle.x + Mathf.Cos(angle) * distance, GroundY, middle.z + Mathf.Sin(angle) * distance);
                float width = 9f + random.Next(0, 14), depth = 9f + random.Next(0, 14);
                if (Clearance(basePoint) < 45f + Mathf.Max(width, depth)) continue;
                Ruin(scrap, embers, basePoint, width, depth, 40f + (float)random.NextDouble() * 70f, random);
                placed++;
            }
            // Burnt car wrecks and scrap heaps on the dust, a pile under the bridge gap, fires among them.
            var fires = new List<Vector3>();
            for (int placed = 0, attempt = 0; placed < 46 && attempt < 300; attempt++)
            {
                float d = (float)random.NextDouble() * layout.Length;
                Ring r = RingAt(d);
                var p = At(r, (random.Next(2) == 0 ? -1f : 1f) * (8f + (float)random.NextDouble() * 60f), 0f);
                p.y = GroundY;
                Wreck(scrap, p, random);
                if (placed % 4 == 0) { Blaze(scrap, embers, p + new Vector3(3f, 0f, 2f), 2.6f, random); fires.Add(p); }
                placed++;
            }
            Vector3 gap = RingAt((layout.GapStart + layout.GapEnd) * 0.5f).centre;
            gap.y = GroundY;
            for (int k = 0; k < 7; k++) Wreck(scrap, gap + new Vector3(((float)random.NextDouble() - 0.5f) * 14f, k * 0.5f, ((float)random.NextDouble() - 0.5f) * 14f), random);
            Blaze(scrap, embers, gap + Vector3.right * 4f, 4f, random);
            fires.Insert(0, gap);
            // Dead billboards on stilts, readable from the deck.
            for (int k = 0; k < 6; k++)
            {
                Ring r = RingAt((k + 0.3f) * layout.Length / 6f);
                var p = At(r, (k % 2 == 0 ? -1f : 1f) * 30f, 0f);
                if (Clearance(p) < 24f) continue;
                p.y = GroundY;
                Billboard(scrap, embers, p, Quaternion.LookRotation(r.centre - p, Vector3.up), r.centre.y - GroundY + 8f, random);
            }
            Add("Dust", dust, solid, false, false);
            Add("Scrap", scrap, solid, false, false);
            Add("Embers", embers, glow, false, false);
            BuildSmoke(fires);
            BuildAsh();
        }

        private static void Ruin(MeshChunkWriter w, MeshChunkWriter glowW, Vector3 basePoint, float width, float depth, float height, System.Random random)
        {
            var color = Concrete[random.Next(Concrete.Length)];
            float tilt = random.Next(4) == 0 ? ((float)random.NextDouble() - 0.5f) * 14f : 0f;
            var m = Matrix4x4.TRS(basePoint, Quaternion.Euler(tilt, random.Next(0, 90), tilt * 0.5f), Vector3.one);
            LowPoly.Box(w, m, new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), color);
            // Blown-out window rows; now and then one still burns.
            for (float y = 5f; y < height - 3f; y += 3.6f)
            {
                LowPoly.Box(w, m, new Vector3(0f, y, 0f), new Vector3(width + 0.12f, 1.1f, depth + 0.12f), Soot);
                if (random.Next(9) == 0)
                {
                    float along = ((float)random.NextDouble() - 0.5f) * (depth - 2f);
                    LowPoly.Box(glowW, m, new Vector3((random.Next(2) == 0 ? -1f : 1f) * (width * 0.5f + 0.08f), y, along), new Vector3(0.1f, 1f, 1.6f), Fire);
                }
            }
            // Jagged broken top and bare girders.
            for (int k = 0; k < 3; k++)
            {
                var chunk = new Vector3(width * (0.3f + (float)random.NextDouble() * 0.35f), 2f + (float)random.NextDouble() * 6f, depth * (0.3f + (float)random.NextDouble() * 0.35f));
                var at = new Vector3(((float)random.NextDouble() - 0.5f) * (width - chunk.x), height + chunk.y * 0.5f - 0.5f, ((float)random.NextDouble() - 0.5f) * (depth - chunk.z));
                LowPoly.Box(w, m * Matrix4x4.TRS(at, Quaternion.Euler(0f, 0f, ((float)random.NextDouble() - 0.5f) * 20f), Vector3.one), Vector3.zero, chunk, color);
            }
            for (int k = 0; k < 3; k++)
            {
                float length = 3f + (float)random.NextDouble() * 6f;
                var at = new Vector3((k - 1) * width * 0.4f, height, (random.Next(2) == 0 ? -0.45f : 0.45f) * depth);
                var bend = Quaternion.Euler(((float)random.NextDouble() - 0.5f) * 40f, 0f, ((float)random.NextDouble() - 0.5f) * 40f);
                LowPoly.Box(w, m * Matrix4x4.TRS(at, bend, Vector3.one), Vector3.up * (length * 0.5f), new Vector3(0.35f, length, 0.35f), Rust);
            }
        }

        private static void Wreck(MeshChunkWriter w, Vector3 at, System.Random random)
        {
            float roll = random.Next(4) == 0 ? 150f + (float)random.NextDouble() * 60f : ((float)random.NextDouble() - 0.5f) * 30f;
            var m = Matrix4x4.TRS(at, Quaternion.Euler(((float)random.NextDouble() - 0.5f) * 16f, (float)random.NextDouble() * 360f, roll), Vector3.one);
            int tone = random.Next(3);
            var body = tone == 0 ? Rust : tone == 1 ? RustDark : new Color32(66, 60, 56, 0);
            LowPoly.Box(w, m, new Vector3(0f, 0.8f, 0f), new Vector3(2f, 0.9f, 4.4f), body);
            LowPoly.Box(w, m, new Vector3(0f, 1.55f, -0.3f), new Vector3(1.7f, 0.65f, 2f), MeshChunkWriter.Scale(body, 0.8f));
            for (int k = 0; k < 4; k++)
            {
                if (random.Next(4) == 0) continue;   // a wheel is gone
                var wheel = m * Matrix4x4.TRS(new Vector3(k < 2 ? -1f : 1f, 0.4f, k % 2 == 0 ? -1.4f : 1.4f), Quaternion.Euler(0f, 0f, 90f), Vector3.one);
                LowPoly.Cylinder(w, wheel, Vector3.down * 0.15f, 0.42f, 0.42f, 0.3f, 7, Soot);
            }
        }

        private static void Blaze(MeshChunkWriter w, MeshChunkWriter glowW, Vector3 at, float scale, System.Random random)
        {
            var m = Matrix4x4.TRS(at, Quaternion.Euler(0f, random.Next(0, 360), 0f), Vector3.one * scale);
            for (int k = 0; k < 3; k++)
            {
                var barrel = new Vector3((k - 1) * 0.9f, 0f, (k % 2) * 0.8f);
                LowPoly.Cylinder(w, m, barrel, 0.4f, 0.4f, 1.1f, 8, k == 1 ? Rust : RustDark);
                LowPoly.Pyramid(glowW, m, barrel + Vector3.up * 1.1f, new Vector2(0.7f, 0.7f), 1.2f + (float)random.NextDouble(), Vector2.zero, Fire);
                LowPoly.Pyramid(glowW, m, barrel + Vector3.up * 1.1f, new Vector2(0.35f, 0.35f), 0.7f, Vector2.zero, FireCore);
            }
        }

        private static void Billboard(MeshChunkWriter w, MeshChunkWriter glowW, Vector3 at, Quaternion facing, float height, System.Random random)
        {
            var m = Matrix4x4.TRS(at, facing * Quaternion.Euler(0f, 0f, ((float)random.NextDouble() - 0.5f) * 10f), Vector3.one);
            foreach (float side in new[] { -4f, 4f })
                LowPoly.Box(w, m, new Vector3(side, height * 0.5f, 0f), new Vector3(0.5f, height, 0.5f), Rust);
            var panel = new Color32(150, 112, 72, 0);
            LowPoly.Box(w, m * Matrix4x4.TRS(new Vector3(0f, height + 2.5f, 0f), Quaternion.Euler(0f, 0f, ((float)random.NextDouble() - 0.5f) * 12f), Vector3.one),
                Vector3.zero, new Vector3(11f, 5f, 0.3f), panel);
            // A faded warning stripe and one flickering-looking tube still lit.
            LowPoly.Box(w, m, new Vector3(0f, height + 1.2f, -0.2f), new Vector3(10.4f, 0.9f, 0.1f), new Color32(120, 30, 22, 0));
            LowPoly.Box(glowW, m, new Vector3(-2f, height + 3.8f, -0.2f), new Vector3(4f, 0.25f, 0.1f), new Color32(255, 60, 40, 255));
        }

        // Slow dark columns over the bigger fires: particle billboards with the shared dust material (skipped without it).
        private void BuildSmoke(List<Vector3> fires)
        {
            var material = WorldAssets.Instance != null ? WorldAssets.Instance.dustMaterial : null;
            if (material == null) return;
            for (int k = 0; k < fires.Count && k < 6; k++)
            {
                var go = new GameObject("Smoke");
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(fires[k] + Vector3.up * 2f, Quaternion.Euler(-90f, 0f, 0f));
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 14f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                main.startSize = new ParticleSystem.MinMaxCurve(7f, 12f);
                main.startColor = new Color(0.3f, 0.26f, 0.24f, 0.6f);
                main.maxParticles = 32;
                main.prewarm = true;
                var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 8f; shape.radius = 2f;
                var emission = ps.emission; emission.rateOverTime = 2.6f;
                var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(1.2f); velocity.y = new ParticleSystem.MinMaxCurve(0f); velocity.z = new ParticleSystem.MinMaxCurve(0.6f);
                var col = ps.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.85f, 0.15f), new GradientAlphaKey(0f, 1f) });
                col.color = g;
                var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 3f));
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                ps.Play();
            }
        }

        // Ash drifting down around the camera (the emitter follows it in LateUpdate).
        private void BuildAsh()
        {
            var material = WorldAssets.Instance != null ? WorldAssets.Instance.dustMaterial : null;
            if (material == null) return;
            var go = new GameObject("Ash");
            go.transform.SetParent(transform, false);
            ash = go.transform;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 7f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.76f, 0.7f, 0.85f), new Color(0.35f, 0.32f, 0.3f, 0.85f));
            main.gravityModifier = 0.04f;
            main.maxParticles = 350;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(90f, 2f, 90f);
            var emission = ps.emission; emission.rateOverTime = 50f;
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.3f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            ps.Play();
        }

        private GameObject Add(string groupName, MeshChunkWriter writer, Material material, bool collide, bool gravity, Transform parent = null)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(parent != null ? parent : transform, false);
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
            foreach (var m in new[] { glow, warn, ownedSolid })
                if (m != null) { if (Application.isPlaying) Destroy(m); else DestroyImmediate(m); }
        }
    }
}
