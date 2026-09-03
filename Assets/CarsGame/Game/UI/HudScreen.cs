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
    public sealed class HudScreen : MonoBehaviour
    {
        public RaceManager race;
        public float hintSeconds = 30f;

        private TextMeshProUGUI speedText, lapText, positionText, timerText, centerText, hintText, hpText, pauseText, ammoText, hitMarker;
        private CarCannon playerCannon;
        private float hitMarkerUntil;
        private Image hpFill;
        private RectTransform pausePanel;
        private CarController playerCar;
        private CarHealth playerHealth;
        private float hintUntil;
        private readonly StringBuilder sb = new StringBuilder(256);

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
        }

        private void OnDisable()
        {
            if (race == null) return;
            race.OnCountdownTick -= OnCountdown;
            race.OnRaceStarted -= OnStarted;
            race.OnRaceFinished -= OnFinished;
            race.OnPositionsChanged -= RefreshPosition;
            PauseController.PausedChanged -= OnPaused;
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

            // Bottom-left: speed + hints
            speedText = UiFactory.Text(root, "Speed", "0 km/h", 56f, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 24f), new Vector2(430f, 104f), new Color(1f, 0.85f, 0.2f));
            hintText = UiFactory.Text(root, "Hints", "", 24f, TextAlignmentOptions.BottomLeft, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(30f, 110f), new Vector2(640f, 260f), new Color(0.9f, 0.9f, 0.9f));

            // Bottom-right: HP bar
            UiFactory.Bar(root, "HpBar", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 30f), new Vector2(-30f, 62f), new Color(0.2f, 0.85f, 0.25f), out hpFill);
            hpText = UiFactory.Text(root, "HpText", "HP 100", 22f, TextAlignmentOptions.Center, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 30f), new Vector2(-30f, 62f));
            ammoText = UiFactory.Text(root, "Ammo", "", 30f, TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, 70f), new Vector2(-30f, 120f), new Color(1f, 0.85f, 0.3f));
            hitMarker = UiFactory.Text(root, "HitMarker", "✕", 64f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-60f, -60f), new Vector2(60f, 60f), new Color(1f, 0.35f, 0.2f));
            hitMarker.gameObject.SetActive(false);

            // Top-left: lap + position
            lapText = UiFactory.Text(root, "Lap", "LAP 1/3", 36f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -110f), new Vector2(400f, -30f));
            positionText = UiFactory.Text(root, "Position", "P1/4", 36f, TextAlignmentOptions.TopLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -170f), new Vector2(400f, -110f));

            // Top-centre: timer
            timerText = UiFactory.Text(root, "Timer", "0:00.000", 40f, TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-200f, -90f), new Vector2(200f, -30f));

            // Centre: countdown / wrong way / messages
            centerText = UiFactory.Text(root, "Center", "", 120f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-500f, -120f), new Vector2(500f, 120f), new Color(1f, 0.9f, 0.3f));

            // Pause overlay (hidden)
            pausePanel = UiFactory.Panel(root, "Pause", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.55f));
            pauseText = UiFactory.Text(pausePanel, "PauseText", "", 90f, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pausePanel.gameObject.SetActive(false);

            hintUntil = Time.unscaledTime + hintSeconds;
            Loc.LanguageChanged += ApplyStaticTexts;
            ApplyStaticTexts();
        }

        private void ApplyStaticTexts()
        {
            bool touch = UnityEngine.InputSystem.Touchscreen.current != null || Application.isMobilePlatform;
            hintText.text = Loc.Get(touch ? "hud.hint.touch" : "hud.hint.keys");
            // On touch the bottom corners belong to the on-screen buttons: park the hint under the timer.
            var hintRt = hintText.rectTransform;
            if (touch)
            {
                hintRt.anchorMin = new Vector2(0.5f, 1f); hintRt.anchorMax = new Vector2(0.5f, 1f);
                hintRt.offsetMin = new Vector2(-500f, -230f); hintRt.offsetMax = new Vector2(500f, -110f);
                hintText.alignment = TextAlignmentOptions.Top;
            }
            pauseText.text = Loc.Get("pause.title") + "\n<size=40%>" + Loc.Get("pause.hint") + "</size>";
            RefreshPosition();
            if (playerHealth != null) OnHealth(playerHealth.Health, playerHealth.maxHealth);
        }

        private void Update()
        {
            if (race == null) return;
            if (playerCar == null && race.Player != null)
            {
                playerCar = race.Player.Car;
                if (playerHealth != null) playerHealth.OnHealthChanged -= OnHealth;
                if (playerCannon != null) playerCannon.AmmoChanged -= OnAmmo;
                playerHealth = race.Player.Health;
                playerCannon = playerCar.GetComponent<CarCannon>();
                if (playerCannon != null) { playerCannon.AmmoChanged += OnAmmo; OnAmmo(playerCannon.Ammo, playerCannon.AmmoMax); }
                RaceDirector.Instance.ShellHit -= OnShellHit;
                RaceDirector.Instance.ShellHit += OnShellHit;
                if (playerHealth != null) playerHealth.OnHealthChanged += OnHealth;
                OnHealth(playerHealth != null ? playerHealth.Health : 100f, playerHealth != null ? playerHealth.maxHealth : 100f);
                RefreshPosition();
            }

            if (playerCar != null) speedText.text = Mathf.RoundToInt(Mathf.Abs(playerCar.SpeedKmh)) + " " + Loc.Get("hud.speed_unit") + (playerCar.IsBoosting ? "  " + Loc.Get("hud.nitro") : "");
            if (playerCannon != null && playerCannon.IsReloading) ammoText.text = Loc.Format("hud.reload", "p", Mathf.RoundToInt(playerCannon.ReloadProgress(SimulationDriver.Instance.Tick) * 100f));
            if (hitMarker.gameObject.activeSelf && Time.unscaledTime > hitMarkerUntil) hitMarker.gameObject.SetActive(false);
            if (race.State >= RaceState.Racing) timerText.text = UiFactory.FormatTime(race.RaceTime);

            if (hintText.gameObject.activeSelf && Time.unscaledTime > hintUntil) hintText.gameObject.SetActive(false);

            if (race.State == RaceState.Racing && race.Player != null && race.Player.Progress != null)
            {
                bool wrong = race.Player.Progress.WrongWay;
                string wrongText = Loc.Get("hud.wrong_way");
                if (wrong && centerText.text != wrongText) { centerText.fontSize = 80f; centerText.color = new Color(1f, 0.3f, 0.2f); centerText.text = wrongText; }
                else if (!wrong && centerText.text == wrongText) centerText.text = "";
            }
        }

        private void OnAmmo(int ammo, int max)
        {
            if (max <= 0) { ammoText.text = ""; return; }
            ammoText.text = ammo > 0 ? new string('|', ammo) + new string('.', max - ammo) : Loc.Format("hud.reload", "p", 0);
        }

        private void OnShellHit(GameObject shooter, GameObject victim)
        {
            if (playerCar == null || shooter != playerCar.gameObject) return;
            hitMarker.gameObject.SetActive(true);
            hitMarkerUntil = Time.unscaledTime + 0.15f;
        }

        private void OnHealth(float current, float max)
        {
            float t = max > 0f ? current / max : 0f;
            hpFill.fillAmount = t;
            hpFill.color = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(0.2f, 0.85f, 0.25f), t);
            hpText.text = playerHealth != null && playerHealth.IsDead ? Loc.Get("hud.wrecked") : Loc.Format("hud.hp", "hp", Mathf.CeilToInt(current));
        }

        private void RefreshPosition()
        {
            if (race.Player == null) return;
            var progress = race.Player.Progress;
            int lap = Mathf.Clamp(progress.LapsCompleted + 1, 1, race.laps);
            lapText.text = Loc.Format("hud.lap", "cur", lap, "total", race.laps);
            positionText.text = Loc.Format("hud.position", "pos", race.PositionOf(race.Player), "n", race.Participants.Count);
        }

        private void OnCountdown(int value)
        {
            centerText.fontSize = 140f; centerText.color = new Color(1f, 0.9f, 0.3f);
            centerText.text = value > 0 ? value.ToString() : Loc.Get("hud.go");
            if (value == 0) Invoke(nameof(ClearCenter), 1f);
        }

        private void ClearCenter() { if (centerText.text == Loc.Get("hud.go")) centerText.text = ""; }

        private void OnStarted() { RefreshPosition(); }

        private void OnFinished(RaceResults results) { centerText.text = ""; }

        /// <summary>Menu screens hide the HUD; the race shows it.</summary>
        public void SetVisible(bool visible)
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            foreach (var c in GetComponentsInChildren<Canvas>(true)) if (c.name == "HUD") c.gameObject.SetActive(visible);
            if (visible) { hintUntil = Time.unscaledTime + hintSeconds; hintText.gameObject.SetActive(true); playerCar = null; }
        }

        private void OnPaused(bool paused) { pausePanel.gameObject.SetActive(paused); }
    }
}
