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
            var save = PlatformGlue.Save;
            tracker = new HintTracker(save != null ? save.Data.settings.hintsSeen : 0);
            grassTime = lockedTime = stuckTime = 0f;
            stuckShown = stuckReset = false;
            if (watchedCar != null) watchedCar.Teleported -= OnTeleported;
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

            if (Mathf.Abs(player.Car.SpeedKmh) < 3f) stuckTime += dt;
            else { OnTeleported(); stuckReset = false; }
            if (stuckTime >= 2f && !stuckShown)
            {
                stuckShown = true;
                hud.ShowHint(Loc.Get("hint.stuck"), hintSeconds);
            }
            if (stuckTime >= 4f && !stuckReset && PlatformGlue.Save != null && PlatformGlue.Save.Data.Races < 3)
            {
                stuckReset = true;
                player.Car.ResetCar();
                if (player.Health != null) player.Health.SetInvulnerable(1.5f);
            }

            grassTime = player.Car.Surface == GroundSurface.Ground ? grassTime + dt : 0f;
            if (grassTime > grassSeconds) Try(Hint.Grass, "hint.grass");

            // A target sits in the reticle and the player keeps not shooting: say where the trigger is.
            bool idleLock = hud.ReticleLocked && !player.Car.LastCommand.Fire;
            lockedTime = idleLock ? lockedTime + dt : 0f;
            if (lockedTime > lockedNoFireSeconds) Try(Hint.AimFire, hud.TouchLayout ? "hint.aim.touch" : "hint.aim.keys");
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

        private void Try(Hint hint, string key)
        {
            // Only while the HUD is up: a pickup rolled into on the results screen must not use up its tip.
            if (tracker == null || race.State != RaceState.Racing || race.RaceTime < quietAfterGoSeconds || !tracker.TryShow(hint, Time.unscaledTime)) return;
            hud.ShowHint(Loc.Get(key), hintSeconds);
            var save = PlatformGlue.Save;
            if (save == null) return;
            save.Data.settings.hintsSeen = tracker.Mask;
            save.MarkDirty();
        }
    }
}
