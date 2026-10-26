using System.Collections.Generic;
using NUnit.Framework;
using RacingGame.Core.Platform;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public class MusicDirectorTests
    {
        private sealed class FakeOutput : IMusicOutput
        {
            public readonly List<string> Log = new List<string>();
            public float Volume = -1f;
            public bool Paused;
            public void Play(string track, bool loop, float fadeSeconds) => Log.Add("play " + track);
            public void Stop(float fadeSeconds) => Log.Add("stop");
            public void Sting(string track, float volume) => Log.Add("sting " + track);
            public void Preload(string track) => Log.Add("preload " + track);
            public void SetVolume(float volume) => Volume = volume;
            public void SetPaused(bool paused) => Paused = paused;
            public void Duck(float depth, float attack, float hold, float release) => Log.Add("duck");
        }

        private static (MusicDirector, FakeOutput) Make()
        {
            var o = new FakeOutput();
            return (new MusicDirector(o), o);
        }

        [Test]
        public void MenuPlaysTheMenuLoopAndRepeatsAreIgnored()
        {
            var (d, o) = Make();
            d.OnState(FlowState.Menu, 0f);
            d.OnState(FlowState.Menu, 1f);
            CollectionAssert.AreEqual(new[] { "play menu" }, o.Log);
        }

        [Test]
        public void CountdownIsSilentAndGoStartsAlternatingRaceTracks()
        {
            var (d, o) = Make();
            d.OnState(FlowState.Menu, 0f);
            d.OnState(FlowState.Racing, 1f);
            Assert.AreEqual("stop", o.Log[1]);
            d.OnRaceStarted();
            Assert.AreEqual("play " + MusicDirector.RaceTracks[0], o.Log[o.Log.Count - 1]);
            d.OnState(FlowState.Results, 50f);
            d.OnState(FlowState.Racing, 60f);
            d.OnRaceStarted();
            Assert.AreEqual("play " + MusicDirector.RaceTracks[1], o.Log[o.Log.Count - 1]);
        }

        [Test]
        public void GoOutsideARaceDoesNothing()
        {
            var (d, o) = Make();
            d.OnState(FlowState.Menu, 0f);
            d.OnRaceStarted();
            CollectionAssert.AreEqual(new[] { "play menu" }, o.Log);
        }

        [TestCase(1, "sting win")]
        [TestCase(3, "sting win")]
        [TestCase(4, "sting lose")]
        public void FinishPlaysOneStingByPlace(int place, string expected)
        {
            var (d, o) = Make();
            d.OnState(FlowState.Racing, 0f);
            d.OnRaceStarted();
            d.OnPlayerFinished(place);
            d.OnPlayerFinished(1);
            Assert.AreEqual(expected, o.Log[o.Log.Count - 1]);
            Assert.AreEqual(1, o.Log.FindAll(x => x.StartsWith("sting")).Count);
        }

        [Test]
        public void MenuLoopReturnsAfterTheStingOnResults()
        {
            var (d, o) = Make();
            d.OnState(FlowState.Racing, 0f);
            d.OnRaceStarted();
            d.OnPlayerFinished(2);
            d.OnState(FlowState.Results, 10f);
            Assert.AreNotEqual("stop", o.Log[o.Log.Count - 1], "the sting must not be cut by a stop");
            d.Tick(10f + MusicDirector.ResultsMenuDelay - 0.1f);
            Assert.AreNotEqual("play menu", o.Log[o.Log.Count - 1]);
            d.Tick(10f + MusicDirector.ResultsMenuDelay);
            Assert.AreEqual("play menu", o.Log[o.Log.Count - 1]);
        }

        [Test]
        public void ResultsWithoutAFinishFadeOutThenMenu()
        {
            var (d, o) = Make();
            d.OnState(FlowState.Racing, 0f);
            d.OnRaceStarted();
            d.OnState(FlowState.Results, 20f);
            Assert.AreEqual("stop", o.Log[o.Log.Count - 1]);
            d.Tick(22f);
            Assert.AreEqual("play menu", o.Log[o.Log.Count - 1]);
        }

        [Test]
        public void HardPausesStopMusicSoftPausesOnlyLowerIt()
        {
            var (d, o) = Make();
            d.SetLevel(1f);
            Assert.AreEqual(MusicDirector.BaseVolume, o.Volume, 1e-5f);
            d.SetPauseReasons(PauseReason.User);
            Assert.IsFalse(o.Paused);
            Assert.AreEqual(MusicDirector.BaseVolume * MusicDirector.SoftPauseFactor, o.Volume, 1e-5f);
            d.SetPauseReasons(PauseReason.User | PauseReason.Ad);
            Assert.IsTrue(o.Paused);
            d.SetPauseReasons(PauseReason.Hidden);
            Assert.IsTrue(o.Paused);
            d.SetPauseReasons(PauseReason.None);
            Assert.IsFalse(o.Paused);
            Assert.AreEqual(MusicDirector.BaseVolume, o.Volume, 1e-5f);
        }

        [Test]
        public void MusicSettingFollowsTheSoundToggleUntilChosen()
        {
            Assert.AreEqual(1f, MusicDirector.MusicLevel(new SettingsData { sfx = 1f }));
            Assert.AreEqual(0f, MusicDirector.MusicLevel(new SettingsData { sfx = 0f }), "old save with sound off stays silent");
            Assert.AreEqual(1f, MusicDirector.MusicLevel(new SettingsData { sfx = 0f, music = 1f }));
            Assert.AreEqual(0f, MusicDirector.MusicLevel(new SettingsData { sfx = 1f, music = 0f }));
            Assert.AreEqual(0f, MusicDirector.MusicLevel(null));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void ExplicitEightyPercentSurvivesSaveAndMergeWithoutFollowingEffects(float effects)
        {
            var legacy = SaveMigrator.FromJson("{\"version\":5,\"settings\":{\"sfx\":0,\"music\":0.8}}");
            Assert.AreEqual(0f, MusicDirector.MusicLevel(legacy.settings));
            var chosen = new SaveData { updatedAtUnixMs = 100 };
            chosen.settings.music = 0.8f;
            chosen.settings.musicChosen = true;
            chosen.settings.sfx = effects;
            var restored = SaveMigrator.FromJson(SaveMigrator.ToJson(chosen));
            var merged = SaveMerge.Merge(legacy, restored);
            Assert.IsTrue(merged.settings.musicChosen);
            Assert.AreEqual(0.8f, MusicDirector.MusicLevel(merged.settings));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void OldExplicitMusicToggleKeepsItsChoice(float level)
        {
            var legacy = new SaveData { version = 5 };
            legacy.settings.sfx = 1f - level;
            legacy.settings.music = level;
            var migrated = SaveMigrator.FromJson(SaveMigrator.ToJson(legacy));
            Assert.IsTrue(migrated.settings.musicChosen);
            Assert.AreEqual(level, MusicDirector.MusicLevel(migrated.settings));
        }
    }
}
