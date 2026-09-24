using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RacingGame
{
    /// <summary>Small screen-space HP bars projected above every AI car (one overlay canvas, no per-car canvases).</summary>
    public sealed class WorldHealthBars : MonoBehaviour
    {
        public RaceManager race;
        public Vector2 size = new Vector2(90f, 10f);
        public float heightAboveCar = 1.9f;

        private sealed class Entry { public RaceParticipant P; public RectTransform Rt; public Image Fill; public TextMeshProUGUI Name; }
        private readonly List<Entry> entries = new List<Entry>();
        private Canvas canvas;
        private RectTransform canvasRt;

        private void Awake()
        {
            if (race == null) race = GetComponent<RaceManager>() ?? FindAnyObjectByType<RaceManager>();
            canvas = UiFactory.CreateOverlayCanvas("WorldBars", -1);
            canvas.transform.SetParent(transform, false);
            canvasRt = canvas.GetComponent<RectTransform>();
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

        private void LateUpdate()
        {
            if (race == null) return;
            if (entries.Count != CountAi()) Rebuild();
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var e in entries)
            {
                if (e.P == null) continue;
                Vector3 world = e.P.transform.position + Vector3.up * heightAboveCar;
                Vector3 sp = cam.WorldToScreenPoint(world);
                bool visible = sp.z > 0f && sp.z < 120f && race.State >= RaceState.Countdown && race.State < RaceState.Finished && !PauseController.IsPaused;
                e.Rt.gameObject.SetActive(visible);
                if (!visible) continue;
                e.Name.gameObject.SetActive(sp.z < 60f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, sp, null, out Vector2 local);
                e.Rt.anchoredPosition = local;
                float t = e.P.Health != null && e.P.Health.maxHealth > 0f ? e.P.Health.Health / e.P.Health.maxHealth : 0f;
                e.Fill.fillAmount = t;
                e.Fill.color = Color.Lerp(new Color(1f, 0.3f, 0.28f), UiFactory.Positive, t);
            }
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
            foreach (var e in entries) if (e.Rt != null) Destroy(e.Rt.gameObject);
            entries.Clear();
            foreach (var p in race.Participants)
            {
                if (p.isPlayer) continue;
                var bg = UiFactory.Bar(canvas.transform, "Bar_" + p.displayName, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), -size * 0.5f, size * 0.5f, new Color(0.2f, 0.85f, 0.25f), out var fill);
                var name = UiFactory.Text(bg.transform, "RivalName", UiFactory.Escape(HudScreen.GetLocalizedRacerName(p.displayName)),
                    21f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(-95f, 4f), new Vector2(95f, 33f));
                name.fontStyle = FontStyles.Bold;
                name.raycastTarget = false;
                var ai = p.GetComponent<AiDriver>();
                if (ai != null && ai.profile != null) name.color = Color.Lerp(ai.profile.paint, Color.white, 0.45f);
                entries.Add(new Entry { P = p, Rt = bg.rectTransform, Fill = fill, Name = name });
            }
        }
    }
}
