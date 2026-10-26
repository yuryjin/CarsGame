using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Ghost;
using RacingGame.Core.Platform;
using RacingGame.Core.Economy;

namespace RacingGame.Tests
{
    public sealed class GhostPlaybackPlayTests
    {
        private GameFlow flow;
        private GhostCar ghost;
        private RaceManager race;
        private RaceParticipant player;
        private float capture;
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (ghost != null) Object.DestroyImmediate(ghost.gameObject);
            if (flow != null) flow.EnterMenu();
            PauseController.ResetAll(); Time.captureDeltaTime = capture;
        }

        private IEnumerator BootFrozenPlayer()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
            while (race.State == RaceState.Countdown) yield return null;
            player = race.Player;
            player.enabled = false; player.Progress.enabled = false; player.Car.enabled = false;
            player.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
            player.Car.Teleport(new Vector3(0f, 500f, 0f), Quaternion.identity);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RecordingAndReplayingAnInvertedPosePreservesTheWholeOrientation()
        {
            yield return BootFrozenPlayer();
            Quaternion pose = Quaternion.Euler(17f, 70f, 155f);
            player.Car.Teleport(new Vector3(0f, 500f, 0f), pose);
            var recorder = flow.GetComponent<GhostRecorder>();
            recorder.Begin(player, 123, "Wall rider");
            for (int tick = 0; tick < 20; tick++) yield return new WaitForFixedUpdate();
            var record = GhostRecord.FromBytes(recorder.Stop().ToBytes());
            Assert.NotNull(record);
            Assert.GreaterOrEqual(record.Samples.Count, 2);
            int cars = RaceDirector.Instance.Cars.Count;
            ghost = GhostCar.Spawn(race.spawner.raceCarPrefab, record, new Color(.6f, .8f, 1f, .35f));
            ghost.Begin();
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.Less(Quaternion.Angle(pose, ghost.transform.rotation), .1f,
                "A ghost must reproduce pitch and roll on walls and ceilings, not only yaw");
            Assert.AreEqual(cars, RaceDirector.Instance.Cars.Count);
            Assert.IsEmpty(ghost.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(ghost.GetComponentsInChildren<Rigidbody>());
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RecordingATankReplaysItsSilhouetteWithoutLiveComponents()
        {
            yield return BootFrozenPlayer();
            CarBody.Apply(player.gameObject, CarBodies.Get("tortoise"));
            var recorder = flow.GetComponent<GhostRecorder>();
            recorder.Begin(player, 123, "Tank rider");
            for (int tick = 0; tick < 20; tick++) yield return new WaitForFixedUpdate();
            var record = GhostRecord.FromBytes(recorder.Stop().ToBytes());
            Assert.NotNull(record);
            int cars = RaceDirector.Instance.Cars.Count;
            ghost = GhostCar.Spawn(race.spawner.raceCarPrefab, record, Color.cyan);
            yield return null;
            Assert.IsTrue(System.Array.Exists(ghost.GetComponentsInChildren<MeshFilter>(),
                filter => filter.sharedMesh != null && filter.sharedMesh.name == "GhostVisual_tortoise"),
                "A recorded tank must not turn into the stock sports car during replay");
            Assert.AreEqual(cars, RaceDirector.Instance.Cars.Count);
            Assert.IsEmpty(ghost.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(ghost.GetComponentsInChildren<Rigidbody>());
            Assert.AreEqual(1, ghost.GetComponentsInChildren<MonoBehaviour>().Length);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RecorderUsesRaceClockPausesAndCapturesRecoveryCuts()
        {
            yield return BootFrozenPlayer();
            var recorder = flow.GetComponent<GhostRecorder>();
            double start = RaceClock.Now;
            recorder.Begin(player, 123, "Clock rider");
            Assert.AreEqual(0, recorder.Record.Samples[0].TimeMilliseconds, "Capture GO rather than the first later sample tick");
            for (int tick = 0; tick < 10; tick++) yield return new WaitForFixedUpdate();
            // Accumulate clock intervals without physics steps. captureDeltaTime accelerates
            // physics in this fixture, but RaceClock deliberately follows unscaled wall time.
            for (int interval = 0; interval < 5; interval++) RaceClock.Advance(.2f);
            for (int tick = 0; tick < 10; tick++) yield return new WaitForFixedUpdate();
            Assert.GreaterOrEqual(recorder.Record.Samples[recorder.Record.Samples.Count - 1].TimeSeconds, .999f,
                "A fixed-tick-only timeline would lose the clock interval");
            PauseController.Set(PauseReason.User, true);
            int count = recorder.Record.Samples.Count;
            double paused = RaceClock.Now;
            for (int frame = 0; frame < 10; frame++) yield return null;
            Assert.AreEqual(count, recorder.Record.Samples.Count);
            Assert.AreEqual(paused, RaceClock.Now);
            PauseController.Set(PauseReason.User, false);
            yield return null; yield return null;
            Vector3 before = player.transform.position;
            player.Car.Teleport(before + Vector3.right * 100f, Quaternion.Euler(0, 90, 180));
            var cut = recorder.Record.Samples[recorder.Record.Samples.Count - 1];
            Assert.AreNotEqual(0, cut.Flags & GhostRecord.TeleportFlag);
            recorder.Record.EvaluatePose(cut.TimeSeconds - .001f, out var previous, out _);
            Assert.Less(Vector3.Distance(previous, before), .01f, "Do not glide toward a recovery position before its timestamp");
            recorder.Record.EvaluatePose(cut.TimeSeconds, out var after, out var rotation);
            Assert.Less(Vector3.Distance(after, player.transform.position), .01f);
            Assert.Less(Quaternion.Angle(rotation, player.transform.rotation), .05f);
            var record = recorder.Stop();
            Assert.AreEqual(RaceClock.Now - start, record.Duration, .002d, "The final partial sampling interval is retained");
            Assert.NotNull(GhostRecord.FromBytes(record.ToBytes()));
            int finalCount = record.Samples.Count;
            player.Car.Teleport(before, Quaternion.identity);
            Assert.AreEqual(finalCount, record.Samples.Count, "Stopped recorders detach their recovery listener");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator ReachingTheSampleLimitDiscardsTheIncompleteReplay()
        {
            yield return BootFrozenPlayer();
            var recorder = flow.GetComponent<GhostRecorder>();
            recorder.Begin(player, 123, "Long run");
            // Fill the bounded buffer directly; this checks overflow handling, not a 33-minute play session.
            for (int i = 1; i < GhostRecord.MaxSamples; i++)
                recorder.Record.AddPose(player.transform.position, player.transform.rotation, i * .1d, 0f, 0);
            Assert.IsNull(recorder.Stop(), "A partial replay cannot masquerade as a shorter personal best");
            Assert.IsFalse(recorder.Recording);
        }
    }
}
