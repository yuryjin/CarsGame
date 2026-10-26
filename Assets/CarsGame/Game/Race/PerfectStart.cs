using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Launch skill: a throttle press (the rising edge of the player's own input, before auto-throttle)
    /// in a short window around GO gives a brief boost. Holding it through the countdown does not count,
    /// and auto-throttle never presses anything. Timing uses <see cref="RaceClock"/>, the countdown's clock.
    /// </summary>
    [RequireComponent(typeof(RaceManager))]
    public sealed class PerfectStart : MonoBehaviour
    {
        [Tooltip("The press may come this early before GO (s).")] public float earlySeconds = 0.25f;
        [Tooltip("… or this late after GO (s).")] public float lateSeconds = 0.3f;
        public float boostSeconds = 1.2f;

        private RaceManager race;
        private HudScreen hud;
        private double lastPressAt = double.NaN, goAt = double.NaN;
        private bool wasPressed, judged;
        private static readonly Color PerfectColor = new Color(0.5f, 1f, 0.6f);

        /// <summary>Test hook: the last race awarded a perfect start.</summary>
        public bool Awarded { get; private set; }
        public bool AcceptingInput => !judged && race != null
            && (race.State == RaceState.Countdown || race.State == RaceState.Racing);

        private void Awake() { race = GetComponent<RaceManager>(); hud = GetComponent<HudScreen>(); }
        private void OnEnable() { race.OnFieldSpawned += ResetState; race.OnRaceStarted += OnGo; }
        private void OnDisable() { race.OnFieldSpawned -= ResetState; race.OnRaceStarted -= OnGo; }

        private void ResetState() { lastPressAt = goAt = double.NaN; wasPressed = judged = Awarded = false; }
        private void OnGo() { goAt = RaceClock.Now; }

        private void Update()
        {
            if (judged || race.State < RaceState.Countdown || race.State > RaceState.Racing) return;
            var player = race.Player;
            var input = player != null ? player.GetComponent<PlayerDriverInput>() : null;
            if (input == null || !input.enabled) return;   // autopilot / remote driver
            double now = RaceClock.Now;
            bool pressed = input.RawThrottle > 0.9f;
            // The race clock stands still while paused: a press then would land "inside" the window forever.
            // Track the key state so a key held through the pause is not a fresh press on resume.
            if (PauseController.IsPaused) { wasPressed = pressed; return; }
            if (pressed && !wasPressed) lastPressAt = now;   // latch the press; a release keeps the timestamp
            wasPressed = pressed;

            if (double.IsNaN(goAt)) return;
            bool inWindow = !double.IsNaN(lastPressAt) && lastPressAt >= goAt - earlySeconds && lastPressAt <= goAt + lateSeconds;
            if (!inWindow && now - goAt <= lateSeconds) return;   // still time to press
            judged = true;
            if (!inWindow) return;
            Awarded = true;
            player.Car.ApplyBoost(boostSeconds);
            if (hud != null) hud.ShowEvent(Loc.Get("hud.perfect_start"), PerfectColor);
            SfxPlayer.Instance.PlayUi(SfxSynth.LapDing, 0.8f);
        }
    }
}
