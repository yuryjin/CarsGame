using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Mission layer on top of the race HUD: the fortress's core bar with its phase notches, how far it still has to
    /// crawl, the enemy count, a marker that points at the fortress, small bars over hurt enemies and a hit marker for
    /// the player's hits on units. Lives inside the HUD canvas (shown and hidden with it); hides the lap/position card.
    /// </summary>
    public sealed class MissionHud : MonoBehaviour
    {
        private const int MaxBars = 10;
        private MissionDirector mission;
        private HudScreen hud;
        private RectTransform root, bossMarker, distancePanel, bossPanel, coreTrack, routeTrack;
        private GameObject raceStatus;
        private Button commandButton;
        private TextMeshProUGUI commandLabel;
        private RectTransform squadMap;
        private Image coreFill, routeFill;
        private TextMeshProUGUI bossName, corePct, routeText, countText, markerText, hitText;
        private readonly RectTransform[] bars = new RectTransform[MaxBars];
        private readonly Image[] barFills = new Image[MaxBars];
        private float hitUntil, nextSlowRefresh;
        private Sprite white;
        private int layoutWidth, layoutHeight;
        private float layoutScale;
        private bool layoutTouch;
        private readonly List<RectTransform> markerBlockers = new List<RectTransform>();
        private readonly List<Rect> occupied = new List<Rect>();
        private readonly Vector3[] corners = new Vector3[4];
        private static readonly HashSet<string> MarkerBlockerNames = new HashSet<string>
        {
            "BossPanel", "LiveGoals", "Speedometer", "Health", "AmmoPanel", "SecondaryPanel",
            "StatusPanel", "TimerPanel", "PauseButton", "HintsPanel", "EventPanel",
            "Left", "Right", "Fire", "Brake", "SwapWeapon", "Secondary", "Reset"
        };

        public static MissionHud Create(MissionDirector mission, HudScreen hud)
        {
            Canvas canvas = null;
            if (hud != null) foreach (var c in hud.GetComponentsInChildren<Canvas>(true)) if (c.name == "HUD") { canvas = c; break; }
            var go = new GameObject("MissionHud", typeof(RectTransform));
            var mh = go.AddComponent<MissionHud>();
            mh.mission = mission;
            mh.hud = hud;
            var rt = (RectTransform)go.transform;
            if (canvas != null) rt.SetParent(canvas.transform, false);
            else rt.SetParent(UiFactory.CreateOverlayCanvas("MissionHUD", 5).transform, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetSiblingIndex(0);   // under the HUD's own panels and the pause overlay
            mh.root = rt;
            mh.Build();
            mission.Announce += mh.OnAnnounce;
            MissionUnit.Damaged += mh.OnUnitDamaged;
            var status = canvas != null ? canvas.transform.Find("RaceStatus") : null;
            if (status != null) { mh.raceStatus = status.gameObject; mh.raceStatus.SetActive(false); }
            return mh;
        }

        private void Build()
        {
            white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            var top = new Vector2(0.5f, 1f);
            var panel = UiFactory.Card(root, "BossPanel", top, top, new Vector2(-380f, -212f), new Vector2(380f, -104f), 0.92f);
            bossPanel = panel;
            bossName = UiFactory.Text(panel, "BossName", Loc.Get("mission.boss"), 26f, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -44f), new Vector2(-120f, -6f), new Color(1f, 0.45f, 0.3f));
            bossName.fontStyle = FontStyles.Bold;
            corePct = UiFactory.Text(panel, "CorePct", "100%", 26f, TextAlignmentOptions.MidlineRight, new Vector2(0f, 1f), Vector2.one, new Vector2(-140f, -44f), new Vector2(-18f, -6f));
            var track = UiFactory.Panel(panel, "CoreBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -70f), new Vector2(-18f, -48f), new Color(0.12f, 0.08f, 0.07f), false);
            coreTrack = track;
            coreFill = Fill(track, new Color(1f, 0.32f, 0.18f));
            foreach (float notch in new[] { 0.33f, 0.66f })
                UiFactory.Panel(track, "Phase", new Vector2(notch, 0f), new Vector2(notch, 1f), new Vector2(-2f, -4f), new Vector2(2f, 4f), new Color(1f, 0.85f, 0.4f), false);
            var route = UiFactory.Panel(panel, "RouteBar", Vector2.zero, new Vector2(1f, 0f), new Vector2(18f, 12f), new Vector2(-300f, 22f), new Color(0.12f, 0.1f, 0.08f), false);
            routeTrack = route;
            routeFill = Fill(route, new Color(1f, 0.6f, 0.15f));
            routeText = UiFactory.Text(panel, "RouteText", "", 20f, TextAlignmentOptions.MidlineRight, Vector2.zero, new Vector2(1f, 0f), new Vector2(-290f, 4f), new Vector2(-18f, 32f), UiFactory.Muted);
            countText = UiFactory.Text(panel, "EnemyCount", "", 22f, TextAlignmentOptions.Center, top, top, new Vector2(-300f, -250f), new Vector2(300f, -216f), UiFactory.Muted);

            bossMarker = UiFactory.Panel(root, "BossMarker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-22f, -22f), new Vector2(22f, 22f), new Color(1f, 0.3f, 0.2f, 0.9f));
            bossMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            distancePanel = UiFactory.Panel(root, "BossDistancePanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-55f, -12f), new Vector2(55f, 12f), new Color(0.035f, 0.05f, 0.08f, 0.9f), false);
            markerText = UiFactory.Text(distancePanel, "BossDistance", "", 20f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiFactory.Accent);
            hitText = UiFactory.Text(root, "UnitHit", "×", 56f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-50f, 30f), new Vector2(50f, 130f), new Color(1f, 0.6f, 0.2f));
            hitText.gameObject.SetActive(false);
            for (int i = 0; i < MaxBars; i++)
            {
                var bar = UiFactory.Panel(root, "UnitBar" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -4f), new Vector2(30f, 4f), new Color(0f, 0f, 0f, 0.65f), false);
                barFills[i] = Fill(bar, new Color(1f, 0.35f, 0.2f));
                bars[i] = bar;
                bar.gameObject.SetActive(false);
            }
            // Only the command button receives input. World markers must let it through.
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            commandButton = UiFactory.Button(panel, "SquadCommand", Loc.Get("mission.squad_order") + " ›", ToggleSquadMap,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 24f, UiFactory.Raised);
            commandLabel = commandButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        private void ToggleSquadMap()
        {
            if (squadMap != null) { Destroy(squadMap.gameObject); squadMap = null; return; }
            if (mission.Boss == null || mission.Boss.Defeated) return;
            squadMap = UiFactory.Card(root, "SquadMap", new Vector2(.16f, .21f), new Vector2(.84f, .68f), Vector2.zero, Vector2.zero);
            squadMap.GetComponent<Image>().raycastTarget = true;
            BossModuleMap.Create(squadMap, mission.Boss, mission.Boss.Parts.IndexOf(mission.SquadTarget), selected =>
            {
                mission.MarkSquadTarget(mission.Boss.Parts[selected]);
                ToggleSquadMap();
                nextSlowRefresh = 0f;
            });
        }

        internal static float StatusWidthPixels(float screenWidth, float scale) => Mathf.Min(screenWidth - 24f, Mathf.Max(250f, 760f * scale));

        private void Layout()
        {
            float scale = Mathf.Max(0.01f, root.lossyScale.x);
            bool touch = TouchControlsPresenter.IsShown;
            if (layoutWidth == Screen.width && layoutHeight == Screen.height && Mathf.Abs(layoutScale - scale) < 0.001f && layoutTouch == touch) return;
            layoutWidth = Screen.width; layoutHeight = Screen.height; layoutScale = scale;
            layoutTouch = touch;
            // Keep the HUD coordinate system for world projections, but size information in
            // actual screen pixels. Uniformly enlarging the old card would cover the health HUD.
            float width = StatusWidthPixels(Screen.width, scale);
            if (hud != null) width = hud.ConstrainTopCenterWidth(width);
            float title = Mathf.Max(14f, 26f * scale), detail = Mathf.Max(12f, 20f * scale);
            float percentageWidth = title * 3.6f;
            bossName.fontSize = corePct.fontSize = title / scale;
            float nameHeight = bossName.GetPreferredValues(bossName.text, (width - percentageWidth - 28f) / scale, float.PositiveInfinity).y * scale;
            float count = Mathf.Max(12f, 22f * scale), header = Mathf.Max(title + 12f, nameHeight + 8f);
            float routeTop = header + 12f, routeBottom = routeTop + detail + 8f;
            float countTop = routeBottom + 12f, orderTop = countTop + count + 8f;
            float height = orderTop + Mathf.Max(32f, 42f * scale) + 4f;
            float top = 100f * scale + 8f; // below the ordinary race timer
            bossPanel.offsetMin = new Vector2(-width * 0.5f, -top - height) / scale;
            bossPanel.offsetMax = new Vector2(width * 0.5f, -top) / scale;
            Place(bossName.rectTransform, 10f, 3f, width - percentageWidth - 18f, header - 2f, scale);
            Place(corePct.rectTransform, width - percentageWidth - 10f, 3f, width - 10f, header - 2f, scale);
            Place(coreTrack, 10f, header, width - 10f, header + 7f, scale);
            Place(routeText.rectTransform, 10f, routeTop, width - 10f, routeBottom, scale);
            Place(routeTrack, 10f, routeBottom + 3f, width - 10f, routeBottom + 8f, scale);
            Place(countText.rectTransform, 10f, countTop, width - 10f, orderTop - 2f, scale);
            Place((RectTransform)commandButton.transform, 6f, orderTop, width - 6f, height - 4f, scale);
            commandLabel.fontSizeMax = Mathf.Max(14f, 22f * scale) / scale;
            routeText.fontSize = detail / scale; countText.fontSize = count / scale;
            routeText.alignment = TextAlignmentOptions.MidlineLeft;
            markerText.fontSize = Mathf.Max(12f, 20f * scale) / scale;
            distancePanel.sizeDelta = new Vector2(110f, 24f) / scale;
            bossMarker.sizeDelta = Vector2.one * Mathf.Max(14f, 44f * scale) / scale;
            markerBlockers.Clear();
            CollectMarkerBlockers(hud != null ? hud.transform : root);
            var controls = FindAnyObjectByType<TouchControlsPresenter>();
            if (controls != null) CollectMarkerBlockers(controls.transform);
        }

        private void CollectMarkerBlockers(Transform parent)
        {
            foreach (var rt in parent.GetComponentsInChildren<RectTransform>(true))
                if (MarkerBlockerNames.Contains(rt.name) && !markerBlockers.Contains(rt)) markerBlockers.Add(rt);
        }

        // Keep the marker's horizontal direction, but use the closest free vertical space
        // when a projected/off-screen target would put its distance behind another HUD card.
        private Vector2 AvoidHud(Vector2 point, float unitsPerPixel)
        {
            occupied.Clear();
            foreach (var rt in markerBlockers)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                rt.GetWorldCorners(corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                occupied.Add(Rect.MinMaxRect(a.x, a.y, b.x, b.y));
            }
            Vector2 screen = point / unitsPerPixel + new Vector2(Screen.width, Screen.height) * 0.5f;
            float above = bossMarker.sizeDelta.y / unitsPerPixel * 0.71f;
            if (MarkerFits(screen.x, screen.y, above)) return point;
            float best = screen.y, distance = float.PositiveInfinity;
            foreach (var r in occupied)
                for (int side = 0; side < 2; side++)
                {
                    float candidate = side == 0 ? r.yMin - above - 4f : r.yMax + 42f;
                    float delta = Mathf.Abs(candidate - screen.y);
                    if (delta >= distance || !MarkerFits(screen.x, candidate, above)) continue;
                    best = candidate; distance = delta;
                }
            screen.y = best;
            return (screen - new Vector2(Screen.width, Screen.height) * 0.5f) * unitsPerPixel;
        }

        private bool MarkerFits(float x, float y, float above)
        {
            var box = new Rect(x - 55f, y - 38f, 110f, 38f + above);
            if (box.yMin < 8f || box.yMax > Screen.height - 8f) return false;
            foreach (var r in occupied) if (box.Overlaps(r)) return false;
            return true;
        }

        private static void Place(RectTransform rt, float left, float top, float right, float bottom, float scale)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = new Vector2(left, -bottom) / scale;
            rt.offsetMax = new Vector2(right, -top) / scale;
        }

        private Image Fill(RectTransform track, Color color)
        {
            var fill = UiFactory.Panel(track, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color, false).GetComponent<Image>();
            fill.sprite = white;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            return fill;
        }

        private void OnAnnounce(string text, Color color)
        {
            if (hud != null) hud.ShowEvent(text, color);
        }

        private void OnUnitDamaged(MissionUnit unit, GameObject attacker, float damage)
        {
            var player = mission != null && RaceDirector.Current != null ? PlayerCar() : null;
            if (player == null || attacker != player.gameObject) return;
            hitUntil = Time.unscaledTime + (unit.Alive ? 0.12f : 0.35f);
            hitText.color = unit.Alive ? new Color(1f, 0.6f, 0.2f) : new Color(1f, 0.15f, 0.1f);
            hitText.gameObject.SetActive(true);
        }

        private CarController player;
        private CarController PlayerCar()
        {
            if (player == null)
            {
                var race = FindAnyObjectByType<RaceManager>();
                player = race != null && race.Player != null ? race.Player.Car : null;
            }
            return player;
        }

        private void LateUpdate()
        {
            if (mission == null || mission.Boss == null) return;
            Layout();
            var boss = mission.Boss;
            if (hitText.gameObject.activeSelf && Time.unscaledTime > hitUntil) hitText.gameObject.SetActive(false);
            float core = boss.CoreFraction;
            coreFill.fillAmount = core;
            routeFill.fillAmount = boss.Progress01;
            if (Time.unscaledTime >= nextSlowRefresh)
            {
                nextSlowRefresh = Time.unscaledTime + 0.2f;
                corePct.SetText("{0:0}%", Mathf.Ceil(core * 100f));
                var marked = mission.SquadTarget;
                commandLabel.text = Loc.Get(marked != null ? "mission.part." + marked.Role.ToString().ToLowerInvariant() : "mission.squad_order") + " ›";
                commandButton.interactable = !boss.Defeated;
                routeText.text = Loc.Format("mission.route", "m", Mathf.RoundToInt(boss.RemainingMetres));
                var me = PlayerCar() != null ? PlayerCar().GetComponent<RaceParticipant>() : null;
                countText.text = Loc.Format("mission.count", "n", mission.EnemiesAlive, "k", me != null ? me.Kills : 0);
            }
            var cam = Camera.main;
            if (cam == null) return;
            PlaceBossMarker(cam, boss);
            PlaceUnitBars(cam);
        }

        private void PlaceBossMarker(Camera cam, BossCrawler boss)
        {
            bool show = !boss.Defeated;
            bossMarker.gameObject.SetActive(show);
            distancePanel.gameObject.SetActive(show);
            if (!show) return;
            var marked = mission.SquadTarget;
            Vector3 target = marked != null ? marked.AimPoint : boss.transform.position + Vector3.up * 30f;
            Vector3 sp = cam.WorldToScreenPoint(target);
            Vector2 half = root.rect.size * 0.5f;
            float scale = root.rect.width / Mathf.Max(1f, Screen.width);
            Vector2 p = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f) * scale;
            if (sp.z < 0f) p = -p;
            // Reserve room for the distance label as well as the diamond at either edge.
            Vector2 bounds = new Vector2(Mathf.Max(1f, half.x - 64f * scale), Mathf.Max(1f, half.y - 54f * scale));
            bool onScreen = sp.z > 0f && Mathf.Abs(p.x) < bounds.x && Mathf.Abs(p.y) < bounds.y;
            if (!onScreen)
            {
                // Clamp to the screen edge along the direction to the fortress.
                if (p.sqrMagnitude < 1f) p = Vector2.down;
                float k = Mathf.Min(bounds.x / Mathf.Max(1f, Mathf.Abs(p.x)), bounds.y / Mathf.Max(1f, Mathf.Abs(p.y)));
                p *= k;
            }
            p = AvoidHud(p, scale);
            bossMarker.anchoredPosition = p;
            distancePanel.anchoredPosition = p + new Vector2(0f, -26f * scale);
            var car = PlayerCar();
            float metres = car != null ? Vector3.Distance(car.transform.position, marked != null ? target : boss.transform.position) : 0f;
            markerText.SetText("{0:0} m", metres);
        }

        // Small bars over hurt units near the player (not the boss parts on the hull: the top bar covers the core).
        private readonly List<MissionUnit> shown = new List<MissionUnit>(MaxBars);
        private void PlaceUnitBars(Camera cam)
        {
            shown.Clear();
            var car = PlayerCar();
            Vector3 from = car != null ? car.transform.position : cam.transform.position;
            foreach (var u in MissionUnit.All)
            {
                if (shown.Count >= MaxBars) break;
                if (!u.Alive || !u.ShowBar || u.Health >= u.MaxHealth - 0.01f || u.Hidden) continue;
                if ((u.transform.position - from).sqrMagnitude > 110f * 110f) continue;
                shown.Add(u);
            }
            float scale = root.rect.width / Mathf.Max(1f, Screen.width);
            for (int i = 0; i < MaxBars; i++)
            {
                bool on = i < shown.Count;
                if (on)
                {
                    var u = shown[i];
                    Vector3 sp = cam.WorldToScreenPoint(u.AimPoint + Vector3.up * (u.Radius + 0.8f));
                    on = sp.z > 0f;
                    if (on)
                    {
                        bars[i].anchoredPosition = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f) * scale;
                        barFills[i].fillAmount = u.Health / u.MaxHealth;
                    }
                }
                if (bars[i].gameObject.activeSelf != on) bars[i].gameObject.SetActive(on);
            }
        }

        public void Dispose()
        {
            if (mission != null) mission.Announce -= OnAnnounce;
            MissionUnit.Damaged -= OnUnitDamaged;
            if (raceStatus != null) raceStatus.SetActive(true);
            if (white != null) Destroy(white);
            if (this != null) Destroy(gameObject);
        }
    }
}
