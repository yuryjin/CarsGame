using System.Collections;
using NUnit.Framework;
using RacingGame.Core.Platform;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class FinalLapMusicPlayTests
    {
        [UnityTest, Timeout(420000)]
        public IEnumerator ThePlayerHearsPercussionOnlyDuringTheContestedFinish()
        {
            float clock = Time.captureDeltaTime;
            GameFlow flow = null;
            RaceEventDefinition practice = null;
            string saved = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float timeout = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
                yield return null; yield return null;
                saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>();
                var music = MusicPlayer.Current;
                if (music == null) { music = new GameObject("MusicPlayer").AddComponent<MusicPlayer>(); music.Attach(flow); }
                var layer = music.transform.Find("FinalLapPercussion").GetComponent<AudioSource>();
                practice = Object.Instantiate(System.Array.Find(flow.events, e => e != null && e.eventId == "e1"));
                practice.laps = 1; practice.aiCountOverride = 1; practice.pickupCount = 0;
                PlatformGlue.Save.Data.settings.musicChosen = true;
                PlatformGlue.Save.Data.settings.music = .8f;
                PlatformGlue.Save.Data.settings.sfx = .8f;
                foreach (bool close in new[] { true, false })
                {
                    flow.StartRace(practice, flow.tiers[0]);
                    Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
                    while (race.State == RaceState.Countdown) yield return null;
                    var player = race.Player;
                    var progress = player.Progress;
                    var rival = race.Participants[0] == player ? race.Participants[1] : race.Participants[0];
                    var gunner = rival.GetComponent<AiGunner>();
                    if (gunner != null) gunner.enabled = false;
                    rival.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                    while ((!progress.Counter.LineArmed || progress.Path.Length - progress.DistanceFromStart > 93f)
                        && race.State == RaceState.Racing && race.RaceTime < 155d) yield return null;
                    Assert.IsTrue(progress.Counter.LineArmed);
                    var path = progress.Path;
                    float toLine = path.Length - progress.DistanceFromStart;
                    Assert.Less(toLine, 96f);
                    float targetGap = close ? 7f : 140f;
                    path.Pose(progress.Distance - targetGap, -2f, out var p, out var rot);
                    rival.Car.Teleport(p + Vector3.up * .45f, rot);
                    // Give this late-joining test rival the same completed gates as the leader.
                    // The finish itself uses the ordinary car physics, lap and race code.
                    var counter = rival.Progress.Counter;
                    float line = path.startLineDistance;
                    counter.Begin(path.Wrap(line - 1f));
                    float length = path.Length - toLine - targetGap;
                    for (float d = 0f; d <= length; d += 1f) counter.Step(path.Wrap(line + d));
                    rival.Progress.Resync();
                    if (close)
                    {
                        rival.Car.Body.constraints = player.Car.Body.constraints;
                        rival.Car.Body.linearVelocity = rot * Vector3.forward * player.Car.Body.linearVelocity.magnitude;
                    }
                    using (var capture = new AgentAudioCapture(close ? "last-lap-close" : "last-lap-clear"))
                    {
                        if (capture.Active) Time.captureDeltaTime = 0f;
                        var live = new System.Collections.Generic.List<AudioSource>(music.GetComponentsInChildren<AudioSource>());
                        foreach (var voice in SfxPlayer.Instance.GetComponentsInChildren<AudioSource>())
                            if (voice.name.StartsWith("UiVoice_")) live.Add(voice);
                        if (capture.Active) capture.Solo(live.ToArray());
                        float until = Time.realtimeSinceStartup + 8f;
                        while (close && !layer.isPlaying && race.State == RaceState.Racing && Time.realtimeSinceStartup < until) yield return null;
                        for (int i = 0; i < 3; i++) yield return null;
                        capture.Mark(close ? "close" : "clear");
                        Assert.AreEqual(close, layer.isPlaying);
                        if (close)
                        {
                            PauseController.Set(PauseReason.User, true);
                            yield return null;
                            capture.Mark("pause"); Assert.IsFalse(layer.isPlaying);
                            if (capture.Active) yield return new WaitForSecondsRealtime(.9f);
                            PauseController.Set(PauseReason.User, false);
                            yield return null; yield return null;
                            capture.Mark("resume"); Assert.IsTrue(layer.isPlaying);
                        }
                        capture.Mark("lock-warning");
                        SfxPlayer.Instance.PlayUi(SfxSynth.Beep, .45f, 1.6f);
                        float gap = Mathf.Abs(player.Progress.TotalProgress - rival.Progress.TotalProgress);
                        while (!race.HasFinished(player) && race.RaceTime < 180d) yield return null;
                        yield return null;
                        capture.Mark("finish");
                        Assert.IsTrue(race.HasFinished(player));
                        Assert.IsFalse(layer.isPlaying);
                        if (capture.Active) yield return new WaitForSecondsRealtime(1.2f);
                        Debug.Log($"[FinalLapMusic] close={close} rival gap at warning={gap:F1}m, player={race.PlayerElapsedTime:F3}s");
                    }
                    flow.EnterMenu(); yield return null;
                    Time.captureDeltaTime = SimulationDriver.FixedStep;
                }
            }
            finally
            {
                PauseController.ResetAll();
                if (flow != null) flow.EnterMenu();
                if (practice != null) Object.Destroy(practice);
                if (saved != null) { JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data); PlatformGlue.Save.MarkDirty(); }
                Time.captureDeltaTime = clock;
            }
        }
    }
}
