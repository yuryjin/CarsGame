using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    /// <summary>Minefields and remote charges on the real scene: the Minefield event, an arena match, a player charge.</summary>
    public sealed class MinesPlayTests
    {
        private float savedCaptureDelta;
        [SetUp] public void Save() { savedCaptureDelta = Time.captureDeltaTime; }
        [TearDown] public void Restore() { Time.captureDeltaTime = savedCaptureDelta; LogAssert.ignoreFailingMessages = false; }

        private GameFlow flow; private RaceManager race; private RaceDirector director;
        private int fieldHits, carMineHits;

        private IEnumerator Boot()
        {
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float wait = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < wait) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;   // GameFlow.Start (EnterMenu) must run before a race starts
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            director = RaceDirector.Instance;
            fieldHits = carMineHits = 0;
            director.MineHit += (owner, victim) => { if (owner == null) fieldHits++; else carMineHits++; };
        }

        private IEnumerator Start(RaceEventDefinition ev, bool autopilot)
        {
            var save = PlatformGlue.Save;
            if (!save.Data.progress.unlockedEvents.Contains(ev.eventId)) save.Data.progress.unlockedEvents.Add(ev.eventId);
            flow.StartRace(ev, null);
            if (autopilot) Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
            Assert.AreEqual(RaceState.Countdown, race.State, "race started");
            while (race.State == RaceState.Countdown) yield return null;
        }

        private static IEnumerator Ticks(int n) { for (int i = 0; i < n; i++) yield return null; }

        private static bool OnGround(MineHazard mine)
        {
            int layer = LayerMask.NameToLayer("Track");
            return Physics.Raycast(mine.transform.position + Vector3.up, Vector3.down, out var hit, 3f, 1 << layer, QueryTriggerInteraction.Ignore)
                && Mathf.Abs(hit.point.y - mine.transform.position.y) < 0.2f;
        }

        private string Deaths()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var p in race.Participants) sb.Append($" {p.name}:D{p.Deaths}/laps{(p.Progress != null ? p.Progress.LapsCompleted : 0)}");
            return sb.ToString();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator MinefieldEventLaysSlalomGatesThatTheFieldGetsThrough()
        {
            yield return Boot();
            RaceEventDefinition e6 = null;
            foreach (var e in flow.events) if (e != null && e.eventId == "e6") e6 = e;
            Assert.IsNotNull(e6);
            var ev = Object.Instantiate(e6); ev.aiCountOverride = 3; ev.laps = 1;
            yield return Start(ev, true);

            Assert.AreEqual(12, director.Field.Count, "two slalom gates of six mines");
            int onRoad = 0;
            foreach (var m in director.LiveMineHazards) if (m.IsField && OnGround(m)) onRoad++;
            Assert.AreEqual(12, onRoad, "every field mine lies on the road surface");

            float until = Time.realtimeSinceStartup + 600f;
            int ticks = 0;
            while (race.State != RaceState.Finished && ticks < 50 * 240 && Time.realtimeSinceStartup < until) { ticks++; yield return null; }
            Debug.Log($"[MinesPlayTests] minefield e6: {ticks / 50f:F0}s state={race.State} fieldHits={fieldHits} carMineHits={carMineHits} dropped={director.MinesDropped} fieldLive={director.Field.LiveCount}/{director.Field.Count} deaths:{Deaths()}");
            int finished = 0;
            foreach (var p in race.Participants) if (race.HasFinished(p)) finished++;
            Assert.Greater(finished, 0, "the minefield slows the field down but does not stop it");
            Assert.AreEqual(12, director.Field.Count, "the field lasts the whole race");
            flow.EnterMenu();
            Assert.AreEqual(0, director.Field.Count, "and is gone after it");
            Assert.AreEqual(0, director.LiveMines);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator ArenaLaysAFieldArmsEveryoneAndThePlayerChargeWorks()
        {
            yield return Boot();
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            ev.eventId = "arena_mines_test"; ev.nameKey = "events.e1.name"; ev.mode = EventMode.Arena;
            ev.matchDuration = 120f; ev.aiCountOverride = 3;
            yield return Start(ev, false);

            Assert.IsTrue(race.IsArenaMode);
            Assert.AreEqual(RaceEventDefinition.ArenaMineFieldDefault, director.Field.Count);
            var arena = race.spawner.Arena;
            foreach (var m in director.LiveMineHazards)
            {
                if (!m.IsField) continue;
                float r = Vector3.Distance(new Vector3(m.transform.position.x, 0f, m.transform.position.z), arena.transform.position);
                Assert.That(r, Is.InRange(ArenaLayout.MineInnerRadius - 0.1f, ArenaLayout.MineOuterRadius + 0.1f));
                Assert.IsTrue(OnGround(m), "arena mine on the floor");
            }
            foreach (var p in race.Participants)
                Assert.AreNotEqual(SecondaryWeaponType.None, p.Weapons.secondaryType, p.name + " has mines or charges in the arena");

            int planted = 0;
            for (int i = 0; i < 50 * 40; i++)
            {
                foreach (var p in race.Participants) if (!p.isPlayer && p.Weapons.PlantedCharge != null && p.Weapons.PlantedCharge.SpawnTick == SimulationDriver.Instance.Tick - 1) planted++;
                yield return null;
            }
            Assert.Greater(planted, 0, "arena bots plant charges");
            int charges = 0;
            foreach (var m in director.LiveMineHazards) if (m.Kind == MineKind.Charge) charges++;
            Debug.Log($"[MinesPlayTests] arena 40s: bot charges planted={planted} on the ground={charges} fieldHits={fieldHits} carMineHits={carMineHits} dropped={director.MinesDropped} fieldLive={director.Field.LiveCount}/{director.Field.Count} deaths:{Deaths()}");

            // The player's charge: plant, wait for it to arm, put a rival on it, press again.
            // The autonomous battle above is complete. Stop its input before testing a
            // particular blast, so unrelated gunfire cannot kill the target while it arms.
            foreach (var p in race.Participants)
            {
                p.Health.ReviveNow();
                p.Car.inputEnabled = false;
            }
            var player = race.Player;
            RaceParticipant rival = null;
            foreach (var p in race.Participants) if (!p.isPlayer && !p.Health.IsDead) { rival = p; break; }
            Assert.IsNotNull(rival);
            var w = player.Weapons;
            w.secondaryType = SecondaryWeaponType.RemoteCharge; w.secondaryAmmoMax = 2; w.RefillSecondaryAmmo();
            Vector3 spot = arena.transform.TransformPoint(new Vector3(0f, 0.5f, -44f));   // along the south wall, outside the field
            player.Car.Teleport(spot, arena.transform.rotation);
            yield return Ticks(2);
            int tick = SimulationDriver.Instance.Tick;
            Assert.IsTrue(w.TryFireSecondary(tick), "first press plants");
            var charge = w.PlantedCharge;
            Assert.IsNotNull(charge);
            player.Car.Teleport(spot + arena.transform.right * 30f, arena.transform.rotation);
            yield return Ticks(40);
            rival.Health.ReviveNow();
            rival.Car.Teleport(charge.transform.position + arena.transform.right * 2f + Vector3.up * 0.5f, arena.transform.rotation);
            rival.Car.inputEnabled = false;
            yield return Ticks(5);
            while (rival.Health.IsInvulnerable) yield return null;
            float before = rival.Health.Health;
            Assert.Greater(before, 0f, "The controlled blast needs a live target.");
            Assert.IsTrue(w.TryFireSecondary(SimulationDriver.Instance.Tick), "second press detonates");
            Assert.Less(rival.Health.Health, before, "the rival on the charge takes the blast");
            Assert.IsNull(w.PlantedCharge);
            Debug.Log($"[MinesPlayTests] player charge: rival {before:F0} -> {rival.Health.Health:F0}");
            flow.EnterMenu();
            Object.Destroy(ev);
        }
    }
}
