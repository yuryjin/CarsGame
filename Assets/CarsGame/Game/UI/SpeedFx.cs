using UnityEngine;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Speed feel on top of the camera's FOV: streak lines rushing in from the screen edges near top speed
    /// and during a boost, and the drift charge meter (three stage pips) under the reticle. Own overlay
    /// canvas below the HUD; plain Images moved in LateUpdate, no allocations after Awake.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class SpeedFx : MonoBehaviour
    {
        public int lineCount = 16;
        [Range(0f, 1f)] [Tooltip("Fraction of top speed where the lines start to appear.")]
        public float lineStartSpeed = 0.85f;
        public float lineSpeed = 2600f;   // reference units per second
        [Range(0f, 1f)] [Tooltip("Peak line opacity at full intensity.")]
        public float lineAlpha = 0.1f;

        private RaceManager race;
        private Canvas canvas;
        private RectTransform root;
        private RectTransform[] lines;
        private Image[] lineImages;
        private float[] linePhase, lineAngle, lineLength;
        private float intensity;
        private RectTransform driftMeter;
        private readonly Image[] driftPips = new Image[3];
        private Image driftFill;
        private TMPro.TextMeshProUGUI driftLabel;
        private int releasedStage;
        private CarController car;
        private int shownStage = -1;
        private float boostFlash;
        private float pixelScale = -1f;
        private int layoutWidth;

        private static readonly Color[] StageColors = { new Color(1f, 1f, 1f, 0.9f), new Color(0.35f, 0.75f, 1f), new Color(1f, 0.65f, 0.15f), new Color(1f, 0.3f, 0.85f) };

        private void Awake()
        {
            race = GetComponent<RaceManager>() ?? FindAnyObjectByType<RaceManager>();
            canvas = UiFactory.CreateOverlayCanvas("SpeedFx", -2);
            canvas.transform.SetParent(transform, false);
            Destroy(canvas.GetComponent<GraphicRaycaster>());   // purely visual
            root = (RectTransform)canvas.transform;

            lines = new RectTransform[lineCount]; lineImages = new Image[lineCount];
            linePhase = new float[lineCount]; lineAngle = new float[lineCount]; lineLength = new float[lineCount];
            var rng = new System.Random(7);
            for (int i = 0; i < lineCount; i++)
            {
                var rt = UiFactory.Panel(root, "Line" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-2f, 0f), new Vector2(2f, 1f), new Color(1f, 1f, 1f, 0f), false);
                rt.pivot = new Vector2(0.5f, 0f);
                var img = rt.GetComponent<Image>(); img.raycastTarget = false;
                lines[i] = rt; lineImages[i] = img;
                lineAngle[i] = (i + (float)rng.NextDouble() * 0.8f) / lineCount * 360f;
                linePhase[i] = (float)rng.NextDouble();
                lineLength[i] = 80f + (float)rng.NextDouble() * 140f;
            }

            driftMeter = UiFactory.Panel(root, "DriftMeter", new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.3f), new Vector2(-110f, -9f), new Vector2(110f, 9f), new Color(0.03f, 0.05f, 0.09f, 0.75f), false);
            driftFill = UiFactory.Panel(driftMeter, "Fill", Vector2.zero, new Vector2(0f, 1f), new Vector2(3f, 3f), new Vector2(3f, -3f), StageColors[0], false).GetComponent<Image>();
            for (int i = 0; i < 3; i++)
            {
                float x = i == 0 ? 0.6f / 2.4f : i == 1 ? 1.4f / 2.4f : 1f;
                driftPips[i] = UiFactory.Panel(driftMeter, "Stage" + i, new Vector2(x, 0f), new Vector2(x, 1f), new Vector2(-3f, -6f), new Vector2(0f, 6f), new Color(1f, 1f, 1f, 0.35f), false).GetComponent<Image>();
            }
            foreach (var img in driftMeter.GetComponentsInChildren<Image>()) img.raycastTarget = false;
            driftLabel = UiFactory.Text(driftMeter, "Action", "", 24f, TMPro.TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-240f, 12f), new Vector2(240f, 65f), Color.white);
            driftLabel.raycastTarget = false;
            driftLabel.enableAutoSizing = true; driftLabel.fontSizeMin = 18f; driftLabel.fontSizeMax = 24f;
            driftMeter.gameObject.SetActive(false);
            SetVisible(false);
        }

        public void SetVisible(bool visible) { if (canvas != null) canvas.gameObject.SetActive(visible); }

        private void OnDrift(int stage, float seconds) { boostFlash = 1f; releasedStage = stage; }

        private void UpdateDriftLayout()
        {
            float scale = Mathf.Max(0.01f, root.lossyScale.x);
            if (Mathf.Abs(scale - pixelScale) < 0.001f && layoutWidth == Screen.width) return;
            pixelScale = scale; layoutWidth = Screen.width;
            driftMeter.sizeDelta = new Vector2(Mathf.Max(160f, 220f * scale), Mathf.Max(10f, 18f * scale)) / scale;
            driftFill.rectTransform.offsetMin = new Vector2(2f, 2f) / scale;
            driftFill.rectTransform.offsetMax = new Vector2(2f - 4f * driftFill.rectTransform.anchorMax.x, -2f) / scale;
            foreach (var pip in driftPips)
            {
                pip.rectTransform.offsetMin = new Vector2(-2f, -3f) / scale;
                pip.rectTransform.offsetMax = new Vector2(0f, 3f) / scale;
            }
            float width = Mathf.Clamp(Screen.width * 0.6f, 240f, 520f) / scale;
            driftLabel.rectTransform.offsetMin = new Vector2(-width * 0.5f, 6f / scale);
            driftLabel.rectTransform.offsetMax = new Vector2(width * 0.5f, 46f / scale);
            driftLabel.fontSizeMin = 14f / scale;
            driftLabel.fontSizeMax = Mathf.Max(24f, 14f / scale);
        }

        private void LateUpdate()
        {
            UpdateDriftLayout();
            var player = race != null ? race.Player : null;
            var c = player != null ? player.Car : null;
            if (c != car)
            {
                if (car != null) car.DriftBoosted -= OnDrift;
                car = c;
                if (car != null) car.DriftBoosted += OnDrift;
                shownStage = -1;
                boostFlash = 0f;
            }
            bool racing = car != null && race.State >= RaceState.Racing && race.State < RaceState.Finished && !PauseController.IsPaused;
            float dt = Time.unscaledDeltaTime;

            // ---- speed lines ----
            float target = 0f;
            if (racing)
            {
                float top = Mathf.Max(1f, car.maxSpeed);
                target = Mathf.Clamp01((Mathf.Abs(car.SpeedKmh) / top - lineStartSpeed) / (1f - lineStartSpeed));
                if (car.IsBoosting) target = Mathf.Max(target, 0.7f);
                target = Mathf.Max(target, boostFlash * 0.7f);
            }
            intensity = Mathf.MoveTowards(intensity, target, dt * (target > intensity ? 4f : 2f));
            if (!PauseController.IsPaused) boostFlash = Mathf.MoveTowards(boostFlash, 0f, dt * 0.8f);
            float halfW = root.rect.width * 0.5f, halfH = root.rect.height * 0.5f;
            float outer = Mathf.Sqrt(halfW * halfW + halfH * halfH);
            for (int i = 0; i < lines.Length; i++)
            {
                if (intensity <= 0.01f)
                {
                    if (lineImages[i].color.a != 0f) lineImages[i].color = new Color(1f, 1f, 1f, 0f);
                    continue;
                }
                // Each line travels from the corner radius inwards to ~75 % (stays at the periphery) and restarts; brighter the faster we go.
                linePhase[i] += dt * lineSpeed * (0.6f + 0.6f * intensity) / outer;
                if (linePhase[i] > 1f) { linePhase[i] -= 1f; lineAngle[i] = (lineAngle[i] + 137.5f) % 360f; }
                float r = Mathf.Lerp(outer, outer * 0.75f, linePhase[i]);
                float a = lineAngle[i] * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var rt = lines[i];
                rt.anchoredPosition = dir * r;
                rt.localRotation = Quaternion.Euler(0f, 0f, lineAngle[i] + 90f);
                rt.sizeDelta = new Vector2(1.5f + 1f * intensity, lineLength[i] * (0.5f + intensity));
                float fade = Mathf.Sin(linePhase[i] * Mathf.PI);
                lineImages[i].color = new Color(1f, 1f, 1f, lineAlpha * intensity * fade);
            }

            // ---- drift meter ----
            bool drifting = racing && car.IsDrifting;
            bool showMeter = racing && (drifting || boostFlash > 0f);
            if (driftMeter.gameObject.activeSelf != showMeter) driftMeter.gameObject.SetActive(showMeter);
            if (!drifting)
            {
                shownStage = -1;
                if (showMeter) driftLabel.text = Loc.Format("drift.released", "n", releasedStage);
                return;
            }
            float charge = car.DriftCharge01;
            driftFill.rectTransform.anchorMax = new Vector2(charge, 1f);
            driftFill.rectTransform.offsetMax = new Vector2(2f - 4f * charge, -2f) / pixelScale;
            int stage = car.DriftStage;
            if (stage != shownStage)
            {
                shownStage = stage;
                driftLabel.text = stage > 0 ? Loc.Format("drift.release", "n", stage) : Loc.Get("drift.hold");
                driftFill.color = StageColors[Mathf.Clamp(stage, 0, 3)];
                for (int i = 0; i < 3; i++) driftPips[i].color = i < stage ? StageColors[i + 1] : new Color(1f, 1f, 1f, 0.35f);
                if (stage > 0) SfxPlayer.Instance.PlayUi(SfxSynth.Beep, 0.25f + 0.1f * stage, 0.9f + 0.2f * stage);
            }
        }

        private void OnDestroy() { if (car != null) car.DriftBoosted -= OnDrift; }
    }
}
