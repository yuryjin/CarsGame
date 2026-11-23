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
        public IEnumerator RuinedSkylineAndBomberFormationAppearInTheMatch()
        {
            yield return Boot();
            var arena = race.spawner.Arena.transform;
            var ruins = arena.Find("RuinedWorks");
            var formation = arena.Find("BomberFormation");
            Assert.IsNotNull(ruins);
            Assert.IsNotNull(formation);
            Assert.AreEqual(2, formation.childCount);
            Assert.IsEmpty(ruins.GetComponentsInChildren<Collider>());
            Assert.IsEmpty(formation.GetComponentsInChildren<Collider>());

            var buildings = ruins.GetComponentsInChildren<MeshRenderer>();
            var planes = formation.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(1, buildings.Length, "the static skyline is a single mesh");
            Assert.AreEqual(2, planes.Length);
            Assert.AreSame(planes[0].GetComponent<MeshFilter>().sharedMesh, planes[1].GetComponent<MeshFilter>().sharedMesh);
            foreach (var renderer in new[] { buildings[0], planes[0], planes[1] })
            {
                Assert.AreEqual("CarsGame/StylizedLit", renderer.sharedMaterial.shader.name);
                Assert.IsTrue(renderer.GetComponent<MeshFilter>().sharedMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color));
            }
            Assert.Greater(buildings[0].bounds.max.y - arena.position.y, ArenaLayout.BridgeHeight);
            var bridgehead = Resources.Load<GameObject>("Art/Arena/RuinedBridgehead").GetComponentInChildren<MeshFilter>().sharedMesh;
            int ruinsWithBlockers = 0;
            foreach (var collider in arena.GetComponentsInChildren<BoxCollider>())
            {
                var model = collider.GetComponentInChildren<MeshFilter>();
                if (model == null || model.sharedMesh != bridgehead) continue;
                Assert.AreEqual(LayerMask.NameToLayer("Track"), collider.gameObject.layer);
                Assert.AreEqual(new Vector3(17f, 12f, 24f), collider.size);
                Assert.That(collider.bounds.max.y - arena.position.y, Is.EqualTo(12f).Within(.01f));
                ruinsWithBlockers++;
            }
            Assert.AreEqual(3, ruinsWithBlockers);
            var lead = formation.GetChild(0);
            var start = lead.position;
            var frustum = GeometryUtility.CalculateFrustumPlanes(Camera.main);
            foreach (var plane in planes) Assert.IsTrue(GeometryUtility.TestPlanesAABB(frustum, plane.bounds), "visible from the grid");
            CaptureView(Camera.main, "arena-grid");
            for (int i = 0; i < 12; i++) yield return null;
            Assert.Greater(Vector3.Distance(start, lead.position), 1f);
            PauseController.Set(PauseReason.User, true);
            yield return null;
            var paused = lead.position;
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(paused, lead.position);
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
            var go = new GameObject("ArenaOverview", typeof(Camera));
            var camera = go.GetComponent<Camera>();
            try
            {
                camera.transform.position = arena.transform.position + new Vector3(150f, 170f, 150f);
                camera.transform.LookAt(arena.transform.position);
                camera.farClipPlane = 600f;
                CaptureView(camera, "arena-overview");
                var plane = arena.transform.Find("BomberFormation").GetChild(0);
                camera.transform.position = plane.TransformPoint(new Vector3(-18f, 8f, 23f));
                camera.transform.LookAt(plane.position);
                CaptureView(camera, "arena-bomber");
            }
            finally { Object.Destroy(go); }
        }

        private static void CaptureView(Camera camera, string filename)
        {
            string work = System.Environment.GetEnvironmentVariable("CG_WORK");
            if (string.IsNullOrEmpty(work) || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(work, filename + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = previousTarget;
                target.Release();
                Object.Destroy(target); Object.Destroy(pixels);
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

        [UnityTest, Timeout(900000)]
        public IEnumerator ScoredMatchesKeepTheStrongerRecordAndSeparateDamageAcrossLives()
        {
            // Pin the field so only the specified hits affect the scored comparison.
            int first = 0;
            for (int match = 0; match < 2; match++)
            {
                if (match == 0) yield return Boot();
                else
                {
                    flow.EnterMenu();
                    flow.StartRace(ev, null);
                    while (race.State == RaceState.Countdown) yield return null;
                }
                var save = PlatformGlue.Save;
                if (match == 0)
                {
                    save.Data.progress.bests.RemoveAll(b => b?.eventId == "a1");
                    save.Data.progress.bests.Add(new Core.Save.EventBest { eventId = "a1", bestTimeMs = 180000 });
                }
                Assert.AreEqual(180f, race.MatchDuration);
                var player = race.Player;
                var foe = race.Participants[0] != player ? race.Participants[0] : race.Participants[1];
                var ally = race.Participants[2] != player && race.Participants[2] != foe ? race.Participants[2] : race.Participants[3];
                foreach (var p in race.Participants)
                {
                    var input = p.gameObject.AddComponent<ScriptedDriverInput>();
                    input.Set(0, 0, true);
                    p.Car.SetInputSource(input);
                    var gun = p.GetComponent<AiGunner>(); if (gun != null) gun.enabled = false;
                    p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                }
                Time.captureDeltaTime = .2f;
                if (match == 0)
                {
                    while (foe.Health.IsInvulnerable) yield return null;
                    var hit = new Core.Combat.HitInfo(player.gameObject, 10, Core.Combat.HitType.Projectile);
                    Assert.Greater(foe.Health.ApplyHit(hit).DamageDealt, 0f);
                    Assert.IsTrue(foe.Health.ApplyHit(new Core.Combat.HitInfo(ally.gameObject, 200, Core.Combat.HitType.Projectile)).Fatal);
                    Assert.AreEqual(1, player.Score.Assists); Assert.AreEqual(1, ally.Kills); Assert.AreEqual(0, player.Kills);
                    while (foe.Health.IsDead || foe.Health.IsInvulnerable) yield return null;
                    Assert.IsTrue(foe.Health.ApplyHit(new Core.Combat.HitInfo(ally.gameObject, 200, Core.Combat.HitType.Projectile)).Fatal);
                    Assert.AreEqual(1, player.Score.Assists); Assert.AreEqual(2, ally.Kills);
                    int tick = SimulationDriver.Instance.Tick;
                    int points = player.Score.Points.Total;
                    PauseController.Set(PauseReason.User, true);
                    for (int i = 0; i < 5; i++) yield return null;
                    Assert.AreEqual(tick, SimulationDriver.Instance.Tick); Assert.AreEqual(points, player.Score.Points.Total);
                    PauseController.Set(PauseReason.User, false);
                }
                while (race.RemainingTime > 15d) yield return null;
                var hud = GameObject.Find("Goals")?.GetComponent<TMPro.TMP_Text>();
                Assert.NotNull(hud);
                Assert.IsNotEmpty(hud.text);
                if (match == 0) StringAssert.Contains(HudScreen.GetLocalizedRacerName(ally.displayName), hud.text);
                else StringAssert.Contains(Loc.Get("arena.tied"), hud.text);
                while (race.State == RaceState.Racing) yield return null;
                yield return null;
                var result = race.Results.Player;
                Assert.AreEqual(200, result.ArenaPoints.Survival);
                Assert.AreEqual(result.ArenaPoints.Total, player.Score.Points.Total);
                var best = save.Data.progress.bests.Find(b => b.eventId == "a1");
                Assert.IsTrue(best.hasArenaScore);
                if (match == 0)
                {
                    first = result.ArenaPoints.Total;
                    Assert.AreEqual(1, result.Assists); Assert.AreEqual(20, result.ArenaPoints.Knockouts);
                    Assert.AreEqual(first, best.arenaPoints); Assert.AreEqual(2, result.Position);
                }
                else
                {
                    Assert.Less(result.ArenaPoints.Total, first);
                    Assert.AreEqual(1, result.Position); Assert.AreEqual(first, best.arenaPoints);
                }
                Debug.Log($"Arena match {match + 1}: {race.Results.RaceDuration:F2}s, score {result.ArenaPoints.Total}, place {result.Position}, record {best.arenaPoints}");
            }
        }
    }
}
