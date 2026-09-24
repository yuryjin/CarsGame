using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using RacingGame.Core.Combat;
using RacingGame.Core.Platform;
using RacingGame.Core.Sim;
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
        private CarStatus playerStatus;
        private CarWeapons playerWeapons;
        private float hitMarkerUntil;
        private RectTransform goalsPanel;
        private TextMeshProUGUI goalsText, combatText, incomingArrow, incomingText;
        private readonly RaceResultEntry goalEntry = new RaceResultEntry();
        private float nextGoalRefresh, incomingUntil;
        private Vector3 incomingSource;

        private Image hpFill, hpTrail, hpIcon;
        private Sprite healthFillSprite;
        private RectTransform speedPanel, healthPanel, hintPanel, ammoPanel, secondaryPanel, statusPanel, eventPanel, centerPanel, pauseButton;
        private TextMeshProUGUI secondaryText, statusText;
        private string lastStatusStr;
        private bool lastStatusShield, lastStatusFrozen, lastStatusJammed, lastStatusPunctured, lastStatusSlowed;
        private int lastSecondaryAmmo = -1;
        private bool lastSecondaryReady;
        private SecondaryWeaponType lastSecondaryType = SecondaryWeaponType.None;
        private PrimaryWeaponType lastPrimaryType = PrimaryWeaponType.Cannon;
        private bool lastOverheated;
        private int lastHeatPct = -1;
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

        [Header("Threat Markers")]
        public float threatMaxRange = 75f;
        private sealed class ThreatMarkerWidget
        {
            public RectTransform root;
            public TextMeshProUGUI arrow;
            public TextMeshProUGUI distanceText;
        }
        private readonly ThreatMarkerWidget[] threatMarkers = new ThreatMarkerWidget[8];

        [Header("Kill Feed")]
        private readonly KillFeedBuffer killFeedBuffer = new KillFeedBuffer();
        private RectTransform killFeedPanel;
        private readonly RectTransform[] killFeedRows = new RectTransform[KillFeedBuffer.MaxEntries];
        private readonly TextMeshProUGUI[] killFeedTexts = new TextMeshProUGUI[KillFeedBuffer.MaxEntries];
        private readonly Image[] killFeedBgs = new Image[KillFeedBuffer.MaxEntries];
        private bool killFeedNamesCached;
        private static readonly Color HitmarkerNormalColor = new Color(1f, 0.35f, 0.2f);
        private static readonly Color HitmarkerKillColor = new Color(1f, 0.12f, 0.12f);

        [Header("Status Indicators")]
        private readonly StatusIndicatorWidget[] statusIndicators = new StatusIndicatorWidget[5];

        /// <summary>Dedicated status indicator widgets for active vehicle effects (test hook).</summary>
        public IReadOnlyList<StatusIndicatorWidget> StatusIndicators => statusIndicators;

        /// <summary>Access to the kill feed ring buffer (test hook).</summary>
        public KillFeedBuffer KillFeed => killFeedBuffer;

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
            var director = RaceDirector.Current != null ? RaceDirector.Current : RaceDirector.Instance;
            if (director != null)
            {
                director.OnKillRegistered -= OnKillRegistered;
                director.OnKillRegistered += OnKillRegistered;
            }
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
            var director = RaceDirector.Current;
            if (director != null)
            {
                director.OnKillRegistered -= OnKillRegistered;
            }
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

            secondaryPanel = UiFactory.Card(root, "SecondaryPanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-354f, 194f), new Vector2(-30f, 242f), HudPanelAlpha);
            secondaryText = UiFactory.Text(secondaryPanel, "Secondary", "", 24f, TextAlignmentOptions.MidlineRight, Vector2.zero, Vector2.one, new Vector2(18f, 4f), new Vector2(-18f, -4f), new Color(1f, 0.6f, 0.2f));
            secondaryText.enableAutoSizing = true; secondaryText.fontSizeMin = 16f; secondaryText.fontSizeMax = 24f;
            secondaryPanel.gameObject.SetActive(false);

            BuildStatusPanel(root);
            BuildReticle(root);
            BuildThreatMarkers(root);
            BuildKillFeed(root);
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
            hitMarker = UiFactory.Text(reticle, "HitMarker", "×", 64f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, -60f), new Vector2(60f, 60f), HitmarkerNormalColor);
            hitMarker.gameObject.SetActive(false);
            combatText = UiFactory.Text(reticle, "HitOutcome", "", 22f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-180f, 60f), new Vector2(180f, 100f));
            incomingArrow = UiFactory.Text(root, "IncomingDirection", "▲", 44f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-28f, -28f), new Vector2(28f, 28f), CriticalColor);
            incomingText = UiFactory.Text(root, "IncomingSource", "", 23f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260f, -150f), new Vector2(260f, -112f));
            incomingArrow.gameObject.SetActive(false); incomingText.gameObject.SetActive(false);
            goalsPanel = UiFactory.Card(root, "LiveGoals", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -342f), new Vector2(530f, -190f), HudPanelAlpha);
            goalsText = UiFactory.Text(goalsPanel, "Goals", "", 23f, TextAlignmentOptions.MidlineLeft,
                Vector2.zero, Vector2.one, new Vector2(16f, 8f), new Vector2(-16f, -8f));
            goalsText.enableAutoSizing = true; goalsText.fontSizeMin = 18f; goalsText.fontSizeMax = 23f;
            goalsText.raycastTarget = combatText.raycastTarget = incomingArrow.raycastTarget = incomingText.raycastTarget = false;
            goalsPanel.GetComponent<Image>().raycastTarget = false;

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

        private void BuildStatusPanel(Transform root)
        {
            statusPanel = UiFactory.Card(root, "StatusPanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-354f, 250f), new Vector2(-30f, 298f), HudPanelAlpha);
            statusText = UiFactory.Text(statusPanel, "Status", "", 22f, TextAlignmentOptions.MidlineRight, Vector2.zero, Vector2.one, new Vector2(18f, 4f), new Vector2(-18f, -4f), UiFactory.Accent);
            statusText.enableAutoSizing = true; statusText.fontSizeMin = 14f; statusText.fontSizeMax = 22f;
            statusText.raycastTarget = false;

            BuildStatusIndicator(0, StatusKind.Shield, new Color(0.25f, 0.88f, 0.82f), "◆", "hud.status.shield");
            BuildStatusIndicator(1, StatusKind.Frozen, new Color(0.4f, 0.8f, 1f), "✦", "hud.status.frozen");
            BuildStatusIndicator(2, StatusKind.Jammed, new Color(1f, 0.65f, 0f), "▲", "hud.status.jammed");
            BuildStatusIndicator(3, StatusKind.Punctured, new Color(1f, 0.27f, 0.27f), "●", "hud.status.punctured");
            BuildStatusIndicator(4, StatusKind.Slowed, new Color(0.6f, 0.35f, 1f), "▼", "hud.status.slowed");

            statusPanel.gameObject.SetActive(false);
        }

        private void BuildStatusIndicator(int index, StatusKind kind, Color themeColor, string glyph, string locKey)
        {
            var badge = UiFactory.Card(statusPanel, "Status_" + kind, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 0.85f);
            var bgImage = badge.GetComponent<Image>();
            if (bgImage != null)
            {
                bgImage.color = new Color(themeColor.r * 0.22f, themeColor.g * 0.22f, themeColor.b * 0.22f, 0.9f);
                bgImage.raycastTarget = false;
            }

            var glyphText = UiFactory.Text(badge, "Glyph", glyph, 22f, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(8f, 0f), new Vector2(30f, 0f), themeColor);
            glyphText.fontStyle = FontStyles.Bold;
            glyphText.raycastTarget = false;

            var labelText = UiFactory.Text(badge, "Label", "", 18f, TextAlignmentOptions.MidlineLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(32f, 0f), new Vector2(-6f, 0f), themeColor);
            labelText.fontStyle = FontStyles.Bold;
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 11f;
            labelText.fontSizeMax = 18f;
            labelText.raycastTarget = false;

            var widget = new StatusIndicatorWidget(badge, bgImage, glyphText, labelText, kind, themeColor, glyph, locKey);
            widget.RefreshLocalization();
            widget.SetActive(false);
            statusIndicators[index] = widget;
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
            if (statusIndicators != null)
            {
                for (int i = 0; i < statusIndicators.Length; i++)
                {
                    statusIndicators[i]?.RefreshLocalization();
                }
            }
            lastStatusStr = null;
            killFeedNamesCached = false;
            EnsureKillFeedNames();
            UpdateKillFeedDisplay();
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

        private void BuildThreatMarkers(Transform root)
        {
            var container = UiFactory.Panel(root, "ThreatMarkers", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear, false);
            for (int i = 0; i < threatMarkers.Length; i++)
            {
                var markerRt = UiFactory.Panel(container, "Threat" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-28f, -28f), new Vector2(28f, 28f), new Color(0.04f, 0.06f, 0.1f, 0.88f), true);
                var arrow = UiFactory.Text(markerRt, "Arrow", "▲", 24f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-16f, 2f), new Vector2(16f, 28f), Color.red);
                var dist = UiFactory.Text(markerRt, "Dist", "", 15f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-26f, -24f), new Vector2(26f, 4f), Color.white);
                dist.outlineWidth = 0.2f; arrow.outlineWidth = 0.2f;
                arrow.raycastTarget = false; dist.raycastTarget = false;
                markerRt.GetComponent<Image>().raycastTarget = false;
                threatMarkers[i] = new ThreatMarkerWidget { root = markerRt, arrow = arrow, distanceText = dist };
                markerRt.gameObject.SetActive(false);
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
            bool show = playerCar != null && playerCannon != null && !playerCannon.locked && playerCannon.muzzle != null && race.State >= RaceState.Countdown && race.State < RaceState.Finished
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
                Place(secondaryPanel, Vector2.one, new Vector2(-494f, -238f + row), new Vector2(-170f, -190f + row));
                Place(statusPanel, Vector2.one, new Vector2(-494f, -290f + row), new Vector2(-170f, -242f + row));
                Place(hintPanel, new Vector2(0.5f, 1f), new Vector2(-460f, -322f + row), new Vector2(420f, -202f + row));
                if (killFeedPanel != null) Place(killFeedPanel, new Vector2(0f, 1f), new Vector2(30f, -310f), new Vector2(360f, -190f));
                for (int i = 0; i < killFeedTexts.Length; i++) if (killFeedTexts[i] != null) killFeedTexts[i].alignment = TextAlignmentOptions.MidlineLeft;
                hintText.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                Place(pauseButton, Vector2.one, new Vector2(-106f, -100f), new Vector2(-30f, -24f));
                Place(speedPanel, Vector2.zero, new Vector2(30f, 30f), new Vector2(274f, 254f));
                Place(healthPanel, new Vector2(1f, 0f), new Vector2(-354f, 30f), new Vector2(-30f, 130f));
                Place(ammoPanel, new Vector2(1f, 0f), new Vector2(-354f, 138f), new Vector2(-30f, 186f));
                Place(secondaryPanel, new Vector2(1f, 0f), new Vector2(-354f, 194f), new Vector2(-30f, 242f));
                Place(statusPanel, new Vector2(1f, 0f), new Vector2(-354f, 250f), new Vector2(-30f, 298f));
                Place(hintPanel, Vector2.zero, new Vector2(30f, 272f), new Vector2(680f, 422f));
                if (killFeedPanel != null) Place(killFeedPanel, Vector2.one, new Vector2(-360f, -230f), new Vector2(-30f, -110f));
                for (int i = 0; i < killFeedTexts.Length; i++) if (killFeedTexts[i] != null) killFeedTexts[i].alignment = TextAlignmentOptions.MidlineRight;
                hintText.alignment = TextAlignmentOptions.MidlineLeft;
            }
            if (goalsPanel != null) Place(goalsPanel, new Vector2(0f, 1f),
                new Vector2(30f, touch ? -484f : -342f), new Vector2(530f, touch ? -332f : -190f));
            pauseHint.gameObject.SetActive(!touch);
        }

        /// <summary>Test hook: current layout mode.</summary>
        public bool TouchLayout => touchLayout;

        private void Update()
        {
            if (race == null) return;
            UpdateCombatAndGoals();
            if (hudRoot != null && Mathf.Abs(hudRoot.rect.width - layoutWidth) > 1f) ApplyLayout(touchLayout);   // rotation / resize
            if (playerCar == null && race.Player != null)
            {
                playerCar = race.Player.Car;
                if (playerHealth != null) playerHealth.OnHealthChanged -= OnHealth;
                if (playerCannon != null) playerCannon.AmmoChanged -= OnAmmo;
                if (playerWeapons != null) { playerWeapons.PrimarySwitched -= OnPrimarySwitched; playerWeapons.PrimarySwitchDenied -= OnPrimarySwitchDenied; }
                playerHealth = race.Player.Health;
                playerCannon = playerCar.GetComponent<CarCannon>();
                playerStatus = playerCar.GetComponent<CarStatus>();
                playerWeapons = playerCar.GetComponent<CarWeapons>();
                if (playerWeapons != null) { playerWeapons.PrimarySwitched += OnPrimarySwitched; playerWeapons.PrimarySwitchDenied += OnPrimarySwitchDenied; }
                if (playerCannon != null) { playerCannon.AmmoChanged += OnAmmo; BuildAmmoPips(playerCannon.AmmoMax); OnAmmo(playerCannon.Ammo, playerCannon.AmmoMax); }




                RaceDirector.Instance.OnKillRegistered -= OnKillRegistered;
                RaceDirector.Instance.OnKillRegistered += OnKillRegistered;
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

            if (playerStatus != null)
            {
                bool shield = playerStatus.HasShield;
                bool frozen = playerStatus.IsFrozen;
                bool jammed = playerStatus.IsJammed;
                bool punctured = playerStatus.IsPunctured;
                bool slowed = playerStatus.IsSlowed;
                if (lastStatusStr == null || shield != lastStatusShield || frozen != lastStatusFrozen || jammed != lastStatusJammed || punctured != lastStatusPunctured || slowed != lastStatusSlowed)
                {
                    lastStatusShield = shield;
                    lastStatusFrozen = frozen;
                    lastStatusJammed = jammed;
                    lastStatusPunctured = punctured;
                    lastStatusSlowed = slowed;
                    UpdateStatusIndicators(shield, frozen, jammed, punctured, slowed);
                }
            }
            else if (statusPanel != null && statusPanel.gameObject.activeSelf)
            {
                statusPanel.gameObject.SetActive(false);
                HideAllStatusIndicators();
            }

            if (playerWeapons != null && playerWeapons.primaryType == PrimaryWeaponType.MachineGun)
            {
                bool overheated = playerWeapons.IsOverheated;
                int heatPct = Mathf.RoundToInt((playerWeapons.CurrentHeat / Mathf.Max(1f, playerWeapons.heatMax)) * 100f);
                if (overheated != lastOverheated || heatPct != lastHeatPct || playerWeapons.primaryType != lastPrimaryType)
                {
                    lastOverheated = overheated;
                    lastHeatPct = heatPct;
                    lastPrimaryType = PrimaryWeaponType.MachineGun;
                    ammoPanel.gameObject.SetActive(true);
                    if (overheated)
                    {
                        ammoText.text = Loc.Get("hud.overheated");
                        ammoText.color = CriticalColor;
                    }
                    else if (heatPct > 10)
                    {
                        ammoText.text = Loc.Format("hud.heat", "pct", heatPct);
                        ammoText.color = Color.Lerp(new Color(1f, 0.85f, 0.3f), CriticalColor, heatPct / 100f);
                    }
                    else
                    {
                        ammoText.text = Loc.Get("hud.mg_ready");
                        ammoText.color = new Color(1f, 0.85f, 0.3f);
                    }
                }
            }
            else if (playerWeapons != null && playerWeapons.primaryType != lastPrimaryType)
            {
                lastPrimaryType = playerWeapons.primaryType;
                ammoText.color = new Color(1f, 0.85f, 0.3f);
                if (playerCannon != null) OnAmmo(playerCannon.Ammo, playerCannon.AmmoMax);
            }

            if (playerWeapons != null && playerWeapons.secondaryType != SecondaryWeaponType.None)
            {
                int cur = playerWeapons.SecondaryAmmo;
                int max = playerWeapons.SecondaryAmmoMax;
                bool ready = playerWeapons.IsSecondaryReady(SimulationDriver.Instance.Tick);
                var stype = playerWeapons.secondaryType;
                if (cur != lastSecondaryAmmo || ready != lastSecondaryReady || stype != lastSecondaryType)
                {
                    lastSecondaryAmmo = cur;
                    lastSecondaryReady = ready;
                    lastSecondaryType = stype;
                    secondaryPanel.gameObject.SetActive(true);
                    string key = stype == SecondaryWeaponType.Mines ? "hud.secondary.mines" : "hud.secondary.freeze";
                    secondaryText.text = Loc.Format(key, "cur", cur, "max", max);
                    secondaryText.color = ready ? (cur > 0 ? new Color(1f, 0.6f, 0.2f) : UiFactory.Muted) : new Color(0.7f, 0.7f, 0.7f, 0.5f);
                }
            }
            else if (secondaryPanel != null && secondaryPanel.gameObject.activeSelf)
            {
                secondaryPanel.gameObject.SetActive(false);
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

            if (race != null)
            {
                if (!killFeedNamesCached) EnsureKillFeedNames();
                killFeedBuffer.Update(Time.deltaTime);
                UpdateKillFeedDisplay();
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
            if (race != null) { UpdateReticle(); UpdateThreatMarkers(); UpdateToastAndWarnings(); }
            if (shownCenter != centerText.text)
            {
                shownCenter = centerText.text;
                centerPanel.gameObject.SetActive(!string.IsNullOrEmpty(shownCenter));
                centerPanel.sizeDelta = new Vector2(Mathf.Clamp(centerText.preferredWidth + 64f, 200f, Mathf.Min(900f, hudRoot.rect.width - 80f)), 190f);
            }
            if (pausePanel.gameObject.activeSelf)
                pauseControls.interactable = (PauseController.Reasons & ~PauseReason.User) == PauseReason.None;
        }

        private void UpdateThreatMarkers()
        {
            if (playerCar == null || race == null || race.State < RaceState.Countdown || race.State >= RaceState.Finished)
            {
                for (int i = 0; i < threatMarkers.Length; i++)
                    if (threatMarkers[i]?.root != null && threatMarkers[i].root.gameObject.activeSelf)
                        threatMarkers[i].root.gameObject.SetActive(false);
                return;
            }

            var cam = Camera.main;
            if (cam == null) return;

            var participants = race.Participants;
            int markerIdx = 0;
            Vector3 playerPos = playerCar.transform.position;
            Vector3 camPos = cam.transform.position;
            Vector3 camFwd = cam.transform.forward;

            float maxRange = (RaceSetup.Event != null && RaceSetup.Event.mode == EventMode.Arena) ? 90f : threatMaxRange;

            for (int i = 0; i < participants.Count && markerIdx < threatMarkers.Length; i++)
            {
                var rival = participants[i];
                if (rival == null || rival.isPlayer || rival.Car == null || !rival.Car.isActiveAndEnabled) continue;
                if (rival.Health != null && rival.Health.IsDead) continue;

                Vector3 rivalPos = rival.Car.transform.position;
                Vector3 toRival = rivalPos - camPos;
                float dist = Vector3.Distance(playerPos, rivalPos);
                if (dist > maxRange || dist < 1.5f) continue;

                Vector3 vp = cam.WorldToViewportPoint(rivalPos + Vector3.up * 0.5f);
                bool behind = vp.z < 0f || Vector3.Dot(camFwd, toRival) < 0f;
                if (behind)
                {
                    vp.x = 1f - vp.x;
                    vp.y = 1f - vp.y;
                }

                Vector2 fromCenter = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
                bool isOffScreen = behind || vp.x < 0.08f || vp.x > 0.92f || vp.y < 0.08f || vp.y > 0.92f;
                bool isBehindPlayer = Vector3.Dot(playerCar.transform.forward, rivalPos - playerPos) < 0.1f;

                // Only show threat marker when rival is off-screen, behind the player, or very close threat
                if (!isOffScreen && !isBehindPlayer && dist > 25f) continue;

                if (fromCenter.sqrMagnitude < 0.0001f) fromCenter = new Vector2(0f, -0.5f);

                // Elliptical/box edge intersection
                float borderX = 0.44f, borderY = 0.42f;
                float scaleX = borderX / Mathf.Max(0.0001f, Mathf.Abs(fromCenter.x));
                float scaleY = borderY / Mathf.Max(0.0001f, Mathf.Abs(fromCenter.y));
                float borderScale = Mathf.Min(scaleX, scaleY);
                Vector2 edgeVp = new Vector2(0.5f, 0.5f) + fromCenter * Mathf.Min(1f, borderScale);

                Vector2 canvasPos = new Vector2((edgeVp.x - 0.5f) * hudRoot.rect.width, (edgeVp.y - 0.5f) * hudRoot.rect.height);

                Vector3 rivalFwd = rival.Car.transform.forward;
                Vector3 toPlayer = playerPos - rivalPos;
                bool aimingAtMe = Vector3.Dot(rivalFwd, toPlayer.normalized) > 0.65f && dist < 45f;
                bool highThreat = aimingAtMe || (dist < 20f) || (isBehindPlayer && dist < 30f);
                bool medThreat = dist < 45f;

                Color markerColor = highThreat ? new Color(1f, 0.25f, 0.2f, 0.95f)
                                  : medThreat ? new Color(1f, 0.75f, 0.2f, 0.9f)
                                  : new Color(0.4f, 0.8f, 1f, 0.85f);

                var tm = threatMarkers[markerIdx++];
                if (!tm.root.gameObject.activeSelf) tm.root.gameObject.SetActive(true);
                tm.root.anchoredPosition = canvasPos;

                float angle = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg - 90f;
                tm.arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, angle);
                tm.arrow.color = markerColor;

                int dMeters = Mathf.RoundToInt(dist);
                tm.distanceText.SetText("{0:0}m", dMeters);
                tm.distanceText.color = highThreat ? new Color(1f, 0.95f, 0.9f) : UiFactory.Muted;

                if (highThreat)
                {
                    float pulse = 0.9f + 0.15f * Mathf.Sin(Time.unscaledTime * 12f);
                    tm.root.localScale = new Vector3(pulse, pulse, 1f);
                }
                else
                {
                    tm.root.localScale = Vector3.one;
                }
            }

            for (int i = markerIdx; i < threatMarkers.Length; i++)
            {
                if (threatMarkers[i]?.root != null && threatMarkers[i].root.gameObject.activeSelf)
                    threatMarkers[i].root.gameObject.SetActive(false);
            }
        }

        private void OnAmmo(int ammo, int max)
        {
            lastReloadPct = -1;
            if (ammoPips.Length != Mathf.Clamp(max, 0, 12)) BuildAmmoPips(max);   // swapped between cannon and machine gun
            bool primary = max > 0 && (playerCannon == null || !playerCannon.locked);   // mines-only events lock the cannon
            ammoPanel.gameObject.SetActive(primary);
            if (!primary) { ammoText.text = ""; return; }
            ammoText.text = ammo > 0 ? Loc.Format("hud.ammo", "cur", ammo, "max", max) : Loc.Format("hud.reload", "p", 0);
        }

        private void OnPrimarySwitched(PrimaryWeaponType type)
        {
            ShowHint(Loc.Get(type == PrimaryWeaponType.MachineGun ? "garage.machinegun" : "garage.cannon"), 1.2f);
        }

        private void OnPrimarySwitchDenied()
        {
            ShowHint(Loc.Get("garage.machinegun") + " — " + Loc.Get("garage.locked"), 1.5f);
        }

        public void ShowCombatHit(RaceParticipant victim, HitInfo hit, HitResult result)
        {
            var player = race != null ? race.Player : null;
            if (player == null || victim == null || (!result.ShieldAbsorbed && result.DamageDealt <= 0f && !result.StatusApplied)) return;
            bool outgoing = hit.Attacker == player.gameObject && victim != player;
            if (!outgoing && victim != player) return;
            string outcome = Loc.Get(result.Fatal ? "combat.destroyed" : result.ShieldAbsorbed ? "combat.shield" : "combat.hit");
            Color color = result.Fatal ? HitmarkerKillColor : result.ShieldAbsorbed ? new Color(0.3f, 0.95f, 1f) : HitmarkerNormalColor;
            if (outgoing)
            {
                // Preserve a kill confirmation if another bullet lands during the same burst.
                if (!result.Fatal && hitMarker.color == HitmarkerKillColor && Time.unscaledTime < hitMarkerUntil) return;
                hitMarker.color = color; hitMarker.gameObject.SetActive(true);
                combatText.text = outcome; combatText.color = color;
                hitMarkerUntil = Time.unscaledTime + (result.Fatal ? 0.6f : 0.35f);
                if (!result.Fatal) SfxPlayer.Instance.PlayUi(result.ShieldAbsorbed ? SfxSynth.ShieldBreak : SfxSynth.Hit, 0.45f, 1.3f);
            }
            else
            {
                incomingSource = hit.Attacker != null ? hit.Attacker.transform.position : hit.Point;
                incomingUntil = Time.time + 0.85f;
                var attacker = hit.Attacker != null ? hit.Attacker.GetComponent<RaceParticipant>() : null;
                string name = attacker != null ? UiFactory.Escape(GetLocalizedRacerName(attacker.displayName)) : Loc.Get("combat.obstacle");
                incomingText.text = name + " · " + outcome;
                incomingText.color = incomingArrow.color = color;
                if (result.ShieldAbsorbed) SfxPlayer.Instance.PlayUi(SfxSynth.ShieldBreak, 0.65f);
            }
        }

        private void UpdateCombatAndGoals()
        {
            bool incoming = playerCar != null && Time.time < incomingUntil;
            incomingArrow.gameObject.SetActive(incoming);
            incomingText.gameObject.SetActive(incoming);
            if (incoming)
            {
                var cam = Camera.main;
                Vector3 delta = incomingSource - playerCar.transform.position;
                Vector3 right = cam != null ? cam.transform.right : playerCar.transform.right;
                Vector3 forward = cam != null ? Vector3.Cross(right, Vector3.up).normalized : playerCar.transform.forward;
                Vector2 direction = new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, forward)).normalized;
                incomingArrow.rectTransform.anchoredPosition = direction * 175f;
                incomingArrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg);
            }
            if (Time.unscaledTime > hitMarkerUntil) combatText.text = "";
            if (PauseController.IsPaused || Time.unscaledTime < nextGoalRefresh) return;
            nextGoalRefresh = Time.unscaledTime + 0.25f;
            bool showGoals = race.Player != null && RaceSetup.Event != null && race.State >= RaceState.Countdown && race.State <= RaceState.Finishing;
            goalsPanel.gameObject.SetActive(showGoals);
            if (showGoals)
            {
                race.ReadResult(race.Player, goalEntry);
                goalsText.text = StarGoalText.Progress(RaceSetup.Event, goalEntry);
            }
        }

        private void OnKillRegistered(KillEvent evt)
        {
            EnsureKillFeedNames();
            if (playerCar != null && evt.KillerId == playerCar.CarId && evt.KillerId != evt.VictimId)
            {
                hitMarker.color = HitmarkerKillColor;
                hitMarker.gameObject.SetActive(true);
                hitMarkerUntil = Time.unscaledTime + 0.5f;
            }
            killFeedBuffer.Push(evt.KillerId, evt.VictimId);
            UpdateKillFeedDisplay();
        }

        private void BuildKillFeed(Transform root)
        {
            killFeedPanel = UiFactory.Panel(root, "KillFeed", Vector2.one, Vector2.one, new Vector2(-360f, -230f), new Vector2(-30f, -110f), Color.clear, false);
            var panelImg = killFeedPanel.GetComponent<Image>();
            if (panelImg != null) panelImg.raycastTarget = false;

            for (int i = 0; i < KillFeedBuffer.MaxEntries; i++)
            {
                float top = -i * 38f;
                float btm = top - 34f;
                var rowRt = UiFactory.Card(killFeedPanel, $"KillRow{i}", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, btm), new Vector2(0f, top), HudPanelAlpha);
                var bg = rowRt.GetComponent<Image>();
                if (bg != null) bg.raycastTarget = false;

                var txt = UiFactory.Text(rowRt, "Text", "", 20f, TextAlignmentOptions.MidlineRight, Vector2.zero, Vector2.one, new Vector2(12f, 2f), new Vector2(-12f, -2f), Color.white);
                txt.fontStyle = FontStyles.Bold;
                txt.enableAutoSizing = true;
                txt.fontSizeMin = 14f;
                txt.fontSizeMax = 20f;
                txt.raycastTarget = false;

                killFeedRows[i] = rowRt;
                killFeedBgs[i] = bg;
                killFeedTexts[i] = txt;
                rowRt.gameObject.SetActive(false);
            }
        }

        private void EnsureKillFeedNames()
        {
            if (killFeedNamesCached) return;
            if (race == null) return;
            var participants = race.Participants;
            if (participants == null || participants.Count == 0) return;

            for (int i = 0; i < participants.Count; i++)
            {
                var p = participants[i];
                if (p == null) continue;
                int carId = p.Car != null ? p.Car.CarId : 0;
                string locName = GetLocalizedRacerName(p.displayName, p.isPlayer);
                killFeedBuffer.RegisterRacerName(carId, locName);
            }
            killFeedBuffer.PrecacheAll();
            killFeedNamesCached = true;
        }

        public static string GetLocalizedRacerName(string rawName, bool isPlayer = false)
        {
            if (isPlayer || string.Equals(rawName, "You", System.StringComparison.OrdinalIgnoreCase))
            {
                string loc = Loc.Get("player.name");
                if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("[")) return loc;
                return "You";
            }
            if (string.IsNullOrEmpty(rawName)) return "Car";
            string baseName = rawName, suffix = "";
            int split = rawName.LastIndexOf(' ');
            if (split > 0 && int.TryParse(rawName.Substring(split + 1), out _))
            { baseName = rawName.Substring(0, split); suffix = rawName.Substring(split); }
            string key = "ai.name." + baseName.ToLowerInvariant();
            string aiLoc = Loc.Get(key);
            if (!string.IsNullOrEmpty(aiLoc) && !aiLoc.StartsWith("[")) return aiLoc + suffix;
            return rawName;
        }

        private void UpdateKillFeedDisplay()
        {
            for (int i = 0; i < KillFeedBuffer.MaxEntries; i++)
            {
                var entry = killFeedBuffer.GetEntry(i);
                if (entry.IsActive)
                {
                    if (killFeedRows[i] != null && !killFeedRows[i].gameObject.activeSelf)
                        killFeedRows[i].gameObject.SetActive(true);

                    if (killFeedTexts[i] != null)
                    {
                        killFeedTexts[i].text = entry.FormattedText;
                        float alpha = entry.Alpha;
                        var tc = killFeedTexts[i].color;
                        tc.a = alpha;
                        killFeedTexts[i].color = tc;

                        if (killFeedBgs[i] != null)
                        {
                            var bc = killFeedBgs[i].color;
                            bc.a = alpha * HudPanelAlpha;
                            killFeedBgs[i].color = bc;
                        }
                    }
                }
                else
                {
                    if (killFeedRows[i] != null && killFeedRows[i].gameObject.activeSelf)
                        killFeedRows[i].gameObject.SetActive(false);
                }
            }
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
            incomingUntil = hitMarkerUntil = nextGoalRefresh = 0f;
            combatText.text = "";
            incomingArrow.gameObject.SetActive(false); incomingText.gameObject.SetActive(false);
            goalsPanel.gameObject.SetActive(visible);
            if (!visible)
            {
                for (int i = 0; i < threatMarkers.Length; i++)
                    if (threatMarkers[i]?.root != null && threatMarkers[i].root.gameObject.activeSelf)
                        threatMarkers[i].root.gameObject.SetActive(false);
            }
        }

        private void OnPaused(bool paused)
        {
            pausePanel.gameObject.SetActive(paused);
            if (paused) EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
            else EventSystem.current?.SetSelectedGameObject(null);
        }

        private void UpdateStatusIndicators(bool shield, bool frozen, bool jammed, bool punctured, bool slowed)
        {
            if (statusIndicators[0] != null) statusIndicators[0].SetActive(shield);
            if (statusIndicators[1] != null) statusIndicators[1].SetActive(frozen);
            if (statusIndicators[2] != null) statusIndicators[2].SetActive(jammed);
            if (statusIndicators[3] != null) statusIndicators[3].SetActive(punctured);
            if (statusIndicators[4] != null) statusIndicators[4].SetActive(slowed);

            int activeCount = 0;
            for (int i = 0; i < statusIndicators.Length; i++)
            {
                if (statusIndicators[i] != null && statusIndicators[i].IsActive) activeCount++;
            }

            if (activeCount == 0)
            {
                lastStatusStr = "";
                if (statusText != null) statusText.text = "";
                statusPanel.gameObject.SetActive(false);
                return;
            }

            statusPanel.gameObject.SetActive(true);

            int currentSlot = 0;
            for (int i = 0; i < statusIndicators.Length; i++)
            {
                var ind = statusIndicators[i];
                if (ind != null && ind.IsActive)
                {
                    float minX = (float)currentSlot / activeCount;
                    float maxX = (float)(currentSlot + 1) / activeCount;
                    ind.Root.anchorMin = new Vector2(minX, 0f);
                    ind.Root.anchorMax = new Vector2(maxX, 1f);
                    ind.Root.offsetMin = new Vector2(3f, 3f);
                    ind.Root.offsetMax = new Vector2(-3f, -3f);
                    currentSlot++;
                }
            }

            sb.Clear();
            if (shield && statusIndicators[0] != null) sb.Append("<color=#40E0D0>[").Append(statusIndicators[0].CachedLabel).Append("]</color> ");
            if (frozen && statusIndicators[1] != null) sb.Append("<color=#66CCFF>[").Append(statusIndicators[1].CachedLabel).Append("]</color> ");
            if (jammed && statusIndicators[2] != null) sb.Append("<color=#FFA500>[").Append(statusIndicators[2].CachedLabel).Append("]</color> ");
            if (punctured && statusIndicators[3] != null) sb.Append("<color=#FF4444>[").Append(statusIndicators[3].CachedLabel).Append("]</color> ");
            if (slowed && statusIndicators[4] != null) sb.Append("<color=#8855FF>[").Append(statusIndicators[4].CachedLabel).Append("]</color> ");
            lastStatusStr = sb.ToString();
            if (statusText != null) statusText.text = lastStatusStr;
        }

        private void HideAllStatusIndicators()
        {
            if (statusIndicators == null) return;
            for (int i = 0; i < statusIndicators.Length; i++)
            {
                statusIndicators[i]?.SetActive(false);
            }
            lastStatusStr = "";
            lastStatusShield = lastStatusFrozen = lastStatusJammed = lastStatusPunctured = lastStatusSlowed = false;
        }

        /// <summary>Returns true if the dedicated status indicator widget for the specified status kind is active.</summary>
        public bool IsStatusIndicatorActive(StatusKind kind)
        {
            var widget = GetStatusIndicator(kind);
            return widget != null && widget.IsActive;
        }

        /// <summary>Gets the dedicated status indicator widget for the specified kind.</summary>
        public StatusIndicatorWidget GetStatusIndicator(StatusKind kind)
        {
            if (statusIndicators == null) return null;
            for (int i = 0; i < statusIndicators.Length; i++)
            {
                if (statusIndicators[i] != null && statusIndicators[i].Kind == kind)
                    return statusIndicators[i];
            }
            return null;
        }

        private void OnDestroy()
        {
            if (playerHealth != null) playerHealth.OnHealthChanged -= OnHealth;
            if (playerCannon != null) playerCannon.AmmoChanged -= OnAmmo;
            if (playerWeapons != null) { playerWeapons.PrimarySwitched -= OnPrimarySwitched; playerWeapons.PrimarySwitchDenied -= OnPrimarySwitchDenied; }
            var director = RaceDirector.Current;
            if (director != null)
            {
                director.OnKillRegistered -= OnKillRegistered;
            }
            if (healthFillSprite != null) Destroy(healthFillSprite);
        }
    }

    /// <summary>
    /// Dedicated status indicator widget on the HUD displaying visual glyph and localized label for an active status effect.
    /// </summary>
    public sealed class StatusIndicatorWidget
    {
        public StatusKind Kind { get; }
        public RectTransform Root { get; }
        public Image Background { get; }
        public TextMeshProUGUI GlyphText { get; }
        public TextMeshProUGUI LabelText { get; }
        public Color ThemeColor { get; }
        public string Glyph { get; }
        public string LocKey { get; }
        public string CachedLabel { get; private set; }
        public bool IsActive => Root != null && Root.gameObject.activeSelf;

        public StatusIndicatorWidget(RectTransform root, Image background, TextMeshProUGUI glyphText, TextMeshProUGUI labelText, StatusKind kind, Color themeColor, string glyph, string locKey)
        {
            Root = root;
            Background = background;
            GlyphText = glyphText;
            LabelText = labelText;
            Kind = kind;
            ThemeColor = themeColor;
            Glyph = glyph;
            LocKey = locKey;
            CachedLabel = "";
        }

        public void SetActive(bool active)
        {
            if (Root != null && Root.gameObject.activeSelf != active)
            {
                Root.gameObject.SetActive(active);
            }
        }

        public void RefreshLocalization()
        {
            CachedLabel = Loc.Get(LocKey);
            if (LabelText != null) LabelText.text = CachedLabel;
            if (GlyphText != null) GlyphText.text = Glyph;
        }
    }
}
