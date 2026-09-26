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
        private RectTransform root, bossMarker;
        private GameObject raceStatus;
        private Image coreFill, routeFill;
        private TextMeshProUGUI bossName, corePct, routeText, countText, markerText, hitText;
        private readonly RectTransform[] bars = new RectTransform[MaxBars];
        private readonly Image[] barFills = new Image[MaxBars];
        private float hitUntil, nextSlowRefresh;
        private Sprite white;

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
            bossName = UiFactory.Text(panel, "BossName", Loc.Get("mission.boss"), 26f, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -44f), new Vector2(-120f, -6f), new Color(1f, 0.45f, 0.3f));
            bossName.fontStyle = FontStyles.Bold;
            corePct = UiFactory.Text(panel, "CorePct", "100%", 26f, TextAlignmentOptions.MidlineRight, new Vector2(0f, 1f), Vector2.one, new Vector2(-140f, -44f), new Vector2(-18f, -6f));
            var track = UiFactory.Panel(panel, "CoreBar", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -70f), new Vector2(-18f, -48f), new Color(0.12f, 0.08f, 0.07f), false);
            coreFill = Fill(track, new Color(1f, 0.32f, 0.18f));
            foreach (float notch in new[] { 0.33f, 0.66f })
                UiFactory.Panel(track, "Phase", new Vector2(notch, 0f), new Vector2(notch, 1f), new Vector2(-2f, -4f), new Vector2(2f, 4f), new Color(1f, 0.85f, 0.4f), false);
            var route = UiFactory.Panel(panel, "RouteBar", Vector2.zero, new Vector2(1f, 0f), new Vector2(18f, 12f), new Vector2(-300f, 22f), new Color(0.12f, 0.1f, 0.08f), false);
            routeFill = Fill(route, new Color(1f, 0.6f, 0.15f));
            routeText = UiFactory.Text(panel, "RouteText", "", 20f, TextAlignmentOptions.MidlineRight, Vector2.zero, new Vector2(1f, 0f), new Vector2(-290f, 4f), new Vector2(-18f, 32f), UiFactory.Muted);
            countText = UiFactory.Text(root, "EnemyCount", "", 22f, TextAlignmentOptions.Center, top, top, new Vector2(-300f, -250f), new Vector2(300f, -216f), UiFactory.Muted);

            bossMarker = UiFactory.Panel(root, "BossMarker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-22f, -22f), new Vector2(22f, 22f), new Color(1f, 0.3f, 0.2f, 0.9f));
            bossMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);
            markerText = UiFactory.Text(root, "BossDistance", "", 20f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-80f, -60f), new Vector2(80f, -30f), new Color(1f, 0.55f, 0.4f));
            hitText = UiFactory.Text(root, "UnitHit", "×", 56f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-50f, 30f), new Vector2(50f, 130f), new Color(1f, 0.6f, 0.2f));
            hitText.gameObject.SetActive(false);
            for (int i = 0; i < MaxBars; i++)
            {
                var bar = UiFactory.Panel(root, "UnitBar" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -4f), new Vector2(30f, 4f), new Color(0f, 0f, 0f, 0.65f), false);
                barFills[i] = Fill(bar, new Color(1f, 0.35f, 0.2f));
                bars[i] = bar;
                bar.gameObject.SetActive(false);
            }
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
            var boss = mission.Boss;
            if (hitText.gameObject.activeSelf && Time.unscaledTime > hitUntil) hitText.gameObject.SetActive(false);
            float core = boss.CoreFraction;
            coreFill.fillAmount = core;
            routeFill.fillAmount = boss.Progress01;
            if (Time.unscaledTime >= nextSlowRefresh)
            {
                nextSlowRefresh = Time.unscaledTime + 0.2f;
                corePct.SetText("{0:0}%", Mathf.Ceil(core * 100f));
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
            markerText.gameObject.SetActive(show);
            if (!show) return;
            Vector3 target = boss.transform.position + Vector3.up * 30f;
            Vector3 sp = cam.WorldToScreenPoint(target);
            Vector2 half = root.rect.size * 0.5f;
            float scale = root.rect.width / Mathf.Max(1f, Screen.width);
            Vector2 p = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f) * scale;
            if (sp.z < 0f) p = -p;
            bool onScreen = sp.z > 0f && Mathf.Abs(p.x) < half.x - 40f && Mathf.Abs(p.y) < half.y - 40f;
            if (!onScreen)
            {
                // Clamp to the screen edge along the direction to the fortress.
                if (p.sqrMagnitude < 1f) p = Vector2.down;
                float k = Mathf.Min((half.x - 50f) / Mathf.Max(1f, Mathf.Abs(p.x)), (half.y - 90f) / Mathf.Max(1f, Mathf.Abs(p.y)));
                p *= k;
            }
            bossMarker.anchoredPosition = p;
            markerText.rectTransform.anchoredPosition = p + new Vector2(0f, -40f);
            var car = PlayerCar();
            float metres = car != null ? Vector3.Distance(car.transform.position, boss.transform.position) : 0f;
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
