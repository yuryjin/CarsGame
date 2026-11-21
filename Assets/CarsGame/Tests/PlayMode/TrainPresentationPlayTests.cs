using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class TrainPresentationPlayTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator WhistleFollowsTheLocomotiveAndStillSignalsWithEffectsMuted()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var hud = Object.FindAnyObjectByType<HudScreen>();
            var sfx = SfxPlayer.Instance;
            float volume = sfx.volume, previousDelta = Time.captureDeltaTime;
            string profile = JsonUtility.ToJson(PlatformGlue.Save.Data);
            try
            {
                Time.captureDeltaTime = .06f;
                sfx.volume = 0f;
                flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_m2"), flow.tiers[0]);
                var train = TrainMission.Current;
                while (!train.Route.PassedWhistlePoint && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue(train.Route.PassedWhistlePoint);
                yield return null;
                var engine = train.Route.Locomotive;
                var source = engine.GetComponents<AudioSource>().Single(s => s.clip == SfxSynth.TrainWhistle);
                Assert.Greater(source.spatialBlend, .5f);
                Assert.AreEqual(0f, source.volume);
                Assert.AreEqual(Loc.Get("train.whistle"), hud.LastEvent);
                Assert.IsNotNull(GameObject.Find("TrainBearing"), "Direction must be visible when effects are muted");
                var samples = new float[1024];
                Assert.IsTrue(source.clip.GetData(samples, source.clip.frequency / 2));
                Assert.Greater(samples.Max(), .25f, "The generated whistle contains a sustained audible tone");
                var before = source.transform.position;
                sfx.volume = .45f;
                for (int i = 0; i < 8; i++) yield return null;
                Assert.AreEqual(.45f, source.volume);
                Assert.Greater(Vector3.Distance(source.transform.position, before), 2f);
                Assert.AreEqual(1, engine.GetComponents<AudioSource>().Count(s => s.clip == SfxSynth.TrainWhistle));
                flow.RetryRace(); yield return null;
                Assert.IsTrue(source == null);
                Assert.AreEqual(0, TrainMission.Current.Route.Locomotive.GetComponents<AudioSource>().Count(s => s.clip == SfxSynth.TrainWhistle));
            }
            finally
            {
                flow.EnterMenu();
                sfx.volume = volume;
                Time.captureDeltaTime = previousDelta;
                JsonUtility.FromJsonOverwrite(profile, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
        }
    }
}
