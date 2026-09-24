using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Combat;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class HotBombPlayTests
    {
        private float savedCapture;
        private GameFlow flow;
        private RaceManager race;
        private RaceEventDefinition ev;
        [SetUp] public void Setup() { savedCapture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            PauseController.ResetAll();
            Time.captureDeltaTime = savedCapture;
            if (ev != null) Object.Destroy(ev);
        }

        private IEnumerator Boot(int rivals)
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            race = Object.FindAnyObjectByType<RaceManager>();
            ev = Object.Instantiate(Resources.Load<RaceEventDefinition>("Events/Event_e18"));
            ev.aiCountOverride = rivals;
            Assert.IsTrue(flow.IsUnlocked(ev), "the new event is immediately playable");
            DifficultyTier normal = null;
            foreach (var tier in flow.tiers) if (tier != null && tier.tierId == "normal") normal = tier;
            RaceDirector.Instance.Reseed(74321);
            flow.StartRace(ev, normal);
            Assert.AreEqual(RaceState.Countdown, race.State);
            Assert.IsNotNull(race.Player.HotBomb);
        }

        private static IEnumerator Ticks(int count) { for (int i = 0; i < count; i++) yield return null; }
        private static void Put(RaceParticipant p, float distance, float lane)
        {
            TrackPath.Instance.Pose(distance, lane, out var pos, out var rot);
            p.Car.Teleport(pos + Vector3.up * 0.1f, rot);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator ContactPauseExplosionRespawnRetryAndExit()
        {
            yield return Boot(2);
            var bomb = race.Player.HotBomb;
            yield return Ticks(25);
            Assert.IsNull(bomb.Carrier, "no bomb explodes during countdown");
            while (race.State == RaceState.Countdown) yield return null;
            yield return Ticks(2);
            var player = race.Player;
            RaceParticipant rival = null;
            int slot = 0;
            foreach (var p in race.Participants)
            {
                Assert.IsTrue(p.Car.Cannon.locked);
                Assert.AreEqual(SecondaryWeaponType.None, p.Weapons.secondaryType);
                p.Car.inputEnabled = false;
                p.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                Put(p, 50f + slot++ * 100f, 0f);
                if (!p.isPlayer && rival == null) rival = p;
            }
            Assert.AreEqual(player, bomb.Carrier);
            Assert.AreEqual(0, Object.FindObjectsByType<Pickup>().Length);
            yield return Ticks(80);
            Assert.Less(bomb.SecondsLeft, HotBombRace.FuseSeconds, "fuse is running");
            Assert.IsFalse(Object.FindAnyObjectByType<RaceOverlays>().BombMessage.Contains("[bomb."), "localized HUD");

            // Actual Rigidbody contact, not just a direct call into the passing rule.
            Put(player, 50f, -1.4f);
            Put(rival, 50f, 1.4f);
            player.Car.Body.constraints = rival.Car.Body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            player.Car.Body.linearVelocity = TrackPath.Instance.Right(50f) * 4f;
            float fuseBefore = bomb.SecondsLeft;
            int contactTicks = 0;
            while (bomb.Carrier == player && contactTicks++ < 60) yield return null;
            Assert.AreEqual(rival, bomb.Carrier, "touch passes the bomb");
            Assert.AreEqual(1, bomb.Passes, "one collision cannot bounce it back");
            Assert.Less(bomb.SecondsLeft, fuseBefore, "passing never resets the fuse");
            Assert.IsFalse(bomb.Touch(rival, player), "new carrier gets a short no-pass window");
            player.Car.Body.constraints = rival.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
            Put(player, 50f, 0f); Put(rival, 200f, 0f);
            int tick = SimulationDriver.Instance.Tick;
            float left = bomb.SecondsLeft;
            PauseController.Set(PauseReason.User, true);
            yield return Ticks(20);
            Assert.AreEqual(tick, SimulationDriver.Instance.Tick);
            Assert.AreEqual(left, bomb.SecondsLeft);
            Assert.IsFalse(bomb.Touch(rival, player));
            PauseController.Set(PauseReason.User, false);

            rival.Car.ResetCar();
            Assert.AreEqual(rival, bomb.Carrier, "resetting the car cannot discard the bomb");
            rival.Status.ApplyStatus(StatusKind.Shield, 100f);
            rival.Health.SetInvulnerable(100f);
            int kills = player.Kills;
            for (int i = 0; i < 1000 && bomb.Explosions == 0; i++) yield return null;
            Assert.AreEqual(1, bomb.Explosions);
            Assert.IsTrue(rival.Health.IsDead, "the bomb detonates through shields and reset immunity");
            Assert.AreEqual(1, rival.Deaths);
            Assert.AreEqual(kills + 1, player.Kills, "last passer gets the kill");
            rival.Health.Detonate(player.gameObject);
            Assert.AreEqual(1, rival.Deaths, "a wreck cannot explode twice");
            Assert.IsNull(bomb.Carrier);
            yield return Ticks(160);
            Assert.IsFalse(rival.Health.IsDead);
            Assert.IsNotNull(bomb.Carrier, "a fresh bomb starts after the explosion");
            Assert.IsTrue(rival.Car.Cannon.locked, "respawn does not re-enable guns");

            // The holder tries to meet the pack; non-holders move away from its lane.
            var holder = bomb.Carrier;
            RaceParticipant chaser = null;
            foreach (var p in race.Participants) if (p != holder) { chaser = p; break; }
            foreach (var p in race.Participants) Put(p, 500f, 0f);
            Put(holder, 130f, 1f); Put(chaser, 120f, -1f);
            Assert.IsTrue(bomb.TryGetTactic(holder, 1.5f, out float lane, out float limit));
            Assert.That(lane, Is.EqualTo(-1f).Within(0.15f));
            Assert.Less(limit, 30f, "holder slows to meet a car behind");
            Assert.IsTrue(bomb.TryGetTactic(chaser, 1.5f, out lane, out _));
            Assert.Less(lane, 0f, "other car avoids the holder's lane");

            var path = TrackPath.Instance;
            float distance = path.startLineDistance + 1f;
            holder.transform.position = path.Position(distance);
            holder.Progress.BeginRace();
            int lapSteps = Mathf.CeilToInt(path.Length / 4f);
            for (int i = 0; i < lapSteps * ev.laps; i++)
            {
                distance += path.Length / lapSteps;
                holder.transform.position = path.Position(distance);
                holder.Progress.SimulationStep(i);
            }
            Assert.IsTrue(race.HasFinished(holder));
            Assert.IsFalse(bomb.Touch(holder, chaser), "finished cars cannot pass bombs");
            yield return Ticks(2);
            Assert.AreNotEqual(holder, bomb.Carrier, "finished carriers leave the bomb rotation");

            race.QaFinishNow();
            Assert.AreEqual(RaceState.Finished, race.State);
            Assert.IsNull(player.HotBomb);
            int explosions = bomb.Explosions;
            yield return Ticks(60);
            Assert.AreEqual(explosions, bomb.Explosions, "results screen cannot explode cars");
            flow.RetryRace();
            Assert.AreNotEqual(bomb, race.Player.HotBomb);
            Assert.AreEqual(0, race.Player.HotBomb.Passes);
            Assert.IsNull(race.Player.HotBomb.Carrier);
            flow.EnterMenu();
            yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<HotBombRace>());
            RaceEventDefinition normal = null;
            foreach (var e in flow.events) if (e.eventId == "e1") normal = e;
            flow.StartRace(normal, null);
            Assert.IsNull(race.Player.HotBomb);
            Assert.IsFalse(race.Player.Car.Cannon.locked);
            Assert.Greater(race.Player.Health.damagePerImpactSpeed, 0f);
            flow.EnterMenu();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator BotsPassBombsAndCompleteTheEvent()
        {
            yield return Boot(5);
            var bomb = race.Player.HotBomb;
            Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
            while (race.State == RaceState.Countdown) yield return null;
            int ticks = 0;
            while (race.State != RaceState.Finished && ticks++ < 20000) yield return null;
            Debug.Log($"[HotBomb] ticks={ticks} passes={bomb.Passes} explosions={bomb.Explosions} state={race.State}");
            Assert.AreEqual(RaceState.Finished, race.State);
            Assert.Greater(bomb.Passes, 0, "bots deliberately make contact");
            Assert.Greater(bomb.Explosions, 0);
            Assert.AreEqual(0, RaceDirector.Instance.ShotsFired);
            Assert.AreEqual(0, RaceDirector.Instance.MinesDropped);
            Assert.IsNotNull(race.Results);
            Assert.IsTrue(race.Results.Player.Finished);
            flow.EnterMenu();
        }
    }
}
