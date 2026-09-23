using System.Text;
using TMPro;
using UnityEngine;
using RacingGame.Core.Platform;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace RacingGame
{
    /// <summary>
    /// Runtime-built race HUD: speed, HP, lap, position, timer, countdown, wrong-way warning,
    /// onboarding hints for the first half minute, pause overlay and the results panel.
    /// Subscribes to <see cref="RaceManager"/> events; per-frame work is a few cached labels.
    /// TODO(M7): move the layout to a prefab and route every string through localization.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class HudScreen : MonoBehaviour
    {
        public RaceManager race;
        public float hintSeconds = 30f;

        private TextMeshProUGUI speedText, lapText, positionText, timerText, centerText, hintText, hpText, pauseText, pauseHint, ammoText, hitMarker;
        private CarCannon playerCannon;
        private float hitMarkerUntil;
        private Image hpFill, hpTrail, hpIcon;
        private Sprite healthFillSprite;
        private RectTransform speedPanel, healthPanel, hintPanel, ammoPanel, eventPanel, centerPanel, pauseButton;
        private Button resumeButton, restartButton, menuButton;
        private CanvasGroup pauseControls;
        private string shownCenter;
        private SpeedometerGraphic speedDial;
        private TextMeshProUGUI speedUnit, nitroText;
        private float shownSpeed, damageTrailUntil;
        private static readonly Color HealthyColor = new Color(0.22f, 0.9f, 0.68f);
        private static readonly Color CriticalColor = new Color(1f, 0.3f, 0.28f);
        private const float HudPanelAlpha = 0.96f;

        [Header("Reticle")]
        [Tooltip("Where the reticle points: this far ahead of the muzzle along the shell's flight (m).")]
        public float aimDistance = 35f;
        [Tooltip("Cone (degrees) in which a rival counts as locked; matches the AI gunner by default.")]
        public float lockConeDegrees = 9f;
        public float lockMinRange = 6f, lockMaxRange = 45f;
        private static readonly Color ReticleIdle = new Color(1f, 1f, 1f, 0.85f), ReticleLockedColor = new Color(1f, 0.25f, 0.2f, 1f);
        private RectTransform hudRoot, reticle;
        private readonly Image[] reticleArms = new Image[8];
        private Image[] ammoPips = new Image[0];
        private float reticlePunch;
        private int lockCheckFrame;
        [Header("Event toast / low HP")]
        public float toastSeconds = 1.4f;
        [Range(0f, 1f)] public float lowHealthFraction = 0.3f;
        private TextMeshProUGUI eventText;
        private float eventUntil, eventPunch;
        private readonly Image[] lowHpEdges = new Image[4];
        private float nextHeartbeat;
        /// <summary>Last text shown by <see cref="ShowEvent"/> (test hook).</summary>
        public string LastEvent { get; private set; }
        public bool LowHealthWarning { get; private set; }

        /// <summary>True while a rival is inside the lock cone with a clear line of fire.</summary>
        public bool ReticleLocked { get; private set; }
        private bool touchLayout;
        private float layoutWidth = -1f;
        private RectTransform pausePanel;
        private CarController playerCar;
        private CarHealth playerHealth;
        private float hintUntil;
        private readonly StringBuilder sb = new StringBuilder(256);
        // Per-frame labels go through TMP_Text.SetText(format, number): no string garbage while racing.
        private string reloadFormat = "{0:0}%";
        private const string TimerFormat = "{0:0}:{1:00}.{2:000}";
        private int lastSpeed = -1, lastReloadPct = -1;
        private bool lastBoost;
        private long lastTimerMs = -1;

        private void Awake()
        {
            if (race == null) race = GetComponent<RaceManager>() ?? FindAnyObjectByType<RaceManager>();
            Build();
        }

        private void OnEnable()
        {
            if (race == null) return;
            race.OnCountdownTick += OnCountdown;
            race.OnRaceStarted += OnStarted;
            race.OnRaceFinished += OnFinished;
            race.OnPositionsChanged += RefreshPosition;
            PauseController.PausedChanged += OnPaused;
            TouchControlsPresenter.ShownChanged += OnTouchShown;
            if (touchLayout != TouchControlsPresenter.IsShown) OnTouchShown(TouchControlsPresenter.IsShown);
        }

        private void OnDisable()
        {
            if (race == null) return;
            race.OnCountdownTick -= OnCountdown;
            race.OnRaceStarted -= OnStarted;
            race.OnRaceFinished -= OnFinished;
            race.OnPositionsChanged -= RefreshPosition;
            PauseController.PausedChanged -= OnPaused;
            TouchControlsPresenter.ShownChanged -= OnTouchShown;
            Loc.LanguageChanged -= ApplyStaticTexts;
            var director = RaceDirector.Current; if (director != null) director.ShellHit -= OnShellHit;
        }

        private void Build()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                es.transform.SetParent(transform, false);
            }

            var canvas = UiFactory.CreateOverlayCanvas("HUD");
            canvas.transform.SetParent(transform, false);
            var root = canvas.transform;
            hudRoot = (RectTransform)root;

            BuildSpeedometer(root);
            hintPanel = UiFactory.Card(root, "HintsPanel", Vector2.zero, Vector2.zero, new Vector2(30f, 272f), new Vector2(680f, 422f), HudPanelAlpha);
            hintText = UiFactory.Text(hintPanel, "Hints", "", 24f, TextAlignmentOptions.MidlineLeft, Vector2.zero, Vector2.one, new Vector2(18f, 14f), new Vector2(-18f, -14f));

            BuildHealthBar(root);
            ammoPanel = UiFactory.Card(root, "AmmoPanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-354f, 138f), new Vector2(-30f, 186f), HudPanelAlpha);
            ammoText = UiFactory.Text(ammoPanel, "Ammo", "", 30f, TextAlignmentOptions.MidlineRight, Vector2.zero, Vector2.one, new Vector2(18f, 4f), new Vector2(-18f, -4f), new Color(1f, 0.85f, 0.3f));
            ammoText.enableAutoSizing = true; ammoText.fontSizeMin = 18f; ammoText.fontSizeMax = 30f;
            BuildReticle(root);
            eventPanel = UiFactory.Card(root, "EventPanel", new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(-340f, -40f), new Vector2(340f, 40f));
            eventText = UiFactory.Text(eventPanel, "Event", "", 36f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(24f, 10f), new Vector2(-24f, -10f), UiFactory.Accent);
            eventText.enableAutoSizing = true; eventText.fontSizeMin = 22f; eventText.fontSizeMax = 36f;
            eventPanel.gameObject.SetActive(false);
            // Four thin strips on the screen edges instead of a full-screen vignette (no extra overdraw).
            var red = new Color(0.9f, 0.05f, 0.05f, 0f);
            lowHpEdges[0] = UiFactory.Panel(root, "LowHpL", new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(16f, 0f), red, roundedCorners: false).GetComponent<Image>();
            lowHpEdges[1] = UiFactory.Panel(root, "LowHpR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-16f, 0f), Vector2.zero, red, roundedCorners: false).GetComponent<Image>();
            lowHpEdges[2] = UiFactory.Panel(root, "LowHpT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -16f), Vector2.zero, red, roundedCorners: false).GetComponent<Image>();
            lowHpEdges[3] = UiFactory.Panel(root, "LowHpB", new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 16f), red, roundedCorners: false).GetComponent<Image>();
            foreach (var e in lowHpEdges) { e.raycastTarget = false; e.gameObject.SetActive(false); }
            hitMarker = UiFactory.Text(reticle, "HitMarker", "×", 64f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, -60f), new Vector2(60f, 60f), new Color(1f, 0.35f, 0.2f));
            hitMarker.gameObject.SetActive(false);

            // Top-left: lap + position
            var racePanel = UiFactory.Card(root, "RaceStatus", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -174f), new Vector2(310f, -24f), HudPanelAlpha);
            UiFactory.Panel(racePanel, "Accent", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 18f), new Vector2(3f, -18f), UiFactory.Accent, false);
            lapText = UiFactory.Text(racePanel, "Lap", "LAP 1/3", 36f, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -70f), new Vector2(-18f, -12f));
            positionText = UiFactory.Text(racePanel, "Position", "P1/4", 36f, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -138f), new Vector2(-18f, -80f));
            lapText.fontSize = 28f; lapText.color = UiFactory.Muted;
            positionText.fontStyle = FontStyles.Bold;
            lapText.enableAutoSizing = positionText.enableAutoSizing = true;
            lapText.fontSizeMin = positionText.fontSizeMin = 20f;
            lapText.fontSizeMax = 28f; positionText.fontSizeMax = 36f;

            // Top-centre: timer
            var timerPanel = UiFactory.Card(root, "TimerPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-140f, -94f), new Vector2(140f, -24f), HudPanelAlpha);
            timerText = UiFactory.Text(timerPanel, "Timer", "0:00.000", 40f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(16f, 10f), new Vector2(-16f, -10f));

            // Centre: countdown / wrong way / messages
            centerPanel = UiFactory.Card(root, "CenterPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120f, -100f), new Vector2(120f, 100f));
            centerText = UiFactory.Text(centerPanel, "Center", "", 120f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(24f, 8f), new Vector2(-24f, -8f), UiFactory.Accent);
            centerText.fontStyle = FontStyles.Bold;
            centerText.enableAutoSizing = true; centerText.fontSizeMin = 38f; centerText.fontSizeMax = 140f;
            centerPanel.gameObject.SetActive(false);

            BuildPause(root);

            hintUntil = Time.unscaledTime + hintSeconds;
            Loc.LanguageChanged += ApplyStaticTexts;
            ApplyStaticTexts();
        }

        private void BuildPause(Transform root)
        {
            pauseButton = (RectTransform)UiFactory.Button(root, "PauseButton", "", () => PauseController.Set(PauseReason.User, true), Vector2.one, Vector2.one, new Vector2(-106f, -100f), new Vector2(-30f, -24f), 28f, UiFactory.Surface).transform;
            for (int i = 0; i < 2; i++) UiFactory.Panel(pauseButton, "Bar" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-14f + 19f * i, -16f), new Vector2(-5f + 19f * i, 16f), UiFactory.Accent, false);
            pausePanel = UiFactory.Panel(root, "Pause", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.01f, 0.02f, 0.04f, 0.8f), false);
            pausePanel.GetComponent<Image>().raycastTarget = true;
            // Above the touch-control canvas, including its raycaster.
            var overlay = pausePanel.gameObject.AddComponent<Canvas>(); overlay.overrideSorting = true; overlay.sortingOrder = 20;
            pausePanel.gameObject.AddComponent<GraphicRaycaster>();
            var card = UiFactory.Card(pausePanel, "PauseCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300f, -300f), new Vector2(300f, 300f));
            pauseControls = card.gameObject.AddComponent<CanvasGroup>();
            UiFactory.Panel(card, "Accent", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(112f, -36f), UiFactory.Accent, false);
            pauseText = UiFactory.Text(card, "PauseText", "", 56f, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -140f), new Vector2(-40f, -58f));
            pauseText.fontStyle = FontStyles.Bold;
            pauseText.enableAutoSizing = true; pauseText.fontSizeMin = 32f; pauseText.fontSizeMax = 56f;
            var flow = GetComponent<GameFlow>();
            resumeButton = UiFactory.Button(card, "Resume", "", () => PauseController.Set(PauseReason.User, false), new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -266f), new Vector2(-40f, -180f), 32f);
            restartButton = UiFactory.Button(card, "Restart", "", () => flow.RetryRace(), new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -366f), new Vector2(-40f, -286f), 30f, UiFactory.Raised);
            menuButton = UiFactory.Button(card, "MainMenu", "", () => flow.EnterMenu(), new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -466f), new Vector2(-40f, -386f), 30f, UiFactory.Raised);
            pauseHint = UiFactory.Text(card, "Hint", "", 23f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(1f, 0f), new Vector2(40f, 40f), new Vector2(-40f, 90f), UiFactory.Muted);
            pausePanel.gameObject.SetActive(false);
        }

        private void BuildSpeedometer(Transform root)
        {
            speedPanel = UiFactory.Card(root, "Speedometer", Vector2.zero, Vector2.zero, new Vector2(30f, 30f), new Vector2(274f, 254f), HudPanelAlpha);
            var dial = UiFactory.Panel(speedPanel, "Dial", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            speedDial = dial.gameObject.AddComponent<SpeedometerGraphic>();
            speedDial.raycastTarget = false;
            speedDial.SetSpeed(0f, false);
            for (int i = 0; i <= 4; i++)
            {
                float angle = Mathf.Lerp(200f, -20f, i / 4f) * Mathf.Deg2Rad;
                Vector2 p = new Vector2(122f, 92f) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 71f;
                var label = UiFactory.Text(speedPanel, "Scale" + i, (i * SpeedometerGraphic.MaxSpeed / 4f).ToString("0"), 16f,
                    TextAlignmentOptions.Center, Vector2.zero, Vector2.zero, p - new Vector2(20f, 11f), p + new Vector2(20f, 11f), new Color(0.82f, 0.88f, 0.94f));
                label.outlineWidth = 0f;
            }
            speedText = UiFactory.Text(speedPanel, "Speed", "0", 46f, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.zero, new Vector2(74f, 32f), new Vector2(170f, 80f));
            speedText.fontStyle = FontStyles.Bold;
            speedText.outlineWidth = 0f;
            speedUnit = UiFactory.Text(speedPanel, "Unit", "", 16f, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.zero, new Vector2(62f, 12f), new Vector2(182f, 35f), new Color(0.82f, 0.88f, 0.94f));
            nitroText = UiFactory.Text(speedPanel, "Nitro", "", 16f, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.zero, new Vector2(52f, 199f), new Vector2(192f, 222f), SpeedometerGraphic.BoostColor);
            nitroText.gameObject.SetActive(false);
        }

        private void BuildHealthBar(Transform root)
        {
            healthPanel = UiFactory.Card(root, "Health", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-354f, 30f), new Vector2(-30f, 130f), HudPanelAlpha);
            var icon = UiFactory.Panel(healthPanel, "HealthIcon", Vector2.zero, Vector2.zero, new Vector2(18f, 53f), new Vector2(48f, 83f), HealthyColor);
            hpIcon = icon.GetComponent<Image>();
            var ink = new Color(0.03f, 0.12f, 0.14f);
            UiFactory.Panel(icon, "CrossH", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-9f, -2f), new Vector2(9f, 2f), ink, false);
            UiFactory.Panel(icon, "CrossV", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-2f, -9f), new Vector2(2f, 9f), ink, false);
            hpText = UiFactory.Text(healthPanel, "HpText", "", 26f, TextAlignmentOptions.MidlineRight,
                Vector2.zero, Vector2.one, new Vector2(60f, 48f), new Vector2(-18f, -12f));
            hpText.fontStyle = FontStyles.Bold;
            hpText.outlineWidth = 0f;
            hpText.enableAutoSizing = true; hpText.fontSizeMin = 16f; hpText.fontSizeMax = 26f;
            var track = UiFactory.Panel(healthPanel, "HpBar", Vector2.zero, new Vector2(1f, 0f), new Vector2(18f, 20f), new Vector2(-18f, 40f), new Color(0.14f, 0.2f, 0.27f), false);
            // Filled Images need a sprite; a white texel keeps this thin bar rectangular.
            healthFillSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            hpTrail = UiFactory.Panel(track, "DamageTrail", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(1f, 0.72f, 0.3f), false).GetComponent<Image>();
            hpTrail.sprite = healthFillSprite;
            hpTrail.type = Image.Type.Filled; hpTrail.fillMethod = Image.FillMethod.Horizontal;
            hpFill = UiFactory.Panel(track, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, HealthyColor, false).GetComponent<Image>();
            hpFill.sprite = healthFillSprite;
            hpFill.type = Image.Type.Filled; hpFill.fillMethod = Image.FillMethod.Horizontal;
            for (int i = 1; i < 10; i++)
                UiFactory.Panel(track, "Divider" + i, new Vector2(i / 10f, 0f), new Vector2(i / 10f, 1f), new Vector2(-1f, 0f), new Vector2(1f, 0f), new Color(0.03f, 0.05f, 0.09f, 0.85f), false);
        }

        private void ApplyStaticTexts()
        {
            ApplyLayout(touchLayout);
            pauseText.text = Loc.Get("pause.title"); pauseHint.text = Loc.Get("pause.hint");
            resumeButton.GetComponentInChildren<TextMeshProUGUI>().text = Loc.Get("pause.resume");
            restartButton.GetComponentInChildren<TextMeshProUGUI>().text = Loc.Get("pause.restart");
            menuButton.GetComponentInChildren<TextMeshProUGUI>().text = Loc.Get("pause.menu");
            speedUnit.text = Loc.Get("hud.speed_unit");
            nitroText.text = Loc.Get("hud.nitro");
            reloadFormat = Loc.Get("hud.reload").Replace("{p}", "{0:0}");
            lastSpeed = -1; lastReloadPct = -1; lastTimerMs = -1;
            RefreshPosition();
            if (playerHealth != null) OnHealth(playerHealth.Health, playerHealth.maxHealth);
            if (playerCannon != null) OnAmmo(playerCannon.Ammo, playerCannon.AmmoMax);
        }

        // Four corner brackets around the aim point; ammo pips underneath. Plain images, no glyphs.
        private void BuildReticle(Transform root)
        {
            reticle = UiFactory.Panel(root, "Reticle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-36f, -36f), new Vector2(36f, 36f), new Color(0f, 0f, 0f, 0f), roundedCorners: false);
            reticle.GetComponent<Image>().raycastTarget = false;
            const float len = 18f, thick = 4f, half = 36f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) == 0 ? -1f : 1f, sy = (i & 2) == 0 ? -1f : 1f;
                var corner = UiFactory.Panel(reticle, "Corner" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f), roundedCorners: false);
                corner.GetComponent<Image>().raycastTarget = false;
                // Horizontal and vertical arm of the bracket, both children of the corner so they recolour together.
                var h = UiFactory.Panel(corner, "H", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(sx > 0 ? half - len : -half, sy > 0 ? half - thick : -half), new Vector2(sx > 0 ? half : -half + len, sy > 0 ? half : -half + thick), ReticleIdle, roundedCorners: false);
                var v = UiFactory.Panel(corner, "V", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(sx > 0 ? half - thick : -half, sy > 0 ? half - len : -half), new Vector2(sx > 0 ? half : -half + thick, sy > 0 ? half : -half + len), ReticleIdle, roundedCorners: false);
                reticleArms[i * 2] = h.GetComponent<Image>(); reticleArms[i * 2 + 1] = v.GetComponent<Image>();
                reticleArms[i * 2].raycastTarget = false; reticleArms[i * 2 + 1].raycastTarget = false;
            }
            reticle.gameObject.SetActive(false);
        }

        private void BuildAmmoPips(int count)
        {
            foreach (var pip in ammoPips) if (pip != null) Destroy(pip.gameObject);
            count = Mathf.Clamp(count, 0, 12);
            ammoPips = new Image[count];
            const float w = 10f, gap = 5f;
            float x0 = -(count * w + (count - 1) * gap) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                float x = x0 + i * (w + gap);
                var rt = UiFactory.Panel(reticle, "Pip" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, -58f), new Vector2(x + w, -48f), ReticleIdle, roundedCorners: false);
                ammoPips[i] = rt.GetComponent<Image>();
                ammoPips[i].raycastTarget = false;
            }
        }

        private void ClearLock()
        {
            if (ReticleLocked) { ReticleLocked = false; SetReticleColor(ReticleIdle); }
            reticlePunch = 0f;
        }

        private void SetReticleColor(Color c)
        {
            for (int i = 0; i < reticleArms.Length; i++) if (reticleArms[i] != null) reticleArms[i].color = c;
        }

        // Where the shell will be after aimDistance: muzzle, launch velocity (barrel + car), gravity drop.
        private void UpdateReticle()
        {
            bool show = playerCar != null && playerCannon != null && playerCannon.muzzle != null && race.State >= RaceState.Countdown && race.State < RaceState.Finished
                        && (playerHealth == null || !playerHealth.IsDead);
            var cam = Camera.main;
            if (!show || cam == null) { if (reticle.gameObject.activeSelf) reticle.gameObject.SetActive(false); ClearLock(); return; }

            var muzzle = playerCannon.muzzle;
            Vector3 velocity = muzzle.forward * playerCannon.shellSpeed + playerCar.Body.linearVelocity;
            float t = aimDistance / Mathf.Max(1f, playerCannon.shellSpeed);
            Vector3 aim = muzzle.position + velocity * t + 0.5f * t * t * Physics.gravity;
            Vector3 sp = cam.WorldToScreenPoint(aim);
            if (sp.z <= 0f) { reticle.gameObject.SetActive(false); ClearLock(); return; }
            if (!reticle.gameObject.activeSelf) reticle.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, sp, null, out Vector2 local);
            reticle.anchoredPosition = local;

            if (++lockCheckFrame >= 5)
            {
                lockCheckFrame = 0;
                bool locked = TargetFinder.Find(playerCar, muzzle.position, muzzle.forward, lockConeDegrees, lockMinRange, lockMaxRange) != null;
                if (locked != ReticleLocked)
                {
                    ReticleLocked = locked;
                    SetReticleColor(locked ? ReticleLockedColor : ReticleIdle);
                    if (locked) reticlePunch = 1f;
                }
            }
            reticlePunch = Mathf.MoveTowards(reticlePunch, 0f, Time.unscaledDeltaTime * 6f);
            float scale = 1f + 0.18f * reticlePunch;
            reticle.localScale = new Vector3(scale, scale, 1f);

            // Pips: bright = loaded shells; while reloading they fill up one by one.
            int max = ammoPips.Length;
            if (max > 0)
            {
                int loaded = playerCannon.IsReloading ? Mathf.FloorToInt(playerCannon.ReloadProgress(SimulationDriver.Instance.Tick) * max) : Mathf.Min(max, playerCannon.Ammo);
                for (int i = 0; i < max; i++)
                {
                    var c = i < loaded ? (playerCannon.IsReloading ? new Color(1f, 1f, 1f, 0.35f) : ReticleIdle) : new Color(0f, 0f, 0f, 0.35f);
                    if (ammoPips[i].color != c) ammoPips[i].color = c;
                }
            }
        }

        private void OnTouchShown(bool shown)
        {
            touchLayout = shown;
            ApplyLayout(shown);
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchor; rt.anchorMax = anchor;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }

        /// <summary>
        /// Keyboard/gamepad: speed bottom-left, HP and ammo bottom-right. Touch: the on-screen buttons own
        /// both bottom corners (steer 40–420 × 40–200 left; Fire/Brake −480…−40 × 40–260 right, Reset top-right),
        /// so speed sits above the steer pair and HP/ammo move to the top-right, left of Reset.
        /// Offsets are in the 1920×1080 reference units of <see cref="UiFactory.CreateOverlayCanvas"/>.
        /// </summary>
        private void ApplyLayout(bool touch)
        {
            if (speedText == null) return;
            hintText.text = Loc.Get(touch ? "hud.hint.touch" : "hud.hint.keys");
            layoutWidth = hudRoot != null ? hudRoot.rect.width : 1920f;
            // Portrait phones give the canvas ~1000 reference units of width: the timer (±200 around the
            // centre) would collide with a top-right HP bar, so HP/ammo drop to a row below it.
            bool narrow = layoutWidth < 1400f;
            if (touch)
            {
                Place(pauseButton, Vector2.one, new Vector2(-140f, -254f), new Vector2(-40f, -154f));
                Place(speedPanel, Vector2.zero, new Vector2(40f, 224f), new Vector2(284f, 448f));
                float row = narrow ? -90f : 0f;
                Place(healthPanel, Vector2.one, new Vector2(-494f, -134f + row), new Vector2(-170f, -34f + row));
                Place(ammoPanel, Vector2.one, new Vector2(-494f, -186f + row), new Vector2(-170f, -138f + row));
                Place(hintPanel, new Vector2(0.5f, 1f), new Vector2(-460f, -322f + row), new Vector2(420f, -202f + row));
                hintText.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                Place(pauseButton, Vector2.one, new Vector2(-106f, -100f), new Vector2(-30f, -24f));
                Place(speedPanel, Vector2.zero, new Vector2(30f, 30f), new Vector2(274f, 254f));
                Place(healthPanel, new Vector2(1f, 0f), new Vector2(-354f, 30f), new Vector2(-30f, 130f));
                Place(ammoPanel, new Vector2(1f, 0f), new Vector2(-354f, 138f), new Vector2(-30f, 186f));
                Place(hintPanel, Vector2.zero, new Vector2(30f, 272f), new Vector2(680f, 422f));
                hintText.alignment = TextAlignmentOptions.MidlineLeft;
            }
            pauseHint.gameObject.SetActive(!touch);
        }

        /// <summary>Test hook: current layout mode.</summary>
        public bool TouchLayout => touchLayout;

        private void Update()
        {
            if (race == null) return;
            if (hudRoot != null && Mathf.Abs(hudRoot.rect.width - layoutWidth) > 1f) ApplyLayout(touchLayout);   // rotation / resize
            if (playerCar == null && race.Player != null)
            {
                playerCar = race.Player.Car;
                if (playerHealth != null) playerHealth.OnHealthChanged -= OnHealth;
                if (playerCannon != null) playerCannon.AmmoChanged -= OnAmmo;
                playerHealth = race.Player.Health;
                playerCannon = playerCar.GetComponent<CarCannon>();
                if (playerCannon != null) { playerCannon.AmmoChanged += OnAmmo; BuildAmmoPips(playerCannon.AmmoMax); OnAmmo(playerCannon.Ammo, playerCannon.AmmoMax); }
                RaceDirector.Instance.ShellHit -= OnShellHit;
                RaceDirector.Instance.ShellHit += OnShellHit;
                if (playerHealth != null) playerHealth.OnHealthChanged += OnHealth;
                OnHealth(playerHealth != null ? playerHealth.Health : 100f, playerHealth != null ? playerHealth.maxHealth : 100f);
                RefreshPosition();
            }

            if (playerCar != null)
            {
                int speed = Mathf.RoundToInt(Mathf.Abs(playerCar.SpeedKmh));
                bool boost = playerCar.IsBoosting;
                if (speed != lastSpeed) { lastSpeed = speed; speedText.SetText("{0:0}", speed); }
                if (boost != lastBoost) { lastBoost = boost; nitroText.gameObject.SetActive(boost); }
                shownSpeed = Mathf.Lerp(shownSpeed, Mathf.Abs(playerCar.SpeedKmh), 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                speedDial.SetSpeed(shownSpeed, boost);
            }
            if (Time.unscaledTime >= damageTrailUntil && hpTrail.fillAmount > hpFill.fillAmount)
                hpTrail.fillAmount = Mathf.MoveTowards(hpTrail.fillAmount, hpFill.fillAmount, Time.unscaledDeltaTime * 0.6f);
            if (playerCannon != null && playerCannon.IsReloading)
            {
                int pct = Mathf.RoundToInt(playerCannon.ReloadProgress(SimulationDriver.Instance.Tick) * 100f);
                if (pct != lastReloadPct) { lastReloadPct = pct; ammoText.SetText(reloadFormat, pct); }
            }
            if (hitMarker.gameObject.activeSelf && Time.unscaledTime > hitMarkerUntil) hitMarker.gameObject.SetActive(false);
            if (race.State >= RaceState.Racing) SetTimer(race.RaceTime);

            if (hintPanel.gameObject.activeSelf && Time.unscaledTime > hintUntil) hintPanel.gameObject.SetActive(false);

            if (race.State == RaceState.Racing && race.Player != null && race.Player.Progress != null)
            {
                bool wrong = race.Player.Progress.WrongWay;
                string wrongText = Loc.Get("hud.wrong_way");
                if (wrong && centerText.text != wrongText) { centerText.fontSize = 80f; centerText.color = new Color(1f, 0.3f, 0.2f); centerText.text = wrongText; }
                else if (!wrong && centerText.text == wrongText) centerText.text = "";
            }
        }

        private void SetTimer(double seconds)
        {
            long ms = seconds > 0d ? (long)(seconds * 1000d) : 0;
            if (ms == lastTimerMs) return;
            lastTimerMs = ms;
            long m = ms / 60000;
            long rest = ms - m * 60000;
            timerText.SetText(TimerFormat, m, rest / 1000, rest % 1000);
        }

        /// <summary>Contextual tip in the hint card (replaces the controls hint) for a few seconds.</summary>
        public void ShowHint(string text, float seconds)
        {
            if (hintText == null || string.IsNullOrEmpty(text)) return;
            LastHint = text;
            hintText.text = text;
            hintPanel.gameObject.SetActive(true);
            hintUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Last text shown by <see cref="ShowHint"/> (test hook).</summary>
        public string LastHint { get; private set; }

        /// <summary>How long the controls card stays up at the start of a race: long for newcomers, a reminder later.</summary>
        private float ControlsHintSeconds => PlatformGlue.Save != null && PlatformGlue.Save.Data.Races >= 3 ? 8f : hintSeconds;

        /// <summary>Short centred message (kill, death, pickup). The newest replaces the current one.</summary>
        public void ShowEvent(string text, Color color)
        {
            if (eventText == null || string.IsNullOrEmpty(text)) return;
            LastEvent = text;
            eventText.text = text;
            eventText.color = color;
            eventPanel.gameObject.SetActive(true);
            eventUntil = Time.unscaledTime + toastSeconds;
            eventPunch = 1f;
        }

        private void UpdateToastAndWarnings()
        {
            if (eventPanel.gameObject.activeSelf)
            {
                if (Time.unscaledTime > eventUntil) eventPanel.gameObject.SetActive(false);
                eventPunch = Mathf.MoveTowards(eventPunch, 0f, Time.unscaledDeltaTime * 5f);
                float sc = 1f + 0.06f * eventPunch * eventPunch;
                eventPanel.localScale = new Vector3(sc, sc, 1f);
            }

            bool low = playerHealth != null && !playerHealth.IsDead && race.State >= RaceState.Racing && race.State < RaceState.Finished
                       && playerHealth.maxHealth > 0f && playerHealth.Health / playerHealth.maxHealth < lowHealthFraction && !PauseController.IsPaused;
            if (low != LowHealthWarning)
            {
                LowHealthWarning = low;
                foreach (var e in lowHpEdges) e.gameObject.SetActive(low);
                if (!low && playerHealth != null) OnHealth(playerHealth.Health, playerHealth.maxHealth);   // restore the bar colour
                nextHeartbeat = 0f;
            }
            if (!low) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * 1.4f);
            var c = new Color(0.9f, 0.05f, 0.05f, 0.15f + 0.3f * pulse);
            foreach (var e in lowHpEdges) e.color = c;
            var fc = hpFill.color; fc.a = 0.55f + 0.45f * pulse; hpFill.color = fc;
            if (Time.unscaledTime >= nextHeartbeat) { nextHeartbeat = Time.unscaledTime + 0.85f; SfxPlayer.Instance.PlayUi(SfxSynth.Heartbeat, 0.55f); }
        }

        // After CarCameraFollow.LateUpdate (execution order below), so the reticle matches this frame's camera.
        private void LateUpdate()
        {
            if (countdownPunch > 0f)
            {
                // Each digit lands big and settles: 1.6× → 1× over ~0.4 s.
                countdownPunch = Mathf.MoveTowards(countdownPunch, 0f, Time.unscaledDeltaTime * 2.5f);
                float k = 1f + 0.6f * countdownPunch * countdownPunch;
                centerPanel.localScale = new Vector3(k, k, 1f);
            }
            if (race != null) { UpdateReticle(); UpdateToastAndWarnings(); }
            if (shownCenter != centerText.text)
            {
                shownCenter = centerText.text;
                centerPanel.gameObject.SetActive(!string.IsNullOrEmpty(shownCenter));
                centerPanel.sizeDelta = new Vector2(Mathf.Clamp(centerText.preferredWidth + 64f, 200f, Mathf.Min(900f, hudRoot.rect.width - 80f)), 190f);
            }
            if (pausePanel.gameObject.activeSelf)
                pauseControls.interactable = (PauseController.Reasons & ~PauseReason.User) == PauseReason.None;
        }

        private void OnAmmo(int ammo, int max)
        {
            lastReloadPct = -1;
            ammoPanel.gameObject.SetActive(max > 0);
            if (max <= 0) { ammoText.text = ""; return; }
            ammoText.text = ammo > 0 ? Loc.Format("hud.ammo", "cur", ammo, "max", max) : Loc.Format("hud.reload", "p", 0);
        }

        private void OnShellHit(GameObject shooter, GameObject victim)
        {
            if (playerCar == null || shooter != playerCar.gameObject) return;
            hitMarker.gameObject.SetActive(true);
            hitMarkerUntil = Time.unscaledTime + 0.15f;
        }

        private void OnHealth(float current, float max)
        {
            float t = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            if (t < hpFill.fillAmount) damageTrailUntil = Time.unscaledTime + 0.35f;
            else if (t > hpFill.fillAmount) hpTrail.fillAmount = t;
            hpFill.fillAmount = t;
            hpFill.color = t <= lowHealthFraction ? CriticalColor : Color.Lerp(SpeedometerGraphic.Accent, HealthyColor, Mathf.InverseLerp(lowHealthFraction, 1f, t));
            hpIcon.color = hpFill.color;
            hpText.text = current <= 0f ? Loc.Get("hud.wrecked") : Loc.Format("hud.hp", "hp", Mathf.CeilToInt(current));
        }

        private void RefreshPosition()
        {
            if (race.Player == null) return;
            var progress = race.Player.Progress;
            int lap = Mathf.Clamp(progress.LapsCompleted + 1, 1, race.laps);
            lapText.text = Loc.Format("hud.lap", "cur", lap, "total", race.laps);
            positionText.text = Loc.Format("hud.position", "pos", race.PositionOf(race.Player), "n", race.Participants.Count);
        }

        private static readonly Color[] CountdownColors = { new Color(0.45f, 1f, 0.5f), new Color(1f, 0.9f, 0.3f), new Color(1f, 0.62f, 0.2f), new Color(1f, 0.32f, 0.25f) };
        private float countdownPunch;

        private void OnCountdown(int value)
        {
            centerText.fontSize = 140f; centerText.color = CountdownColors[Mathf.Clamp(value, 0, CountdownColors.Length - 1)];
            countdownPunch = 1f;
            centerText.text = value > 0 ? value.ToString() : Loc.Get("hud.go");
            if (value == 0) Invoke(nameof(ClearCenter), 1f);
        }

        private void ClearCenter() { if (centerText.text == Loc.Get("hud.go")) centerText.text = ""; }

        private void OnStarted() { RefreshPosition(); }

        private void OnFinished(RaceResults results) { centerText.text = ""; }

        /// <summary>Menu screens hide the HUD; the race shows it.</summary>
        public void SetVisible(bool visible)
        {
            foreach (var c in GetComponentsInChildren<Canvas>(true)) if (c.name == "HUD") c.gameObject.SetActive(visible);
            pausePanel.gameObject.SetActive(visible && PauseController.IsPaused);
            if (visible)
            {
                hintText.text = Loc.Get(touchLayout ? "hud.hint.touch" : "hud.hint.keys");
                hintUntil = Time.unscaledTime + ControlsHintSeconds; hintPanel.gameObject.SetActive(true); playerCar = null;
            }
            if (eventPanel != null) eventPanel.gameObject.SetActive(false);
        }

        private void OnPaused(bool paused)
        {
            pausePanel.gameObject.SetActive(paused);
            if (paused) EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
            else EventSystem.current?.SetSelectedGameObject(null);
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.OnHealthChanged -= OnHealth;
            if (playerCannon != null) playerCannon.AmmoChanged -= OnAmmo;
            if (healthFillSprite != null) Destroy(healthFillSprite);
        }
    }
}
