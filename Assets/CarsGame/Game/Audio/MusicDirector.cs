using RacingGame.Core.Platform;
using RacingGame.Core.Save;

namespace RacingGame
{
    /// <summary>Where music actually plays: the page's &lt;audio&gt; elements on WebGL, AudioSources elsewhere.</summary>
    public interface IMusicOutput
    {
        /// <summary>Crossfade the music channel to <paramref name="track"/> (a no-op if it already plays).</summary>
        void Play(string track, bool loop, float fadeSeconds);
        void Stop(float fadeSeconds);
        /// <summary>A one-shot over the music channel (win/lose).</summary>
        void Sting(string track, float volume);
        void Preload(string track);
        void SetVolume(float volume);
        void SetPaused(bool paused);
        /// <summary>Dip the music by <paramref name="depth"/> (0..1) for a loud moment, then recover.</summary>
        void Duck(float depth, float attack, float hold, float release);
    }

    /// <summary>
    /// Picks the music for the game loop: menu loop → silence under the countdown (the engines
    /// rev) → a race loop from GO (alternating between races) → win/lose sting when the player
    /// finishes → the menu loop again a few seconds into the results. Pauses for ads, the host
    /// overlay and a hidden tab; plays quieter under the pause menu. Pure logic over
    /// <see cref="IMusicOutput"/>; <see cref="MusicPlayer"/> feeds it events and time.
    /// </summary>
    public sealed class MusicDirector
    {
        public const string Menu = "menu", Win = "win", Lose = "lose";
        public static readonly string[] RaceTracks = { "race_a", "race_b" };
        /// <summary>Music sits under the effects: full setting = this output volume.</summary>
        public const float BaseVolume = 0.55f;
        public const float SoftPauseFactor = 0.4f;
        public const float ResultsMenuDelay = 3.5f;
        /// <summary>Podium places that get the win sting.</summary>
        public const int WinPlaces = 3;
        private const PauseReason HardPause = PauseReason.Ad | PauseReason.HostPause | PauseReason.Hidden;
        /// <summary>
        /// <see cref="SettingsData.music"/> before a music control existed. Without an explicit
        /// choice this value follows the old sound toggle, preserving muted legacy saves.
        /// </summary>
        public const float UnsetMusicSetting = 0.8f;

        private readonly IMusicOutput output;
        private FlowState? state;
        private int races;
        private bool stung;
        private float menuAt = -1f;
        private float level = -1f;
        private PauseReason reasons;
        private float appliedVolume = -1f;
        private bool appliedPaused, appliedOnce;

        public MusicDirector(IMusicOutput output) { this.output = output; }

        public FlowState? State => state;

        public static float MusicLevel(SettingsData s)
        {
            if (s == null) return 0f;
            if (!s.musicChosen && s.music == UnsetMusicSetting) return s.sfx > 0f ? 1f : 0f;
            return s.music < 0f ? 0f : (s.music > 1f ? 1f : s.music);
        }

        public void OnState(FlowState s, float now)
        {
            if (state == s) return;             // GameFlow re-announces the state (e.g. reward doubled)
            state = s;
            switch (s)
            {
                case FlowState.Menu:
                    menuAt = -1f;
                    output.Play(Menu, true, 1.5f);
                    break;
                case FlowState.Racing:
                    menuAt = -1f; stung = false;
                    output.Stop(0.8f);
                    output.Preload(Win); output.Preload(Lose);
                    break;
                case FlowState.Results:
                    if (!stung) output.Stop(1f);
                    menuAt = now + (stung ? ResultsMenuDelay : 1.5f);
                    break;
            }
        }

        /// <summary>GO: the race loop starts (each race takes the next track).</summary>
        public void OnRaceStarted()
        {
            if (state != FlowState.Racing) return;
            output.Play(RaceTracks[races++ % RaceTracks.Length], true, 0.25f);
        }

        public void OnPlayerFinished(int position)
        {
            if (stung || state != FlowState.Racing) return;
            stung = true;
            output.Stop(0.3f);
            output.Sting(position >= 1 && position <= WinPlaces ? Win : Lose, 1f);
        }

        public void Tick(float now)
        {
            if (menuAt < 0f || now < menuAt) return;
            menuAt = -1f;
            if (state == FlowState.Results) output.Play(Menu, true, 2.5f);
        }

        /// <summary>Music setting 0..1 (see <see cref="MusicLevel"/>).</summary>
        public void SetLevel(float value) { level = value; Apply(); }

        public void SetPauseReasons(PauseReason value) { reasons = value; Apply(); }

        public void Duck(float depth, float attack, float hold, float release) => output.Duck(depth, attack, hold, release);

        private void Apply()
        {
            bool hard = (reasons & HardPause) != 0;
            float soft = reasons != PauseReason.None && !hard ? SoftPauseFactor : 1f;
            float volume = (level < 0f ? 0f : level) * BaseVolume * soft;
            if (!appliedOnce || hard != appliedPaused) output.SetPaused(hard);
            if (!appliedOnce || volume != appliedVolume) output.SetVolume(volume);
            appliedOnce = true; appliedPaused = hard; appliedVolume = volume;
        }
    }
}
