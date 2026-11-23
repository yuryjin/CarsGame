using UnityEngine;
using RacingGame.Core.Onboarding;

namespace RacingGame
{
    /// <summary>
    /// One-time tips at the moment they matter (first time on grass, under fire, a locked target the
    /// player does not shoot, each pickup kind) instead of a wall of text before the start, plus a short
    /// toast for every pickup the player takes. Seen tips live in <c>settings.hintsSeen</c>.
    /// </summary>
    [RequireComponent(typeof(RaceManager))]
    public sealed class OnboardingHints : MonoBehaviour
    {
        public float grassSeconds = 0.7f;
        public float lockedNoFireSeconds = 2f;
        public float hintSeconds = 5f;
        [Tooltip("No tips right after GO: the controls card is what a newcomer reads then (the grid also locks the reticle).")]
        public float quietAfterGoSeconds = 8f;

        private RaceManager race;
        private HudScreen hud;
        private HintTracker tracker;
        private float grassTime, lockedTime, stuckTime;
        private bool stuckShown, stuckReset;
        private CarController watchedCar;
        private static readonly Color PickupColor = new Color(0.55f, 1f, 0.7f);

        private void Awake()
        {
            race = GetComponent<RaceManager>();
            hud = GetComponent<HudScreen>();
        }

        private void OnEnable()
        {
            Pickup.PickedUp += OnPickedUp;
            race.OnRaceStarted += OnRaceStarted;
        }

        private void OnDisable()
        {
            Pickup.PickedUp -= OnPickedUp;
            race.OnRaceStarted -= OnRaceStarted;
            var director = RaceDirector.Current; if (director != null) director.ShellHit -= OnShellHit;
            if (watchedCar != null) watchedCar.Teleported -= OnTeleported;
        }

        private void OnRaceStarted()
        {
            if (watchedCar != null) watchedCar.Teleported -= OnTeleported;
            watchedCar = null;
            var flow = GetComponent<GameFlow>();
            if (flow != null && flow.IsTestDrive && !flow.IsPractice) { tracker = null; return; }
            var save = PlatformGlue.Save;
            tracker = new HintTracker(flow != null && flow.IsPractice ? 0 : save != null ? save.Data.settings.hintsSeen : 0);
            grassTime = lockedTime = stuckTime = 0f;
            stuckShown = stuckReset = false;
            watchedCar = race.Player != null ? race.Player.Car : null;
            if (watchedCar != null) watchedCar.Teleported += OnTeleported;
            var director = RaceDirector.Instance;
            director.ShellHit -= OnShellHit;
            director.ShellHit += OnShellHit;
        }

        private void Update()
        {
            if (tracker == null || hud == null || race.State != RaceState.Racing || PauseController.IsPaused) return;
            var player = race.Player;
            if (player == null || player.Car == null || (player.Health != null && player.Health.IsDead)) return;
            float dt = Time.unscaledDeltaTime;
            var ev = RaceSetup.Event;
            if (ev != null)
            {
                var lesson = GetComponent<GameFlow>()?.CurrentPractice ?? PracticeLesson.None;
                bool normal = lesson == PracticeLesson.None;
                if ((normal || lesson == PracticeLesson.Drift) && player.DriftBoosts > 0) Try(Hint.DriftSuccess, "lesson.drift.done");
                if ((normal || lesson == PracticeLesson.Shortcut) && player.Shortcuts > 0)
                    Try(Hint.ShortcutSuccess, normal ? "lesson.shortcut.done" : "practice.completed");
                if ((normal || lesson == PracticeLesson.WallRide) && player.CeilingBoosts > 0) Try(Hint.CeilingSuccess, "lesson.wall.done");
                if (ev.starRingTrack || ev.wastelandTrack) Try(Hint.WallRide, "lesson.wall");
                else if ((ev.eventId == "e3" || ev.eventId == "practice_shortcut") && player.Progress != null && player.Progress.Path != null && player.Progress.Path.Fork != null)
                    Try(Hint.Shortcut, "lesson.shortcut");
                else if ((normal || lesson == PracticeLesson.Drift) && ev.eventId != "e1" && player.Car.SpeedKmh >= 40f)
                    Try(Hint.Drift, hud.TouchLayout ? "lesson.drift.touch" : "lesson.drift.keys");
            }

            float speed = Mathf.Abs(player.Car.SpeedKmh);
            var command = player.Car.LastCommand;
            bool tryingToMove = Mathf.Abs(command.Throttle) > 0.1f && !command.Brake;
            // Reading a tip, waiting with manual throttle, or holding the brake is intentional.
            // Teleporting those drivers also unfairly invalidates their clean-race goal.
            if (speed < 3f && tryingToMove) stuckTime += dt;
            else
            {
                stuckTime = 0f;
                stuckShown = false;
                if (speed >= 3f) stuckReset = false;
            }
            if (stuckTime >= 2f && !stuckShown)
            {
                stuckShown = true;
                string hint = TouchControlsPresenter.IsShown
                    ? Loc.Format("hint.stuck.touch", "button", Loc.Get("touch.reset")) : Loc.Get("hint.stuck");
                hud.ShowHint(hint, hintSeconds);
            }
            if (stuckTime >= 4f && !stuckReset && PlatformGlue.Save != null && PlatformGlue.Save.Data.Races < 3)
            {
                stuckReset = true;
                player.Car.ResetCar();
                if (!GameFlow.RoomReplica && player.Health != null) player.Health.SetInvulnerable(1.5f);
            }

            grassTime = player.Car.Surface == GroundSurface.Ground ? grassTime + dt : 0f;
            if (grassTime > grassSeconds) Try(Hint.Grass, "hint.grass");

            // A target sits in the reticle and the player keeps not shooting: say where the trigger is.
            bool idleLock = player.Car.Cannon != null && !player.Car.Cannon.locked && hud.ReticleLocked && !player.Car.LastCommand.Fire;
            lockedTime = idleLock ? lockedTime + dt : 0f;
            bool earlyAim = RaceSetup.IsIntroRace && race.RaceTime <= 15d;
            if (lockedTime > (earlyAim ? 0f : lockedNoFireSeconds)) Try(Hint.AimFire, hud.TouchLayout ? "hint.aim.touch" : "hint.aim.keys", earlyAim);
        }

        private void OnTeleported()
        {
            stuckTime = 0f;
            stuckShown = stuckReset;
            // Keep the automatic reset latched until the player moves away.
        }

        private void OnShellHit(GameObject shooter, GameObject victim)
        {
            var player = race.Player;
            if (player != null && victim == player.gameObject && race.State == RaceState.Racing) Try(Hint.UnderFire, "hint.under_fire");
        }

        private void OnPickedUp(Pickup pickup, CarController car)
        {
            var player = race.Player;
            if (player == null || car != player.Car || hud == null) return;
            switch (pickup.kind)
            {
                case PickupKind.Health:
                    hud.ShowEvent(Loc.Format("pickup.health", "n", Mathf.RoundToInt(pickup.healAmount)), PickupColor);
                    Try(Hint.PickupHealth, "hint.pickup.health");
                    break;
                case PickupKind.Ammo:
                    hud.ShowEvent(Loc.Get("pickup.ammo"), PickupColor);
                    Try(Hint.PickupAmmo, "hint.pickup.ammo");
                    break;
                case PickupKind.Nitro:
                    hud.ShowEvent(Loc.Get("pickup.nitro"), PickupColor);
                    Try(Hint.PickupNitro, "hint.pickup.nitro");
                    break;
            }
        }

        private void Try(Hint hint, string key, bool early = false)
        {
            // Only while the HUD is up: a pickup rolled into on the results screen must not use up its tip.
            if (tracker == null || race.State != RaceState.Racing || (race.RaceTime < quietAfterGoSeconds && !early) || !tracker.TryShow(hint, Time.unscaledTime)) return;
            hud.ShowHint(Loc.Get(key), hintSeconds);
            if (GetComponent<GameFlow>()?.IsTestDrive == true) return;
            var save = PlatformGlue.Save;
            if (save == null) return;
            save.Data.settings.hintsSeen = tracker.Mask;
            save.MarkDirty();
        }
    }
}
