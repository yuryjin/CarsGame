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
        private GameFlow gameFlow;

        private TextMeshProUGUI speedText, lapText, positionText, timerText, centerText, hintText, hpText, pauseText, pauseHint, ammoText, hitMarker;
        private CarCannon playerCannon;
        private CarStatus playerStatus;
        private CarWeapons playerWeapons;
        private PlayerDriverInput playerInput;
        private RectTransform weaponWheel;
        private WeaponWheelGraphic weaponWheelGraphic;
        private readonly TextMeshProUGUI[] wheelLabels = new TextMeshProUGUI[PrimaryWeaponCatalog.SlotsPerPage];
        private TextMeshProUGUI wheelCenter, wheelPage;
        private float hitMarkerUntil;
        private RectTransform goalsPanel;
        private TextMeshProUGUI goalsText, combatText, incomingArrow, incomingText;
        private readonly RaceResultEntry goalEntry = new RaceResultEntry();
        private float nextGoalRefresh, incomingUntil;
        private bool goalsExpanded;
        private Vector3 incomingSource;

        private Image hpFill, hpTrail, hpIcon;
        private Sprite healthFillSprite;
        private RectTransform speedPanel, healthPanel, hintPanel, ammoPanel, secondaryPanel, statusPanel, eventPanel, centerPanel, pauseButton, racePanel, timerPanel;
        private TextMeshProUGUI secondaryText, statusText;
        private TextMeshProUGUI primaryNameText;
        private string lastStatusStr;
        private bool lastStatusShield, lastStatusFrozen, lastStatusJammed, lastStatusPunctured, lastStatusSlowed;
        private int lastSecondaryAmmo = -1;
        private bool lastSecondaryReady;
        private SecondaryWeaponType lastSecondaryType = SecondaryWeaponType.None;
        private bool lastChargePlanted;
        private PrimaryWeaponType lastPrimaryType = PrimaryWeaponType.Cannon;
        private bool lastOverheated;
        private int lastHeatPct = -1;
        private Button resumeButton, restartButton, menuButton;
        private RectTransform helpPanel;
        private ScrollRect helpScroll;
        private int layoutScreenWidth, layoutScreenHeight;
        private TextMeshProUGUI helpText;
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
        private RectTransform hudRoot, reticle, combatFeedback, incomingPanel;
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
        public bool ReticleVisible => reticle != null && reticle.gameObject.activeInHierarchy;
        public Vector2 ReticleScreenPosition => reticle != null ? RectTransformUtility.WorldToScreenPoint(null, reticle.position) : Vector2.zero;
        private bool touchLayout;
        private float layoutWidth = -1f;
        private RectTransform pausePanel;
        private RectTransform finishPanel;
        private TextMeshProUGUI finishTitle, finishSummary;
        private Button finishResultsButton;
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
            gameFlow = race != null ? race.GetComponent<GameFlow>() : null;
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
            if (race != null)
            {
                race.OnCountdownTick -= OnCountdown;
                race.OnRaceStarted -= OnStarted;
                race.OnRaceFinished -= OnFinished;
                race.OnPositionsChanged -= RefreshPosition;
            }
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
            ammoPanel = UiFactory.Card(root, "AmmoPanel", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-354f, 138f), new Vector2(-30f, 218f), HudPanelAlpha);
            primaryNameText = UiFactory.Text(ammoPanel, "ActiveWeapon", "", 36f, TextAlignmentOptions.MidlineRight,
                new Vector2(0f, 1f), Vector2.one, new Vector2(14f, -47f), new Vector2(-14f, -3f), Color.white);
            primaryNameText.enableWordWrapping = true; primaryNameText.overflowMode = TextOverflowModes.Ellipsis;
            ammoText = UiFactory.Text(ammoPanel, "Ammo", "", 30f, TextAlignmentOptions.MidlineRight, Vector2.zero, new Vector2(1f, 0f), new Vector2(18f, 4f), new Vector2(-18f, 44f), new Color(1f, 0.85f, 0.3f));
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
            // Hit confirmation also applies to radial pulses and rear traps,
            // independently of whether a forward aiming marker exists.
            combatFeedback = UiFactory.Panel(root, "CombatFeedback", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-36f, -36f), new Vector2(36f, 36f));
            hitMarker = UiFactory.Text(combatFeedback, "HitMarker", "×", 64f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, -60f), new Vector2(60f, 60f), HitmarkerNormalColor);
            hitMarker.gameObject.SetActive(false);
            combatText = UiFactory.Text(combatFeedback, "HitOutcome", "", 22f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-180f, 60f), new Vector2(180f, 100f));
            combatText.fontStyle = FontStyles.Bold;
            UiFactory.SetOutline(combatText, 0.25f);
            incomingArrow = UiFactory.Text(root, "IncomingDirection", "▲", 44f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-28f, -28f), new Vector2(28f, 28f), CriticalColor);
            incomingPanel = UiFactory.Card(root, "IncomingPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260f, -150f), new Vector2(260f, -112f), HudPanelAlpha);
            incomingText = UiFactory.Text(incomingPanel, "IncomingSource", "", 23f, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(10f, 6f), new Vector2(-10f, -6f));
            incomingPanel.gameObject.SetActive(false);
            incomingArrow.gameObject.SetActive(false); incomingText.gameObject.SetActive(false);
            goalsPanel = UiFactory.Card(root, "LiveGoals", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -342f), new Vector2(530f, -190f), HudPanelAlpha);
            goalsText = UiFactory.Text(goalsPanel, "Goals", "", 23f, TextAlignmentOptions.MidlineLeft,
                Vector2.zero, Vector2.one, new Vector2(16f, 8f), new Vector2(-16f, -8f));
            goalsText.enableAutoSizing = true; goalsText.fontSizeMin = 18f; goalsText.fontSizeMax = 23f;
            goalsText.raycastTarget = combatText.raycastTarget = incomingArrow.raycastTarget = incomingText.raycastTarget = false;
            goalsPanel.GetComponent<Image>().raycastTarget = true;
            var goalsButton = goalsPanel.gameObject.AddComponent<Button>();
            goalsButton.targetGraphic = goalsPanel.GetComponent<Image>();
            goalsButton.onClick.AddListener(() => { goalsExpanded = !goalsExpanded; nextGoalRefresh = 0f; });

            // Top-left: lap + position
            racePanel = UiFactory.Card(root, "RaceStatus", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -174f), new Vector2(310f, -24f), HudPanelAlpha);
            UiFactory.Panel(racePanel, "Accent", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 18f), new Vector2(3f, -18f), UiFactory.Accent, false);
            lapText = UiFactory.Text(racePanel, "Lap", "LAP 1/3", 36f, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -70f), new Vector2(-18f, -12f));
            positionText = UiFactory.Text(racePanel, "Position", "P1/4", 36f, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 1f), Vector2.one, new Vector2(18f, -138f), new Vector2(-18f, -80f));
            lapText.fontSize = 28f; lapText.color = UiFactory.Muted;
            positionText.fontStyle = FontStyles.Bold;
            lapText.enableAutoSizing = positionText.enableAutoSizing = true;
            lapText.fontSizeMin = positionText.fontSizeMin = 20f;
            lapText.fontSizeMax = 28f; positionText.fontSizeMax = 36f;

            // Top-centre: timer
            timerPanel = UiFactory.Card(root, "TimerPanel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-140f, -94f), new Vector2(140f, -24f), HudPanelAlpha);
            timerText = UiFactory.Text(timerPanel, "Timer", "0:00.000", 40f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(16f, 10f), new Vector2(-16f, -10f));

            // Centre: countdown / wrong way / messages
            centerPanel = UiFactory.Card(root, "CenterPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120f, -100f), new Vector2(120f, 100f));
            centerText = UiFactory.Text(centerPanel, "Center", "", 120f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, new Vector2(24f, 8f), new Vector2(-24f, -8f), UiFactory.Accent);
            centerText.fontStyle = FontStyles.Bold;
            centerText.enableAutoSizing = true; centerText.fontSizeMin = 38f; centerText.fontSizeMax = 140f;
            centerPanel.gameObject.SetActive(false);

            BuildWeaponWheel(root);
            BuildFinishCard(root);
            BuildPause(root);

            hintUntil = Time.unscaledTime + hintSeconds;
            Loc.LanguageChanged += ApplyStaticTexts;
            ApplyStaticTexts();
        }

        private void BuildWeaponWheel(Transform root)
        {
            var center = new Vector2(0.5f, 0.5f);
            weaponWheel = UiFactory.Panel(root, "WeaponWheel", center, center, new Vector2(-360f, -360f), new Vector2(360f, 360f));
            weaponWheelGraphic = weaponWheel.gameObject.AddComponent<WeaponWheelGraphic>();
            weaponWheelGraphic.raycastTarget = false;
            for (int i = 0; i < wheelLabels.Length; i++)
            {
                Vector2 position = WeaponWheelGraphic.SlotDirection(i + 1) * 266f;
                var label = UiFactory.Text(weaponWheel, "Slot" + (i + 1), "", 24f, TextAlignmentOptions.Center,
                    center, center, position - new Vector2(70f, 44f), position + new Vector2(70f, 44f));
                label.enableAutoSizing = true;
                label.fontSizeMin = 14f;
                label.fontSizeMax = 24f;
                label.overflowMode = TextOverflowModes.Ellipsis;
                wheelLabels[i] = label;
            }
            wheelCenter = UiFactory.Text(weaponWheel, "Selected", "", 26f, TextAlignmentOptions.Center,
                center, center, new Vector2(-135f, -65f), new Vector2(135f, 65f), UiFactory.Accent);
            wheelCenter.enableAutoSizing = true;
            wheelCenter.fontSizeMin = 16f;
            wheelCenter.fontSizeMax = 26f;
            wheelPage = UiFactory.Text(weaponWheel, "Key", "", 19f, TextAlignmentOptions.Center,
                center, center, new Vector2(-145f, -130f), new Vector2(145f, -75f), UiFactory.Muted);
            weaponWheel.gameObject.SetActive(false);
        }

        private void UpdateWeaponWheel()
        {
            bool visible = playerInput != null && playerInput.WeaponWheelOpen && !PauseController.IsPaused
                && race.State == RaceState.Racing && playerWeapons != null && playerCannon != null && !playerCannon.locked;
            weaponWheel.gameObject.SetActive(visible);
            if (!visible) return;
            float scale = Mathf.Min(1f, Mathf.Min(hudRoot.rect.width - 40f, hudRoot.rect.height - 40f) / 720f);
            weaponWheel.localScale = Vector3.one * Mathf.Max(0.1f, scale);
            int selected = playerInput.WheelSelection;
            int page = playerInput.SelectionPage;
            int pageStart = page * PrimaryWeaponCatalog.SlotsPerPage;
            int localSelection = selected > pageStart && selected <= pageStart + PrimaryWeaponCatalog.SlotsPerPage ? selected - pageStart : 0;
            weaponWheelGraphic.SetSelection(localSelection, page != 0 || playerWeapons.machineGunUnlocked);
            wheelPage.text = Loc.Format("weapon.page", "page", page + 1, "pages", PrimaryWeaponCatalog.PageCount);
            for (int i = 0; i < wheelLabels.Length; i++)
            {
                var type = (PrimaryWeaponType)(pageStart + i);
                bool available = playerWeapons.IsPrimaryAvailable(type);
                wheelLabels[i].text = ((i + 1) % 10) + "\n" + Loc.Get(PrimaryWeaponCatalog.Get(type).NameKey);
                wheelLabels[i].color = !available ? UiFactory.Muted : selected == pageStart + i + 1 ? UiFactory.Accent : Color.white;
            }
            var shown = selected == 0 ? playerWeapons.PrimaryType : (PrimaryWeaponType)(selected - 1);
            wheelCenter.text = Loc.Get(PrimaryWeaponCatalog.Get(shown).NameKey)
                + (playerWeapons.IsPrimaryAvailable(shown) ? "" : "\n" + Loc.Get("garage.locked"));
        }

        private void BuildFinishCard(Transform root)
        {
            var center = new Vector2(0.5f, 0.5f);
            finishPanel = UiFactory.Card(root, "FinishCard", center, center, new Vector2(-300f, -140f), new Vector2(300f, 140f), 1f);
            finishTitle = UiFactory.Text(finishPanel, "FinishTitle", "", 48f, TextAlignmentOptions.Center,
                new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -90f), new Vector2(-24f, -20f), UiFactory.Positive);
            finishTitle.fontStyle = FontStyles.Bold;
            finishSummary = UiFactory.Text(finishPanel, "FinishSummary", "", 30f, TextAlignmentOptions.Center,
                new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -158f), new Vector2(-24f, -96f));
            finishResultsButton = UiFactory.Button(finishPanel, "FinishResults", "", () => race.ShowResultsAfterFinish(),
                Vector2.zero, new Vector2(1f, 0f), new Vector2(36f, 26f), new Vector2(-36f, 102f), 30f);
            finishPanel.gameObject.SetActive(false);
        }

        private void UpdateFinishCard()
        {
            bool visible = race.CanShowResults && !PauseController.IsPaused;
            bool wasVisible = finishPanel.gameObject.activeSelf;
            finishPanel.gameObject.SetActive(visible);
            if (!visible || wasVisible) return;
            centerText.text = "";
            finishTitle.text = Loc.Get("hud.finish");
            finishSummary.text = Loc.Format("hud.position", "pos", race.PositionOf(race.Player), "n", race.Participants.Count)
                + " · " + UiFactory.FormatTime(race.PlayerElapsedTime);
            finishResultsButton.GetComponentInChildren<TextMeshProUGUI>().text = Loc.Get("results.title");
            EventSystem.current?.SetSelectedGameObject(finishResultsButton.gameObject);
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
            pauseHint = UiFactory.Text(card, "Hint", "", 26f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(1f, 0f), new Vector2(40f, 26f), new Vector2(-40f, 110f), UiFactory.Muted);
            pauseHint.raycastTarget = true;
            var helpButton = pauseHint.gameObject.AddComponent<Button>();
            helpButton.targetGraphic = pauseHint;
            helpButton.onClick.AddListener(ShowHelp);
            helpPanel = UiFactory.Card(pausePanel, "DrivingHelp", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-500f, -360f), new Vector2(500f, 360f), 1f);
            var lessons = UiFactory.Scroll(helpPanel, "HelpScroll", new Vector2(28f, 130f), new Vector2(-28f, -28f));
            helpScroll = lessons.GetComponentInParent<ScrollRect>();
            helpText = UiFactory.Text(lessons, "Lessons", "", 26f, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            UiFactory.SetOutline(helpText, 0f);
            UiFactory.Button(helpPanel, "CloseHelp", Loc.Get("common.back"), () => CloseHelpIfOpen(),
                Vector2.zero, new Vector2(1f, 0f), new Vector2(32f, 30f), new Vector2(-32f, 106f), 28f);
            helpPanel.gameObject.SetActive(false);
            pausePanel.gameObject.SetActive(false);
        }

        /// <summary>Back dismisses the help layer while preserving every pause reason.</summary>
        public bool CloseHelpIfOpen()
        {
            if (helpPanel == null || !helpPanel.gameObject.activeInHierarchy) return false;
            helpPanel.gameObject.SetActive(false);
            if (resumeButton != null) EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
            return true;
        }

        private void ShowHelp()
        {
            helpText.text = Loc.Get(touchLayout ? "help.drive.touch" : "help.drive.keys");
            if (!touchLayout) helpText.text += "\n\n" + Loc.Get("help.drive.gamepad");
            string context = ControlsHint(touchLayout);
            string drift = Loc.Get(touchLayout ? "lesson.drift.touch" : "lesson.drift.keys");
            // Special-event advice supplements the controls rather than replacing them.
            if (context != Loc.Get(touchLayout ? "lesson.drive.touch" : "lesson.drive.keys"))
                helpText.text += "\n\n" + context;
            if (context != drift) helpText.text += "\n\n" + drift;
            var car = race != null && race.Player != null ? race.Player.Car : null;
            var weapons = car != null ? car.Weapons : null;
            if (EventRules.PrimaryEnabled(car) && weapons != null)
            {
                helpText.text += "\n\n" + Loc.Get(touchLayout ? "help.primary.touch"
                    : weapons.CompactLoadout ? "help.primary.compact" : "help.primary.full");
                if (!touchLayout) helpText.text += "\n" + Loc.Get("help.primary.gamepad");
            }
            if (weapons != null && weapons.secondaryType != SecondaryWeaponType.None)
            {
                string action = Loc.Get(weapons.secondaryType == SecondaryWeaponType.FreezeLauncher ? "touch.freeze"
                    : weapons.secondaryType == SecondaryWeaponType.RemoteCharge ? "touch.charge" : "touch.secondary");
                helpText.text += "\n\n" + Loc.Format(touchLayout ? "help.secondary.touch" : "help.secondary.keys", "action", action);
                if (weapons.secondaryType == SecondaryWeaponType.RemoteCharge)
                    helpText.text += " " + Loc.Get("help.charge");
            }
            var ev = RaceSetup.Event;
            if (ev != null) helpText.text += "\n\n" + StarGoalText.Description(ev, 0) + "\n" + StarGoalText.Description(ev, 1) + "\n" + StarGoalText.Description(ev, 2);
            helpPanel.gameObject.SetActive(true);
            ApplyReadableOverlays();
            Canvas.ForceUpdateCanvases();
            helpScroll.verticalNormalizedPosition = 1f;
            var back = helpPanel.GetComponentInChildren<Button>();
            back.GetComponentInChildren<TextMeshProUGUI>().text = Loc.Get("common.back");
            EventSystem.current?.SetSelectedGameObject(back.gameObject);
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
                UiFactory.SetOutline(label, 0f);
            }
            speedText = UiFactory.Text(speedPanel, "Speed", "0", 46f, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.zero, new Vector2(74f, 32f), new Vector2(170f, 80f));
            speedText.fontStyle = FontStyles.Bold;
            UiFactory.SetOutline(speedText, 0f);
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
            UiFactory.SetOutline(hpText, 0f);
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
            BuildStatusIndicator(1, StatusKind.Frozen, new Color(0.4f, 0.8f, 1f), "☆", "hud.status.frozen");
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
            pauseText.text = Loc.Get("pause.title"); pauseHint.text = Loc.Get("experience.help");
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
                UiFactory.SetOutline(dist, 0.2f); UiFactory.SetOutline(arrow, 0.2f);
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

        // Projectile ballistics and contact weapons have different aiming geometry.
        private void UpdateReticle()
        {
            bool show = playerCar != null && playerCannon != null && !playerCannon.locked && playerCannon.muzzle != null && race.State >= RaceState.Countdown && race.State < RaceState.Finished
                        && (playerHealth == null || !playerHealth.IsDead);
            var cam = Camera.main;
            var shot = PrimaryWeaponCatalog.Get(playerCar != null && playerCar.Weapons != null ? playerCar.Weapons.PrimaryType : PrimaryWeaponType.Cannon);
            // A radial pulse or a trap behind the car has no forward aiming point.
            show &= shot.Mode != WeaponMode.Trap && shot.Mode != WeaponMode.Pulse;
            if (!show || cam == null) { if (reticle.gameObject.activeSelf) reticle.gameObject.SetActive(false); ClearLock(); return; }

            var muzzle = playerCannon.muzzle;
            bool projectile = shot.Mode == WeaponMode.Projectile;
            bool specialLocked = false;
            Vector3 aim;
            if (projectile)
            {
                Vector3 velocity = muzzle.forward * playerCannon.shellSpeed + playerCar.Body.linearVelocity;
                float distance = playerCannon.HasAimCommit ? Vector3.Distance(muzzle.position, playerCannon.AimCommitPoint) : aimDistance;
                float t = distance / Mathf.Max(1f, playerCannon.shellSpeed);
                aim = muzzle.position + velocity * t + 0.5f * t * t * Physics.gravity;
            }
            else specialLocked = playerCannon.TryGetSpecialAim(out aim);
            Vector3 sp = cam.WorldToScreenPoint(aim);
            if (sp.z <= 0f) { reticle.gameObject.SetActive(false); ClearLock(); return; }
            if (!reticle.gameObject.activeSelf) reticle.gameObject.SetActive(true);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, sp, null, out Vector2 local);
            reticle.anchoredPosition = local;

            if (!projectile || ++lockCheckFrame >= 5)
            {
                lockCheckFrame = 0;
                bool locked = specialLocked || (projectile && (
                    TargetFinder.Find(playerCar, muzzle.position, muzzle.forward, lockConeDegrees, lockMinRange, lockMaxRange) != null
                    || TargetFinder.FindUnit(playerCar, muzzle.position, muzzle.forward, lockConeDegrees, lockMinRange, lockMaxRange * 1.5f) != null
                    || TargetFinder.FindTurret(muzzle.position, muzzle.forward, lockConeDegrees, lockMinRange, lockMaxRange) != null));
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
            hintText.text = ControlsHint(touch);
            layoutWidth = hudRoot != null ? hudRoot.rect.width : 1920f;
            layoutScreenWidth = Screen.width; layoutScreenHeight = Screen.height;
            ApplyReadableOverlays();
            // Portrait phones give the canvas ~1000 reference units of width: the timer (±200 around the
            // centre) would collide with a top-right HP bar, so HP/ammo drop to a row below it.
            bool narrow = layoutWidth < 1400f;
            if (touch)
            {
                float edge = Mathf.Max(100f, 44f / Mathf.Max(0.1f, hudRoot.lossyScale.x));
                float statusRight = Mathf.Min(-170f, -64f - edge);
                Place(pauseButton, Vector2.one, new Vector2(-40f - edge, -54f - edge * 2f), new Vector2(-40f, -54f - edge));
                Place(speedPanel, Vector2.zero, new Vector2(40f, 224f), new Vector2(284f, 448f));
                float row = narrow ? -90f : 0f;
                Place(healthPanel, Vector2.one, new Vector2(statusRight - 324f, -134f + row), new Vector2(statusRight, -34f + row));
                Place(ammoPanel, Vector2.one, new Vector2(statusRight - 324f, -234f + row), new Vector2(statusRight, -138f + row));
                Place(secondaryPanel, Vector2.one, new Vector2(statusRight - 324f, -286f + row), new Vector2(statusRight, -238f + row));
                Place(statusPanel, Vector2.one, new Vector2(statusRight - 324f, -338f + row), new Vector2(statusRight, -290f + row));
                Place(hintPanel, new Vector2(0.5f, 1f), new Vector2(-420f, -322f + row), new Vector2(420f, -202f + row));
                if (killFeedPanel != null) Place(killFeedPanel, new Vector2(0f, 1f), new Vector2(30f, -310f), new Vector2(360f, -190f));
                for (int i = 0; i < killFeedTexts.Length; i++) if (killFeedTexts[i] != null) killFeedTexts[i].alignment = TextAlignmentOptions.MidlineLeft;
                hintText.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                Place(pauseButton, Vector2.one, new Vector2(-106f, -100f), new Vector2(-30f, -24f));
                Place(speedPanel, Vector2.zero, new Vector2(30f, 30f), new Vector2(274f, 254f));
                Place(healthPanel, new Vector2(1f, 0f), new Vector2(-354f, 30f), new Vector2(-30f, 130f));
                Place(ammoPanel, new Vector2(1f, 0f), new Vector2(-354f, 138f), new Vector2(-30f, 234f));
                Place(secondaryPanel, new Vector2(1f, 0f), new Vector2(-354f, 242f), new Vector2(-30f, 290f));
                Place(statusPanel, new Vector2(1f, 0f), new Vector2(-354f, 298f), new Vector2(-30f, 346f));
                Place(hintPanel, Vector2.zero, new Vector2(30f, 272f), new Vector2(680f, 422f));
                if (killFeedPanel != null) Place(killFeedPanel, Vector2.one, new Vector2(-360f, -230f), new Vector2(-30f, -110f));
                for (int i = 0; i < killFeedTexts.Length; i++) if (killFeedTexts[i] != null) killFeedTexts[i].alignment = TextAlignmentOptions.MidlineRight;
                hintText.alignment = TextAlignmentOptions.MidlineLeft;
            }
            ApplyReadableVitals();
            LayoutGoals(goalsExpanded || (race != null && race.State == RaceState.Countdown));
            LayoutHint();
            pauseHint.gameObject.SetActive(true);
        }

        private void ApplyReadableVitals()
        {
            float scale = Mathf.Max(0.01f, hudRoot.lossyScale.x);
            void Font(TextMeshProUGUI text, float min, float max, float pixels)
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = Mathf.Max(min, pixels / scale);
                text.fontSizeMax = Mathf.Max(max, text.fontSizeMin);
            }
            Font(lapText, 20f, 28f, 12f);
            Font(positionText, 20f, 36f, 16f);
            Font(hpText, 16f, 26f, 12f);
            Font(ammoText, 18f, 30f, 12f);
            Font(primaryNameText, 36f, 36f, 14f);
            Font(secondaryText, 16f, 24f, 12f);
            Font(statusText, 14f, 22f, 12f);
            timerText.fontSize = Mathf.Max(40f, 16f / scale);
            speedText.fontSize = Mathf.Max(46f, 18f / scale);
            speedUnit.fontSize = Mathf.Max(16f, 9f / scale);
            // Tiny dial numerals add noise; the needle and large digital speed remain visible.
            foreach (var text in speedPanel.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.name.StartsWith("Scale", System.StringComparison.Ordinal)) text.gameObject.SetActive(16f * scale >= 9f);
            float unitTop = Mathf.Max(35f, 12f + 12f / scale);
            float speedBottom = unitTop + 4f;
            speedText.rectTransform.offsetMin = new Vector2(50f, speedBottom);
            speedText.rectTransform.offsetMax = new Vector2(194f, speedBottom + Mathf.Max(60f, 24f / scale));
            speedUnit.rectTransform.offsetMax = new Vector2(182f, unitTop);

            float lapHeight = Mathf.Max(58f, 22f / scale), positionHeight = Mathf.Max(58f, 26f / scale);
            float raceWidth = Mathf.Max(280f, 116f / scale), raceHeight = lapHeight + positionHeight + 34f;
            Place(racePanel, new Vector2(0f, 1f), new Vector2(30f, -24f - raceHeight), new Vector2(30f + raceWidth, -24f));
            lapText.rectTransform.offsetMin = new Vector2(18f, -12f - lapHeight);
            positionText.rectTransform.offsetMax = new Vector2(-18f, -22f - lapHeight);
            positionText.rectTransform.offsetMin = new Vector2(18f, -22f - lapHeight - positionHeight);
            float timerWidth = Mathf.Max(280f, 110f / scale), timerHeight = Mathf.Max(70f, 30f / scale);
            Place(timerPanel, new Vector2(0.5f, 1f), new Vector2(-timerWidth * 0.5f, -24f - timerHeight), new Vector2(timerWidth * 0.5f, -24f));

            float width = Mathf.Max(324f, 132f / scale), healthHeight = Mathf.Max(100f, 42f / scale);
            // Noto CJK's ascent/descent needs ~1.45 em. Reserve both complete lines:
            // a short box with Ellipsis can discard even a two-character weapon name.
            primaryNameText.fontSize = primaryNameText.fontSizeMax;
            float weaponLineHeight = Mathf.Max(primaryNameText.fontSizeMax * 1.55f,
                primaryNameText.GetPreferredValues(primaryNameText.text, width - 28f, float.PositiveInfinity).y + 4f);
            weaponLineHeight = Mathf.Max(44f, weaponLineHeight);
            float ammoLineHeight = Mathf.Max(40f, ammoText.fontSizeMax * 1.55f);
            float ammoHeight = Mathf.Max(96f, weaponLineHeight + ammoLineHeight + 12f);
            float extraHeight = Mathf.Max(48f, 28f / scale);
            float right = healthPanel.offsetMax.x;
            Vector2 anchor = touchLayout ? Vector2.one : new Vector2(1f, 0f);
            float healthBottom = touchLayout ? healthPanel.offsetMax.y - healthHeight : 30f;
            Place(healthPanel, anchor, new Vector2(right - width, healthBottom), new Vector2(right, healthBottom + healthHeight));
            float ammoBottom = touchLayout ? healthBottom - 4f - ammoHeight : healthBottom + healthHeight + 8f;
            Place(ammoPanel, anchor, new Vector2(right - width, ammoBottom), new Vector2(right, ammoBottom + ammoHeight));
            float secondaryBottom = touchLayout ? ammoBottom - 4f - extraHeight : ammoBottom + ammoHeight + 8f;
            Place(secondaryPanel, anchor, new Vector2(right - width, secondaryBottom), new Vector2(right, secondaryBottom + extraHeight));
            float statusBottom = touchLayout ? secondaryBottom - 4f - extraHeight : secondaryBottom + extraHeight + 8f;
            Place(statusPanel, anchor, new Vector2(right - width, statusBottom), new Vector2(right, statusBottom + extraHeight));
            hpText.rectTransform.offsetMin = new Vector2(60f, healthHeight * 0.48f);
            primaryNameText.rectTransform.offsetMin = new Vector2(14f, -3f - weaponLineHeight);
            ammoText.rectTransform.offsetMax = new Vector2(-18f, 4f + ammoLineHeight);
        }

        private void LayoutHint()
        {
            float scale = Mathf.Max(0.01f, hudRoot.lossyScale.x);
            hintText.fontSize = Mathf.Max(24f, 12f / scale);
            if (!touchLayout)
            {
                float width = Mathf.Max(650f, Mathf.Min(340f, Screen.width * 0.48f) / scale);
                float desktopHeight = Mathf.Max(150f, hintText.GetPreferredValues(hintText.text, width - 36f, float.PositiveInfinity).y + 28f);
                Place(hintPanel, Vector2.zero, new Vector2(30f, speedPanel.offsetMax.y + 18f), new Vector2(30f + width, speedPanel.offsetMax.y + 18f + desktopHeight));
                return;
            }
            float halfWidth = Mathf.Min(420f, Mathf.Max(180f, hudRoot.rect.width * 0.5f + healthPanel.offsetMin.x - 24f));
            var min = hintPanel.offsetMin; var max = hintPanel.offsetMax;
            min.x = -halfWidth; max.x = halfWidth;
            hintPanel.offsetMin = min; hintPanel.offsetMax = max;
            float height = Mathf.Max(120f, hintText.GetPreferredValues(hintText.text, hintPanel.rect.width - 36f, float.PositiveInfinity).y + 28f);
            var bottom = hintPanel.offsetMin;
            bottom.y = hintPanel.offsetMax.y - height;
            hintPanel.offsetMin = bottom;
        }

        // Top-center cards can grow for small screens or long translations. Keep the
        // teaching tip below both the bomb card and the test-drive return button.
        internal void PlaceHintBelowBomb(float bottomPixels)
        {
            if (!touchLayout || hintPanel == null || hudRoot == null) return;
            if (gameFlow != null) bottomPixels = Mathf.Max(bottomPixels, gameFlow.TestDriveOverlayBottomPixels);
            float scale = Mathf.Max(0.01f, hudRoot.lossyScale.x);
            float top = Mathf.Max(layoutWidth < 1400f ? 292f : 202f, bottomPixels > 0f ? (bottomPixels + 6f) / scale : 0f);
            if (Mathf.Abs(hintPanel.offsetMax.y + top) < 0.01f) return;
            float height = hintPanel.rect.height;
            var min = hintPanel.offsetMin; var max = hintPanel.offsetMax;
            max.y = -top; min.y = -top - height;
            hintPanel.offsetMin = min; hintPanel.offsetMax = max;
        }

        private void LayoutGoals(bool expanded)
        {
            if (goalsPanel == null) return;
            float scale = Mathf.Max(0.1f, hudRoot.lossyScale.x);
            float font = Mathf.Max(23f, 12f / scale);
            goalsText.enableAutoSizing = false;
            goalsText.fontSize = font;
            float width = expanded ? Mathf.Min(820f, 500f * font / 23f) : 500f;
            bool mission = RaceSetup.Event != null && RaceSetup.Event.mode == EventMode.Mission;
            // The mission hides lap/position. Use that free corner, keeping expanded goals
            // beside the boss panel instead of covering its route and enemy count.
            if (mission)
                width = Mathf.Min(width, Mathf.Max(260f, (hudRoot.rect.width - MissionHud.StatusWidthPixels(Screen.width, scale) / scale) * 0.5f - 60f));
            float height = expanded ? Mathf.Max(152f, goalsText.GetPreferredValues(goalsText.text, width - 32f, float.PositiveInfinity).y + 24f)
                : Mathf.Max(62f, (touchLayout ? 44f : 28f) / scale);
            float top = mission ? -24f : touchLayout ? -332f : Mathf.Min(-190f, racePanel.offsetMin.y - 16f);
            if (touchLayout && expanded)
                top = Mathf.Max(top, -(hudRoot.rect.height - speedPanel.offsetMax.y - height - 16f));
            Place(goalsPanel, new Vector2(0f, 1f), new Vector2(30f, top - height), new Vector2(30f + width, top));
        }

        private void ApplyReadableOverlays()
        {
            if (hudRoot == null || helpPanel == null || pauseControls == null) return;
            float pixelScale = Mathf.Max(0.01f, hudRoot.lossyScale.x);
            float readable = UiFactory.ReadableScale(Screen.width, Screen.height);
            // Combat confirmation must remain readable at phone resolution, independently
            // of the small targeting reticle's reference-canvas scale.
            combatText.fontSize = Mathf.Max(22f, 14f / pixelScale);
            float combatHalfWidth = Mathf.Min(400f, Screen.width * 0.55f) / (2f * pixelScale);
            Place(combatText.rectTransform, new Vector2(0.5f, 0.5f),
                new Vector2(-combatHalfWidth, 30f / pixelScale), new Vector2(combatHalfWidth, 62f / pixelScale));
            incomingText.fontSize = Mathf.Max(23f, 13f / pixelScale);
            LayoutIncomingFeedback();
            hitMarker.fontSize = Mathf.Max(64f, 28f / pixelScale);
            finishPanel.localScale = Vector3.one * (Mathf.Min(readable, (Screen.width - 32f) / 600f) / pixelScale);
            // Pause stays fully visible; help may scroll without shrinking the lesson text.
            float pauseScale = Mathf.Min(readable, Mathf.Min(Screen.width - 32f, Screen.height - 32f) / 600f);
            pauseControls.transform.localScale = Vector3.one * Mathf.Max(0.1f, pauseScale / pixelScale);
            helpPanel.localScale = Vector3.one * (readable / pixelScale);
            helpPanel.sizeDelta = new Vector2(Mathf.Min(1000f, (Screen.width - 32f) / readable), Mathf.Min(720f, (Screen.height - 32f) / readable));
        }

        private void LayoutIncomingFeedback()
        {
            float scale = Mathf.Max(0.01f, hudRoot.lossyScale.x);
            float padding = 10f / scale;
            float width = Mathf.Min(Mathf.Min(600f, Screen.width * 0.7f) / scale, incomingText.preferredWidth + padding * 2f);
            float height = Mathf.Max(30f / scale, incomingText.GetPreferredValues(incomingText.text, width - padding * 2f, float.PositiveInfinity).y + 12f / scale);
            Place(incomingPanel, new Vector2(0.5f, 0.5f), new Vector2(-width * 0.5f, -56f / scale - height * 0.5f), new Vector2(width * 0.5f, -56f / scale + height * 0.5f));
            incomingText.rectTransform.offsetMin = new Vector2(padding, 6f / scale);
            incomingText.rectTransform.offsetMax = new Vector2(-padding, -6f / scale);
        }

        /// <summary>Test hook: current layout mode.</summary>
        public bool TouchLayout => touchLayout;

        // Touch puts the vital cards at the top right. Event/mission cards share that
        // row and must leave space for the actual readable width of those cards.
        internal float ConstrainTopCenterWidth(float preferredPixels)
        {
            if (!touchLayout || healthPanel == null || hudRoot == null) return preferredPixels;
            float rightEdge = Screen.width + healthPanel.offsetMin.x * hudRoot.lossyScale.x - 8f;
            return Mathf.Min(preferredPixels, Mathf.Max(120f, (rightEdge - Screen.width * 0.5f) * 2f));
        }

        private void Update()
        {
            if (race == null) return;
            UpdateCombatAndGoals();
            UpdateFinishCard();
            if (hudRoot != null && (Mathf.Abs(hudRoot.rect.width - layoutWidth) > 1f || Screen.width != layoutScreenWidth || Screen.height != layoutScreenHeight)) ApplyLayout(touchLayout);
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
                playerInput = playerCar.GetComponent<PlayerDriverInput>();
                lastSecondaryAmmo = -1;
                lastStatusStr = null;
                if (playerWeapons != null) { playerWeapons.PrimarySwitched += OnPrimarySwitched; playerWeapons.PrimarySwitchDenied += OnPrimarySwitchDenied; }
                if (playerCannon != null) { playerCannon.AmmoChanged += OnAmmo; BuildAmmoPips(playerCannon.AmmoMax); OnAmmo(playerCannon.Ammo, playerCannon.AmmoMax); }




                RaceDirector.Instance.OnKillRegistered -= OnKillRegistered;
                RaceDirector.Instance.OnKillRegistered += OnKillRegistered;
                if (playerHealth != null) playerHealth.OnHealthChanged += OnHealth;
                OnHealth(playerHealth != null ? playerHealth.Health : 100f, playerHealth != null ? playerHealth.maxHealth : 100f);
                RefreshPosition();
            }

            UpdateWeaponWheel();
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

            if (playerWeapons != null && playerCannon != null && playerCannon.AmmoMax == 0 && !playerCannon.locked)
            {
                bool overheated = playerWeapons.IsOverheated;
                int heatPct = Mathf.RoundToInt((playerWeapons.CurrentHeat / Mathf.Max(1f, playerWeapons.heatMax)) * 100f);
                if (overheated != lastOverheated || heatPct != lastHeatPct || playerWeapons.primaryType != lastPrimaryType)
                {
                    lastOverheated = overheated;
                    lastHeatPct = heatPct;
                    lastPrimaryType = playerWeapons.primaryType;
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
                        ammoText.text = Loc.Format("hud.heat", "pct", heatPct);
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
                bool planted = playerWeapons.PlantedCharge != null;
                if (cur != lastSecondaryAmmo || ready != lastSecondaryReady || stype != lastSecondaryType || planted != lastChargePlanted)
                {
                    lastSecondaryAmmo = cur;
                    lastSecondaryReady = ready;
                    lastSecondaryType = stype;
                    lastChargePlanted = planted;
                    secondaryPanel.gameObject.SetActive(true);
                    string key = stype == SecondaryWeaponType.Mines ? "hud.secondary.mines"
                        : stype == SecondaryWeaponType.RemoteCharge ? (planted ? "hud.secondary.charge_live" : "hud.secondary.charge")
                        : "hud.secondary.freeze";
                    secondaryText.text = Loc.Format(key, "cur", cur, "max", max);
                    // A planted charge is the one state that asks for a press right now: red, like its light.
                    secondaryText.color = planted ? new Color(1f, 0.25f, 0.2f)
                        : ready ? (cur > 0 ? new Color(1f, 0.6f, 0.2f) : UiFactory.Muted) : new Color(0.7f, 0.7f, 0.7f, 0.5f);
                }
            }
            else if (secondaryPanel != null && secondaryPanel.gameObject.activeSelf)
            {
                secondaryPanel.gameObject.SetActive(false);
            }

            if (hitMarker.gameObject.activeSelf && Time.unscaledTime > hitMarkerUntil) hitMarker.gameObject.SetActive(false);
            if (race.State >= RaceState.Racing) SetTimer(race.PlayerElapsedTime);

            bool hintVisible = Time.unscaledTime <= hintUntil && !(touchLayout && (goalsExpanded || race.State == RaceState.Countdown))
                && race.State >= RaceState.Countdown && race.State <= RaceState.Finishing;
            if (hintPanel.gameObject.activeSelf != hintVisible) hintPanel.gameObject.SetActive(hintVisible);

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
            LayoutHint();
            hintPanel.gameObject.SetActive(true);
            hintUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Last text shown by <see cref="ShowHint"/> (test hook).</summary>
        public string LastHint { get; private set; }

        /// <summary>How long the controls card stays up at the start of a race: long for newcomers, a reminder later.</summary>
        private float ControlsHintSeconds => PlatformGlue.Save != null && PlatformGlue.Save.Data.Races >= 3 ? 5f : 8f;

        public static string ControlsHint(bool touch)
        {
            var ev = RaceSetup.Event;
            if (ev != null && ev.twist == RacingGame.Core.Daily.RaceTwist.HotBomb) return Loc.Get("lesson.bomb");
            if (ev != null && ev.twist == RacingGame.Core.Daily.RaceTwist.MinesOnly) return Loc.Get(touch ? "lesson.mines.touch" : "lesson.mines.keys");
            if (ev != null && ev.spaceTrack) return Loc.Get("lesson.space");
            if (ev != null && ev.abyssTrack) return Loc.Get("lesson.abyss");
            if (ev != null && (ev.starRingTrack || ev.wastelandTrack)) return Loc.Get("lesson.wall");
            if (ev != null && ev.eventId == "e2") return Loc.Get(touch ? "lesson.drift.touch" : "lesson.drift.keys");
            if (ev != null && ev.eventId == "e3") return Loc.Get("lesson.shortcut");
            return Loc.Get(touch ? "lesson.drive.touch" : "lesson.drive.keys");
        }

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
            if (race != null)
            {
                UpdateReticle(); UpdateThreatMarkers(); UpdateToastAndWarnings();
                combatFeedback.anchoredPosition = reticle.gameObject.activeSelf ? reticle.anchoredPosition : Vector2.zero;
                combatFeedback.gameObject.SetActive(playerCar != null && race.State >= RaceState.Countdown && race.State < RaceState.Finished
                    && (playerHealth == null || !playerHealth.IsDead) && Time.unscaledTime <= hitMarkerUntil);
            }
            if (shownCenter != centerText.text)
            {
                shownCenter = centerText.text;
                centerPanel.gameObject.SetActive(!string.IsNullOrEmpty(shownCenter));
                centerPanel.sizeDelta = new Vector2(Mathf.Clamp(centerText.preferredWidth + 64f, 200f, Mathf.Min(900f, hudRoot.rect.width - 80f)), 190f);
            }
            if (pausePanel.gameObject.activeSelf)
            {
                pauseControls.interactable = !helpPanel.gameObject.activeSelf && (PauseController.Reasons & ~PauseReason.User) == PauseReason.None;
                pauseControls.alpha = helpPanel.gameObject.activeSelf ? 0f : 1f;
                pauseControls.blocksRaycasts = !helpPanel.gameObject.activeSelf;
            }
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
            if (playerWeapons != null) SetPrimaryName(playerWeapons.PrimaryType);
            lastReloadPct = -1;
            lastHeatPct = -1; // new race / language / weapon: rebuild heat-only text even at zero heat
            if (ammoPips.Length != Mathf.Clamp(max, 0, 12)) BuildAmmoPips(max);   // swapped between cannon and machine gun
            bool primary = max > 0 && (playerCannon == null || !playerCannon.locked);   // mines-only events lock the cannon
            ammoPanel.gameObject.SetActive(primary);
            if (!primary) { ammoText.text = ""; return; }
            ammoText.text = ammo > 0 ? Loc.Format("hud.ammo", "cur", ammo, "max", max) : Loc.Format("hud.reload", "p", 0);
        }

        private void OnPrimarySwitched(PrimaryWeaponType type)
        {
            SetPrimaryName(type);
            ShowHint(Loc.Get(PrimaryWeaponCatalog.Get(type).NameKey), 1.2f);
        }

        private void SetPrimaryName(PrimaryWeaponType type)
        {
            string title = Loc.Get(PrimaryWeaponCatalog.Get(type).NameKey);
            if (primaryNameText.text == title) return;
            primaryNameText.text = title;
            // Long translated names wrap at a readable size; grow the card only
            // when its content changes, not on every ammo or simulation update.
            if (hudRoot != null) ApplyReadableVitals();
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
            if (!result.Fatal && !result.ShieldAbsorbed && result.DamageDealt > 0f)
                outcome += "  -" + Mathf.CeilToInt(result.DamageDealt);
            Color color = result.Fatal ? HitmarkerKillColor : result.ShieldAbsorbed ? new Color(0.3f, 0.95f, 1f) : HitmarkerNormalColor;
            if (outgoing)
            {
                // Preserve a kill confirmation if another bullet lands during the same burst.
                if (!result.Fatal && hitMarker.color == HitmarkerKillColor && Time.unscaledTime < hitMarkerUntil) return;
                hitMarker.color = color; hitMarker.gameObject.SetActive(true);
                combatText.text = outcome;
                combatText.color = result.Fatal || result.ShieldAbsorbed ? color : Color.white;
                hitMarkerUntil = Time.unscaledTime + (result.Fatal ? 0.8f : 0.55f);
                if (!result.Fatal) SfxPlayer.Instance.PlayUi(result.ShieldAbsorbed ? SfxSynth.ShieldBreak : SfxSynth.Hit, 0.45f, 1.3f);
            }
            else
            {
                incomingSource = hit.Attacker != null ? hit.Attacker.transform.position : hit.Point;
                incomingUntil = Time.time + 0.85f;
                var attacker = hit.Attacker != null ? hit.Attacker.GetComponent<RaceParticipant>() : null;
                string name = attacker != null ? UiFactory.Escape(GetLocalizedRacerName(attacker.displayName)) : Loc.Get("combat.obstacle");
                incomingText.text = name + " · " + outcome;
                incomingArrow.color = color;
                incomingText.color = result.ShieldAbsorbed ? color : Color.white;
                LayoutIncomingFeedback();
                if (result.ShieldAbsorbed) SfxPlayer.Instance.PlayUi(SfxSynth.ShieldBreak, 0.65f);
            }
        }

        private void UpdateCombatAndGoals()
        {
            bool incoming = playerCar != null && Time.time < incomingUntil;
            incomingArrow.gameObject.SetActive(incoming);
            incomingPanel.gameObject.SetActive(incoming);
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
            bool showGoals = race.Player != null && RaceSetup.Event != null && !DailyRace.IsDaily(RaceSetup.Event)
                && (gameFlow == null || !gameFlow.IsTestDrive) && race.State >= RaceState.Countdown && race.State <= RaceState.Finishing;
            goalsPanel.gameObject.SetActive(showGoals);
            if (showGoals)
            {
                race.ReadResult(race.Player, goalEntry);
                bool expanded = goalsExpanded || race.State == RaceState.Countdown;
                goalsText.text = expanded ? StarGoalText.Progress(RaceSetup.Event, goalEntry)
                    : Loc.Format("goals.compact", "n", RaceEventDefinition.CountStars(RaceSetup.Event.StarMask(goalEntry)));
                LayoutGoals(expanded);
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
                string locName = UiFactory.Escape(GetLocalizedRacerName(p.displayName, p.isPlayer));
                killFeedBuffer.RegisterRacerName(carId, locName);
            }
            killFeedBuffer.PrecacheAll();
            killFeedNamesCached = true;
        }

        public static string GetLocalizedRacerName(string rawName, bool isPlayer = false)
        {
            if (isPlayer || string.Equals(rawName, "You", System.StringComparison.OrdinalIgnoreCase))
            {
                if (Loc.TryGet("player.name", out string loc) && !string.IsNullOrEmpty(loc)) return loc;
                return "You";
            }
            if (string.IsNullOrEmpty(rawName)) return "Car";
            string baseName = rawName, suffix = "";
            int split = rawName.LastIndexOf(' ');
            if (split > 0 && int.TryParse(rawName.Substring(split + 1), out _))
            { baseName = rawName.Substring(0, split); suffix = rawName.Substring(split); }
            string key = "ai.name." + baseName.ToLowerInvariant();
            if (Loc.TryGet(key, out string aiLoc) && !string.IsNullOrEmpty(aiLoc)) return aiLoc + suffix;
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

        private void OnStarted() { RefreshPosition(); nextGoalRefresh = 0f; UpdateCombatAndGoals(); }

        private void OnFinished(RaceResults results) { centerText.text = ""; }

        /// <summary>Menu screens hide the HUD; the race shows it.</summary>
        public void SetVisible(bool visible)
        {
            foreach (var c in GetComponentsInChildren<Canvas>(true)) if (c.name == "HUD") c.gameObject.SetActive(visible);
            pausePanel.gameObject.SetActive(visible && PauseController.IsPaused);
            helpPanel.gameObject.SetActive(false);
            finishPanel.gameObject.SetActive(false);
            if (visible)
            {
                SetTimer(0d);
                goalsExpanded = false;
                hintText.text = ControlsHint(touchLayout);
                LayoutHint();
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
            if (pausePanel == null) return;
            pausePanel.gameObject.SetActive(paused);
            if (!paused && helpPanel != null) helpPanel.gameObject.SetActive(false);
            if (paused && resumeButton != null) EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
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
