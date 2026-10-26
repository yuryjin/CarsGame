using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RacingGame
{
    /// <summary>Projected health bars and readable, separated names. Leader lines retain the car association in a pack.</summary>
    public sealed class WorldHealthBars : MonoBehaviour
    {
        public RaceManager race;
        public Vector2 size = new Vector2(90f, 10f);
        public float heightAboveCar = 1.9f;

        private sealed class Entry
        {
            public RaceParticipant P;
            public RectTransform Rt, Plate, Leader;
            public Image Fill, Trail, Back, Line;
            public TextMeshProUGUI Name;
            public Color Tint;
            public Vector3 Screen;
            public Vector2 LabelPixels, BarPixels;
            public bool Carrier;
            public int Slot, Id;
            public float CompactAt;
            public float HealthRatio, DamageUntil, FlashUntil;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private readonly List<Entry> visible = new List<Entry>();
        private readonly List<RectTransform> blockers = new List<RectTransform>();
        private readonly List<Rect> occupied = new List<Rect>();
        private readonly Vector3[] corners = new Vector3[4];
        private static readonly HashSet<string> BlockerNames = new HashSet<string>
        {
            "RaceStatus", "LiveGoals", "TimerPanel", "Health", "AmmoPanel", "SecondaryPanel", "StatusPanel",
            "PauseButton", "HotBomb", "BossPanel", "Speedometer", "HintsPanel", "EventPanel",
            "Left", "Right", "Fire", "Brake", "SwapWeapon", "Secondary", "Reset", "FinishCard", "ReturnToGarage"
        };
        private static readonly System.Comparison<Entry> PriorityComparer = ComparePriority;
        private Canvas canvas;
        private RectTransform canvasRt, leaders;
        private float layoutScale = -1f;
        private int layoutWidth, layoutHeight;
        private bool layoutTouch;
        private float columnSpacing, rowSpacing, packCentre;

        private void Awake()
        {
            if (race == null) race = GetComponent<RaceManager>() ?? FindAnyObjectByType<RaceManager>();
            canvas = UiFactory.CreateOverlayCanvas("WorldBars", -1);
            canvas.transform.SetParent(transform, false);
            canvasRt = canvas.GetComponent<RectTransform>();
            // All lines render behind all plates, including other cars' labels.
            leaders = UiFactory.Panel(canvasRt, "NameLeaders", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void OnEnable()
        {
            if (race != null) race.OnFieldSpawned += Rebuild;
            Loc.LanguageChanged += Rebuild;
        }

        private void OnDisable()
        {
            if (race != null) race.OnFieldSpawned -= Rebuild;
            Loc.LanguageChanged -= Rebuild;
        }

        private void RefreshLayout(float scale)
        {
            bool touch = TouchControlsPresenter.IsShown;
            if (layoutWidth == Screen.width && layoutHeight == Screen.height && layoutTouch == touch && Mathf.Abs(layoutScale - scale) < 0.001f) return;
            layoutScale = scale; layoutWidth = Screen.width; layoutHeight = Screen.height; layoutTouch = touch;
            foreach (var e in entries)
            {
                e.Name.fontSize = Mathf.Max(21f, 12f / scale);
                Vector2 preferred = e.Name.GetPreferredValues(e.Name.text, float.PositiveInfinity, float.PositiveInfinity) * scale;
                e.LabelPixels = new Vector2(Mathf.Clamp(preferred.x + 8f, 24f, Screen.width - 8f), Mathf.Max(18f, preferred.y + 4f));
                e.Plate.sizeDelta = e.LabelPixels / scale;
                e.BarPixels = new Vector2(Mathf.Max(size.x * scale, 48f), Mathf.Max(size.y * scale, 7f));
                e.Rt.sizeDelta = e.BarPixels / scale;
                // One screen pixel of border: the old three-canvas-unit inset consumed
                // most of the health fill on a small screen.
                e.Fill.rectTransform.offsetMin = e.Trail.rectTransform.offsetMin = Vector2.one / scale;
                e.Fill.rectTransform.offsetMax = e.Trail.rectTransform.offsetMax = -Vector2.one / scale;
                e.Slot = 0; e.CompactAt = 0f;
            }
            blockers.Clear();
            var hud = race != null ? race.GetComponent<HudScreen>() : null;
            if (hud == null) hud = FindAnyObjectByType<HudScreen>();
            if (hud != null) CollectBlockers(hud.transform);
            var overlays = FindAnyObjectByType<RaceOverlays>();
            if (overlays != null) CollectBlockers(overlays.transform);
            var controls = FindAnyObjectByType<TouchControlsPresenter>();
            if (controls != null) CollectBlockers(controls.transform);
        }

        private void CollectBlockers(Transform parent)
        {
            foreach (var rt in parent.GetComponentsInChildren<RectTransform>(true))
                if (BlockerNames.Contains(rt.name) && !blockers.Contains(rt)) blockers.Add(rt);
        }

        private void LateUpdate()
        {
            if (race == null) return;
            if (entries.Count != CountAi()) Rebuild();
            var cam = Camera.main;
            if (cam == null) return;
            float scale = Mathf.Max(0.01f, canvasRt.lossyScale.x);
            RefreshLayout(scale);
            occupied.Clear(); visible.Clear();
            foreach (var rt in blockers)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                rt.GetWorldCorners(corners);
                Vector2 a = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
                Vector2 b = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
                occupied.Add(Rect.MinMaxRect(a.x, a.y, b.x, b.y));
            }
            foreach (var e in entries)
            {
                if (e.P == null) { e.Rt.gameObject.SetActive(false); e.Leader.gameObject.SetActive(false); continue; }
                Vector3 above = e.P.Car != null ? e.P.Car.GravityUp : Vector3.up;
                e.Screen = cam.WorldToScreenPoint(e.P.transform.position + above * heightAboveCar);
                bool show = e.P.gameObject.activeInHierarchy && e.Screen.z > 0f && e.Screen.z < 120f
                    && e.Screen.x >= 0f && e.Screen.x <= Screen.width && e.Screen.y >= 0f && e.Screen.y <= Screen.height
                    && race.State >= RaceState.Countdown && race.State < RaceState.Finished && !PauseController.IsPaused;
                e.Rt.gameObject.SetActive(show);
                if (!show) { e.Leader.gameObject.SetActive(false); continue; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, e.Screen, null, out Vector2 local);
                e.Rt.anchoredPosition = local;
                float t = e.P.Health != null && e.P.Health.maxHealth > 0f ? e.P.Health.Health / e.P.Health.maxHealth : 0f;
                if (t < e.HealthRatio - 0.001f)
                {
                    e.Trail.fillAmount = Mathf.Max(e.Trail.fillAmount, e.HealthRatio);
                    e.DamageUntil = Time.time + 0.35f;
                    e.FlashUntil = Time.time + 0.12f;
                }
                else if (t > e.HealthRatio + 0.001f)
                {
                    e.Trail.fillAmount = t;
                    e.FlashUntil = 0f;
                }
                if (Time.time > e.DamageUntil)
                    e.Trail.fillAmount = Mathf.MoveTowards(e.Trail.fillAmount, t, Time.deltaTime * 1.5f);
                e.HealthRatio = t;
                e.Fill.fillAmount = t;
                e.Fill.color = Time.time < e.FlashUntil ? Color.white : Color.Lerp(new Color(1f, 0.3f, 0.28f), UiFactory.Positive, t);
                occupied.Add(new Rect((Vector2)e.Screen - e.BarPixels * 0.5f, e.BarPixels));
                if (e.Screen.z >= 60f || (e.P.Health != null && e.P.Health.IsDead))
                { e.Plate.gameObject.SetActive(false); e.Leader.gameObject.SetActive(false); continue; }
                e.Carrier = e.P.HotBomb != null && e.P.HotBomb.Carrier == e.P;
                visible.Add(e);
            }
            columnSpacing = rowSpacing = packCentre = 0f;
            foreach (var e in visible)
            {
                columnSpacing = Mathf.Max(columnSpacing, e.LabelPixels.x + 6f);
                rowSpacing = Mathf.Max(rowSpacing, e.LabelPixels.y + 4f);
                packCentre += e.Screen.x;
            }
            if (visible.Count > 0) packCentre /= visible.Count;
            visible.Sort(PriorityComparer);
            foreach (var e in visible) PlaceName(e, scale);
        }

        private static int ComparePriority(Entry a, Entry b)
        {
            int order = b.Carrier.CompareTo(a.Carrier);
            if (order == 0) order = a.Screen.z.CompareTo(b.Screen.z);
            return order != 0 ? order : a.Id.CompareTo(b.Id);
        }

        private Rect Candidate(Entry e, int slot)
        {
            float sign = slot < 12 ? 1f : -1f;
            float offset = e.BarPixels.y * 0.5f + 4f + e.LabelPixels.y * 0.5f + ((slot % 12) / 3) * rowSpacing;
            float column = slot % 3 == 0 ? 0f : slot % 3 == 1 ? -1f : 1f;
            float x = Mathf.Clamp(e.Screen.x + column * columnSpacing, e.LabelPixels.x * 0.5f + 4f, Screen.width - e.LabelPixels.x * 0.5f - 4f);
            return new Rect(new Vector2(x, e.Screen.y + sign * offset) - e.LabelPixels * 0.5f, e.LabelPixels);
        }

        private bool Fits(Rect box, float gap)
        {
            if (box.xMin < 4f || box.yMin < 4f || box.xMax > Screen.width - 4f || box.yMax > Screen.height - 4f) return false;
            var padded = Rect.MinMaxRect(box.xMin - gap, box.yMin - gap, box.xMax + gap, box.yMax + gap);
            foreach (var other in occupied) if (padded.Overlaps(other)) return false;
            return true;
        }

        private void PlaceName(Entry e, float scale)
        {
            Rect box = Candidate(e, e.Slot);
            bool defaultFree = Fits(Candidate(e, 0), 2f);
            if (!defaultFree) e.CompactAt = Time.unscaledTime + 0.6f;
            if (e.Slot != 0 && defaultFree && Time.unscaledTime >= e.CompactAt) { e.Slot = 0; box = Candidate(e, 0); }
            if (!Fits(box, 2f))
            {
                bool found = false;
                for (int step = 0; step < 24; step++)
                {
                    int column = step % 3;
                    if (column != 0 && e.Screen.x >= packCentre) column = 3 - column;
                    int slot = (step / 3) * 3 + column;
                    box = Candidate(e, slot);
                    if (!Fits(box, 2f)) continue;
                    e.Slot = slot; found = true; break;
                }
                // Distant/occluded names may yield, but the health bar remains at the car.
                if (!found) { e.Plate.gameObject.SetActive(false); e.Leader.gameObject.SetActive(false); return; }
            }
            occupied.Add(box);
            e.Plate.gameObject.SetActive(true);
            e.Plate.anchoredPosition = (box.center - (Vector2)e.Screen) / scale;
            e.Name.color = e.Carrier ? UiFactory.Accent : e.Tint;
            e.Back.color = e.Carrier ? new Color(0.16f, 0.08f, 0.03f, 0.94f) : new Color(0.035f, 0.05f, 0.08f, 0.88f);
            bool displaced = e.Slot != 0 || Mathf.Abs(box.center.x - e.Screen.x) > 2f;
            e.Leader.gameObject.SetActive(displaced);
            if (!displaced) return;
            float sign = e.Slot < 12 ? 1f : -1f;
            Vector2 start = e.Rt.anchoredPosition + Vector2.up * (sign * e.BarPixels.y * 0.5f / scale);
            Vector2 end = e.Rt.anchoredPosition + (new Vector2(box.center.x, sign > 0f ? box.yMin : box.yMax) - (Vector2)e.Screen) / scale;
            Vector2 delta = end - start;
            e.Leader.anchoredPosition = (start + end) * 0.5f;
            e.Leader.sizeDelta = new Vector2(1f / scale, delta.magnitude);
            e.Leader.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f);
            Color c = e.Name.color; c.a = 0.65f; e.Line.color = c;
        }

        private int CountAi()
        {
            int n = 0;
            foreach (var p in race.Participants) if (!p.isPlayer) n++;
            return n;
        }

        private void Rebuild()
        {
            if (race == null || canvas == null) return;
            foreach (var e in entries)
            {
                if (e.Rt != null) Destroy(e.Rt.gameObject);
                if (e.Leader != null) Destroy(e.Leader.gameObject);
            }
            entries.Clear(); layoutScale = -1f;
            foreach (var p in race.Participants)
            {
                if (p.isPlayer) continue;
                var bg = UiFactory.Bar(canvas.transform, "Bar_" + p.displayName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -size * 0.5f, size * 0.5f, new Color(0.2f, 0.85f, 0.25f), out var fill);
                var trail = UiFactory.Panel(bg.transform, "DamageTrail", Vector2.zero, Vector2.one,
                    Vector2.one, -Vector2.one, new Color(1f, 0.72f, 0.3f)).GetComponent<Image>();
                trail.type = Image.Type.Filled; trail.fillMethod = Image.FillMethod.Horizontal; trail.fillOrigin = 0;
                trail.transform.SetSiblingIndex(fill.transform.GetSiblingIndex());
                float healthRatio = p.Health != null && p.Health.maxHealth > 0f ? p.Health.Health / p.Health.maxHealth : 0f;
                fill.fillAmount = trail.fillAmount = healthRatio;
                var line = UiFactory.Panel(leaders, "NameLeader", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.white, false);
                line.gameObject.SetActive(false);
                var plate = UiFactory.Panel(bg.transform, "NamePlate", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one, UiFactory.Surface);
                var name = UiFactory.Text(plate, "RivalName", UiFactory.Escape(HudScreen.GetLocalizedRacerName(p.displayName)),
                    21f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(4f, 2f), new Vector2(-4f, -2f));
                name.fontStyle = FontStyles.Bold; name.enableWordWrapping = false; name.overflowMode = TextOverflowModes.Ellipsis;
                var ai = p.GetComponent<AiDriver>();
                if (ai != null && ai.profile != null) name.color = Color.Lerp(ai.profile.paint, Color.white, 0.45f);
                entries.Add(new Entry { P = p, Rt = bg.rectTransform, Fill = fill, Trail = trail, HealthRatio = healthRatio, Name = name, Plate = plate,
                    Leader = line, Line = line.GetComponent<Image>(), Back = plate.GetComponent<Image>(), Tint = name.color, Id = p.Car.CarId });
            }
        }
    }
}
