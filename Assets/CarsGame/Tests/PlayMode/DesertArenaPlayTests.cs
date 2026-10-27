using System.Collections;
using NUnit.Framework;
using RacingGame.Core.Sim;
using RacingGame.Core.Platform;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class DesertArenaPlayTests
    {
        private GameFlow flow;
        private RaceManager race;
        private float capture;
        private RaceEventDefinition ev;

        private sealed class DriveAndBomb : ICarInputSource
        {
            public DriveCommand Sample(int tick) => new DriveCommand { Tick = tick, Throttle = 1f, Fire = true };
        }

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
            if (flow != null) flow.EnterMenu();
            if (ev != null) Object.Destroy(ev);
        }

        private IEnumerator Boot()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; // Let scene Start callbacks finish before starting the next match.
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            ev = Object.Instantiate(Resources.Load<RaceEventDefinition>("Events/Event_a1"));
            Assert.IsNotNull(ev);
            Assert.IsTrue(flow.IsUnlocked(ev));
            flow.StartRace(ev, null);
            Assert.AreEqual(RaceState.Countdown, race.State);
            Assert.AreEqual(0, race.spawner.Arena.LiveBombs);
            while (race.State == RaceState.Countdown) yield return null;
            Assert.IsTrue(race.spawner.Arena.Running);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator BridgeJumpBombPauseDamageAndRetry()
        {
            yield return Boot();
            RaceDirector.Instance.Field.Clear();
            var arena = race.spawner.Arena;
            var player = race.Player;
            var bomb = player.GetComponent<ArenaBombDrop>();
            foreach (var p in race.Participants)
            {
                p.Car.inputEnabled = false;
                p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
            }
            Assert.IsFalse(bomb.Step(true, SimulationDriver.Instance.Tick), "ground fire stays a gun");
            player.Car.Teleport(arena.transform.position + new Vector3(0f, 0.5f, 90f), Quaternion.Euler(0f, 180f, 0f));
            player.Car.Body.constraints = RigidbodyConstraints.None;
            player.Car.inputEnabled = true;
            player.Car.SetInputSource(new DriveAndBomb());
            // Normal driving input, from rest: no scripted launch or teleport into the drop window.
            int until = SimulationDriver.Instance.Tick + 1000;
            while (bomb.Drops == 0 && SimulationDriver.Instance.Tick < until) yield return null;
            Assert.AreEqual(1, bomb.Drops, "normal throttle must climb the bridge and drop during the jump; position=" + player.transform.position);
            Assert.AreEqual(1, arena.LiveBombs);
            Assert.IsNotNull(MissionArt.Glow, "arena strike markers need their shared material before the first mission");
            foreach (var renderer in arena.GetComponentsInChildren<MeshRenderer>())
                Assert.IsNotNull(renderer.sharedMaterial, renderer.name + " must not render magenta");
            CaptureArena(arena);
            Assert.IsFalse(player.Car.IsGrounded);
            var point = arena.BombLanding(player.Car);
            var victim = race.Participants[0] == player ? race.Participants[1] : race.Participants[0];
            victim.Car.Teleport(point + Vector3.up * 0.5f, Quaternion.identity);
            victim.Health.TakeDamage(Mathf.Max(0f, victim.Health.Health - 30f));
            float hp = victim.Health.Health;
            int kills = player.Kills;
            // Pause must stop the impact, not just its animation.
            int tick = SimulationDriver.Instance.Tick;
            PauseController.Set(PauseReason.User, true);
            for (int i = 0; i < 130; i++) yield return null;
            Assert.AreEqual(tick, SimulationDriver.Instance.Tick);
            Assert.AreEqual(hp, victim.Health.Health);
            Assert.AreEqual(1, arena.LiveBombs);
            PauseController.Set(PauseReason.User, false);
            until = SimulationDriver.Instance.Tick + 130;
            while (SimulationDriver.Instance.Tick < until) yield return null;
            Assert.AreEqual(0, arena.LiveBombs);
            Assert.AreEqual(1, bomb.Drops, "holding fire cannot repeat a drop in the same jump");
            Assert.Greater(player.Kills, kills, "bomb kill is credited through normal combat rules");
            Assert.IsTrue(victim.Health.IsDead);
            Assert.Less(player.transform.position.z - arena.transform.position.z, -ArenaLayout.BridgeEnd, "jump reaches the other side");
            Assert.IsFalse(player.Health.IsDead);
            race.QaFinishNow();
            yield return null;
            Assert.AreEqual(0, arena.LiveBombs);
            flow.RetryRace();
            Assert.AreNotEqual(arena, race.spawner.Arena);
            Assert.AreEqual(0, race.Player.GetComponent<ArenaBombDrop>().Drops);
            flow.EnterMenu();
            yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<ArenaLayout>());
            Assert.IsNull(Object.FindAnyObjectByType<ArenaBombDrop>());
        }

        private static void CaptureArena(ArenaLayout arena)
        {
            string work = System.Environment.GetEnvironmentVariable("CG_WORK");
            if (string.IsNullOrEmpty(work) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var go = new GameObject("ArenaOverview", typeof(Camera));
            var camera = go.GetComponent<Camera>();
            camera.transform.position = arena.transform.position + new Vector3(150f, 170f, 150f);
            camera.transform.LookAt(arena.transform.position);
            camera.farClipPlane = 600f;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(work, "arena-overview.png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target); Object.Destroy(pixels); Object.Destroy(go);
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator BotsUseBridgesAndMatchEndsByTimer()
        {
            yield return Boot();
            ev.matchDuration = 65f;
            int drops = 0;
            float until = Time.realtimeSinceStartup + 180f;
            while (race.State == RaceState.Racing && Time.realtimeSinceStartup < until)
            {
                drops = 0;
                foreach (var p in race.Participants)
                    if (!p.isPlayer) drops += p.GetComponent<ArenaBombDrop>().Drops;
                yield return null;
            }
            Assert.AreEqual(RaceState.Finished, race.State);
            Assert.Greater(drops, 0, "bots must reach a bridge and drop a bomb without test steering");
            Assert.IsTrue(race.Results.IsArena);
            Assert.AreEqual(0, race.Results.LapsRequired);
            Assert.IsTrue(race.Results.Player.Finished);
        }
    }
}
