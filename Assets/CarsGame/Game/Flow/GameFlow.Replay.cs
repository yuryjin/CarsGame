using UnityEngine;

namespace RacingGame
{
    public sealed partial class GameFlow
    {
        private GhostCar replayCar;
        private CarCameraFollow replayCamera;
        public bool ViewingReplay => replayCar != null;
        public float ReplayTime { get; private set; }
        public float ReplayDuration => replayCar != null ? replayCar.Record.Duration : 0f;
        public bool ReplayPaused { get; private set; }

        public bool OpenReplay(RaceEventDefinition ev)
        {
            var archive = PlatformGlue.Save?.Data.personalGhosts;
            var prefab = race.spawner != null ? race.spawner.raceCarPrefab : null;
            if (State != FlowState.Menu || ev == null || ev.mode != EventMode.Race || prefab == null
                || archive == null || !archive.HasForEvent(ev.eventId, ev.laps)) return false;

            Teardown();
            ConfigureCircuit(ev, true);
            var record = archive.Find(ev.eventId, TrackHash(ev), ev.laps);
            if (record == null) { Teardown(); return false; }

            replayCar = GhostCar.Spawn(prefab, record, new Color(.65f, .85f, 1f, .8f));
            ReplayTime = 0f;
            ReplayPaused = false;
            MenuCameraDirector.Instance?.Activate(false);
            replayCamera = Camera.main != null ? Camera.main.GetComponent<CarCameraFollow>() : null;
            replayCamera?.SetTarget(replayCar.transform);
            return true;
        }

        public void ToggleReplay()
        {
            if (!ViewingReplay) return;
            if (ReplayPaused && ReplayTime >= ReplayDuration) SeekReplay(0f);
            ReplayPaused = !ReplayPaused;
        }

        public void SeekReplay(float seconds)
        {
            if (!ViewingReplay) return;
            ReplayTime = Mathf.Clamp(seconds, 0f, ReplayDuration);
            replayCar.Seek(ReplayTime);
            replayCamera?.SnapToTarget();
            if (ReplayTime >= ReplayDuration) ReplayPaused = true;
        }

        private void TickReplay()
        {
            if (!ViewingReplay || ReplayPaused || PauseController.IsPaused) return;
            ReplayTime = Mathf.Min(ReplayTime + Time.unscaledDeltaTime, ReplayDuration);
            replayCar.Seek(ReplayTime);
            if (ReplayTime >= ReplayDuration) ReplayPaused = true;
        }

        public void CloseReplay()
        {
            if (ViewingReplay) Teardown();
        }

        private void ReleaseReplayCar()
        {
            if (!ViewingReplay) return;
            if (replayCamera != null) replayCamera.SetTarget(null);
            replayCamera = null;
            replayCar.gameObject.SetActive(false);
            Destroy(replayCar.gameObject);
            replayCar = null;
            MenuCameraDirector.Instance?.Activate(true);
        }
    }
}
