using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Wires <see cref="MusicDirector"/> to the game: flow state, GO, the player's finish, pause
    /// reasons and the music setting. Created by itself in the game scene (no scene or GameFlow
    /// edits), skipped in batch mode so tests and the race harness stay silent.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        public static MusicPlayer Current { get; private set; }

        private MusicDirector director;
        private GameFlow flow;
        private RaceManager race;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (Application.isBatchMode || Current != null) return;
            var flow = FindAnyObjectByType<GameFlow>();
            if (flow == null) return;
            new GameObject("MusicPlayer").AddComponent<MusicPlayer>().Attach(flow);
        }

        public void Attach(GameFlow gameFlow)
        {
            Current = this;
#if UNITY_WEBGL && !UNITY_EDITOR
            IMusicOutput output = new WebMusicOutput();
#else
            IMusicOutput output = gameObject.AddComponent<UnityMusicOutput>();
#endif
            director = new MusicDirector(output);
            director.SetLevel(0f);                           // silent until the save says otherwise
            director.SetPauseReasons(PauseController.Reasons);
            flow = gameFlow;
            race = flow.GetComponent<RaceManager>();
            flow.StateChanged += OnState;
            PauseController.PausedChanged += OnPausedChanged;
            if (race != null) { race.OnRaceStarted += OnRaceStarted; race.OnParticipantFinished += OnFinished; }
            director.OnState(flow.State, Time.unscaledTime);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (flow != null) flow.StateChanged -= OnState;
            PauseController.PausedChanged -= OnPausedChanged;
            if (race != null) { race.OnRaceStarted -= OnRaceStarted; race.OnParticipantFinished -= OnFinished; }
        }

        private void OnState(FlowState s) => director.OnState(s, Time.unscaledTime);
        private void OnRaceStarted() => director.OnRaceStarted();
        private void OnPausedChanged(bool _) => director.SetPauseReasons(PauseController.Reasons);

        private void OnFinished(RaceParticipant p)
        {
            if (p != null && p.isPlayer) director.OnPlayerFinished(race.PositionOf(p));
        }

        /// <summary>Dip the music under a loud effect (see <see cref="SfxPlayer"/>).</summary>
        public void Duck(float depth, float attack = 0.05f, float hold = 0.35f, float release = 0.9f)
            => director?.Duck(depth, attack, hold, release);

        private void Update()
        {
            if (director == null) return;
            var save = PlatformGlue.Save;
            // Polled (a float compare) so the settings screen needs no call into the music code.
            director.SetLevel(save != null && save.Loaded ? MusicDirector.MusicLevel(save.Data.settings) : 0f);
            director.SetPauseReasons(PauseController.Reasons);  // reasons can change without a pause flip
            var player = race != null ? race.Player : null;
            var progress = player != null ? player.Progress : null;
            bool finalLap = progress != null && race.State == RaceState.Racing && !race.IsArenaMode && !race.IsMissionMode
                && progress.LapsCompleted == race.laps - 1;
            float gap = float.PositiveInfinity;
            if (finalLap)
                foreach (var other in race.Participants)
                    if (other != player && other.Progress != null && !race.HasFinished(other) && !RaceManager.IsOut(other))
                        gap = Mathf.Min(gap, Mathf.Abs(other.Progress.TotalProgress - progress.TotalProgress));
            director.SetFinalLapGap(finalLap, gap);
            director.Tick(Time.unscaledTime);
        }
    }
}
