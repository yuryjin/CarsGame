using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace RacingGame.Tests
{
    public sealed class AudioMixerPlayTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator MutingEffectsImmediatelySilencesAnOngoingSkid()
        {
            float capture = Time.captureDeltaTime;
            GameFlow flow = null;
            float volume = 0.8f;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                PauseController.ResetAll();
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float until = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue(PlatformGlue.IsReady);
                yield return null; yield return null;
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>();
                volume = SfxPlayer.Instance.volume;
                SfxPlayer.Instance.volume = 1f;
                Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
                while (race.State == RaceState.Countdown) yield return null;
                var car = race.Player.Car;
                var audio = car.GetComponent<EngineAudio>();
                Assert.NotNull(audio);
                for (int i = 0; i < 45 && audio.ScreechVolume < 0.05f; i++)
                {
                    car.Body.linearVelocity = car.transform.forward * 20f + car.transform.right * 8f;
                    yield return null;
                }
                Assert.Greater(audio.ScreechVolume, 0.05f, "Control: a real grounded side-slip is producing tyre noise");
                SfxPlayer.Instance.volume = 0f;
                yield return null;
                Assert.AreEqual(0f, audio.ScreechVolume, "Muting must not wait for the skid fade-out");
            }
            finally
            {
                if (flow != null) flow.EnterMenu();
                if (SfxPlayer.Current != null) SfxPlayer.Current.volume = volume;
                Time.captureDeltaTime = capture;
                PauseController.ResetAll();
            }
        }

        private sealed class Output : IMusicOutput
        {
            public int Ducks;
            public void Play(string track, bool loop, float fadeSeconds) { }
            public void Stop(float fadeSeconds) { }
            public void Sting(string track, float volume) { }
            public void Preload(string track) { }
            public void SetFinalLap(bool active, float tempo) { }
            public void SetVolume(float volume) { }
            public void SetPaused(bool paused) { }
            public void Duck(float depth, float attack, float hold, float release) { Ducks++; }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator VoicesFollowEffectsSettingEvenAfterPlaybackHasStarted()
        {
            var sfx = SfxPlayer.Instance;
            float volume = sfx.volume;
            var clip = AudioClip.Create("MixerVolumeRegression", 88200, 1, 44100, false);
            try
            {
                sfx.volume = 0.8f;
                sfx.PlayUi(clip, 0.5f);
                sfx.Play(clip, Vector3.zero, 0.25f);
                yield return null; yield return null;
                var voices = System.Array.FindAll(sfx.GetComponentsInChildren<AudioSource>(), s => s.clip == clip);
                Assert.AreEqual(2, voices.Length);
                foreach (var source in voices)
                    Assert.AreEqual(source.name.StartsWith("UiVoice_") ? 0.4f : 0.2f, source.volume, 0.0001f);
                sfx.volume = 0f;
                yield return null; yield return null;
                foreach (var source in voices) Assert.AreEqual(0f, source.volume, "Muting must also silence sounds already playing");
                sfx.volume = 0.4f;
                yield return null; yield return null;
                foreach (var source in voices)
                    Assert.AreEqual(source.name.StartsWith("UiVoice_") ? 0.2f : 0.1f, source.volume, 0.0001f,
                        "Unmuting preserves the individual voice's original gain");
            }
            finally
            {
                foreach (var source in sfx.GetComponentsInChildren<AudioSource>())
                    if (source.clip == clip) { source.Stop(); source.clip = null; }
                sfx.volume = volume;
                Object.DestroyImmediate(clip);
            }
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator MutedEffectsDoNotDuckMusic()
        {
            var sfx = SfxPlayer.Instance;
            float volume = sfx.volume;
            var previous = MusicPlayer.Current;
            var musicObject = new GameObject("MusicDuckRegression");
            var music = musicObject.AddComponent<MusicPlayer>();
            var output = new Output();
            var clip = AudioClip.Create("MixerDuckRegression", 88200, 1, 44100, false);
            var current = typeof(MusicPlayer).GetProperty(nameof(MusicPlayer.Current));
            // Install a passive output at the real MusicPlayer boundary; no playback or profile edits.
            typeof(MusicPlayer).GetField("director", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(music, new MusicDirector(output));
            current.SetValue(null, music);
            try
            {
                sfx.volume = 0f;
                sfx.PlayUi(clip, 1f);
                Assert.AreEqual(0, output.Ducks, "An inaudible effect must not lower independently enabled music");
                yield return null;
                sfx.volume = 1f;
                sfx.PlayUi(clip, 1f);
                Assert.AreEqual(1, output.Ducks, "An audible loud effect still makes room in the mix");
            }
            finally
            {
                Object.DestroyImmediate(musicObject);
                current.SetValue(null, previous);
                foreach (var source in sfx.GetComponentsInChildren<AudioSource>())
                    if (source.clip == clip) { source.Stop(); source.clip = null; }
                sfx.volume = volume;
                Object.DestroyImmediate(clip);
            }
        }
    }
}
