using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Daily;
using RacingGame.Core.Sim;
using RacingGame.Platform.Mock;

namespace RacingGame.Tests
{
    /// <summary>Race rule twists and the daily race on the real scene, with the QA autopilot driving the player.</summary>
    public sealed class TwistPlayTests
    {
        private float savedCaptureDelta;
        [SetUp] public void Save() { savedCaptureDelta = Time.captureDeltaTime; }
        [TearDown] public void Restore() { Time.captureDeltaTime = savedCaptureDelta; LogAssert.ignoreFailingMessages = false; }

        private GameFlow flow; private RaceManager race;

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
        }

        private RaceEventDefinition Event(string id)
        {
            foreach (var e in flow.events) if (e != null && e.eventId == id) return e;
            Assert.Fail("no event " + id); return null;
        }

        private IEnumerator Race(RaceEventDefinition ev, int ai, int laps)
        {
            ev = Object.Instantiate(ev); ev.aiCountOverride = ai; ev.laps = laps;
            var save = PlatformGlue.Save;
            if (!save.Data.progress.unlockedEvents.Contains(ev.eventId)) save.Data.progress.unlockedEvents.Add(ev.eventId);
            flow.StartRace(ev, null);
            Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
            Assert.AreEqual(RaceState.Countdown, race.State, "race started");
            Assert.AreEqual(ai + 1, race.Participants.Count, "field spawned");
            while (race.State == RaceState.Countdown) yield return null;
        }

        private static IEnumerator Ticks(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest, Timeout(600000)]
        public IEnumerator FifteenEventsAndMinesOnlyLocksEveryCannon()
        {
            yield return Boot();
            Assert.GreaterOrEqual(flow.events.Length, 15, "e6–e15 come from Resources/Events");
            yield return Race(Event("e6"), 3, 1);
            Assert.AreEqual(RaceTwist.MinesOnly, flow.CurrentEvent.twist);
            foreach (var p in race.Participants)
            {
                Assert.IsTrue(p.Car.Cannon.locked, p.name + " cannon locked");
                Assert.AreEqual(SecondaryWeaponType.Mines, p.Weapons.secondaryType, p.name);
            }
            yield return Ticks(50 * 40);
            var director = RaceDirector.Instance;
            Debug.Log($"[TwistPlayTests] mines-only: shells={director.ShotsFired} mines={director.MinesDropped} mineHits={director.MineHits}");
            Assert.AreEqual(0, director.ShotsFired, "no cannon fire in a mines-only race");
            flow.EnterMenu();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator OneLifeKeepsWrecksOutAndContinueRevivesOnce()
        {
            yield return Boot();
            EditorMockPlatform.AdFillRate = 1f; EditorMockPlatform.RewardedGrantRate = 1f; EditorMockPlatform.AdDuration = 0.2f;
            yield return Race(Event("e9"), 3, 3);
            Debug.Log($"[TwistPlayTests] one-life start: race={race.State} flow={flow.State} field={race.Participants.Count} event={(flow.CurrentEvent != null ? flow.CurrentEvent.eventId : "-")}");
            yield return Ticks(50 * 4);   // past the start invulnerability
            Assert.Greater(race.Participants.Count, 1, "field spawned");
            RaceParticipant rival = null;
            foreach (var p in race.Participants) if (!p.isPlayer) { rival = p; break; }
            Assert.IsFalse(rival.Health.respawnOnDeath);
            rival.Health.ApplyHit(new Core.Combat.HitInfo(null, 10000f));
            Assert.IsTrue(rival.Health.IsDead);
            yield return Ticks(50 * 6);
            Assert.IsTrue(rival.Health.IsDead, "a one-life wreck stays out");
            Assert.IsTrue(RaceManager.IsOut(rival));

            var player = race.Player;
            player.Health.ApplyHit(new Core.Combat.HitInfo(null, 10000f));
            yield return null;
            Assert.IsTrue(flow.ContinueOpen, "the continue prompt opens");
            var overlays = Object.FindAnyObjectByType<RaceOverlays>();
            Assert.IsTrue(overlays.PromptOpen);
            FindButton(overlays, "Watch").onClick.Invoke();
            float until = Time.realtimeSinceStartup + 10f;
            while (player.Health.IsDead && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsFalse(player.Health.IsDead, "a watched ad revives the player");
            Assert.IsFalse(flow.ContinueOpen);

            yield return Ticks(50 * 3);   // past the respawn shield
            player.Health.ApplyHit(new Core.Combat.HitInfo(null, 10000f));
            yield return null;
            Assert.IsFalse(flow.ContinueOpen, "only one continue per race");
            until = Time.realtimeSinceStartup + 10f;
            while (race.State != RaceState.Finished && Time.realtimeSinceStartup < until) yield return null;
            Assert.AreEqual(RaceState.Finished, race.State, "the race ends when the player is out");
            Assert.IsFalse(race.Results.Player.Finished);
            Debug.Log($"[TwistPlayTests] one-life: player pos={race.Results.Player.Position} deaths={race.Results.Player.Deaths}");
            flow.EnterMenu();
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator DailyRacePaysTheBonusOncePerDay()
        {
            yield return Boot();
            var save = PlatformGlue.Save;
            save.Data.progress.daily = new Core.Save.DailyData();
            var daily = flow.DailyEvent;
            Assert.IsTrue(DailyRace.IsDaily(daily));
            Assert.AreEqual(DailyRace.TodaySpec.Twist, daily.twist);
            flow.StartRace(daily, null);
            Assert.AreEqual(RaceState.Countdown, race.State, "daily race started");
            Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
            while (race.State == RaceState.Countdown) yield return null;
            yield return Ticks(50 * 5);
            race.QaFinishNow();
            yield return null;
            Assert.AreEqual(FlowState.Results, flow.State);
            Debug.Log($"[TwistPlayTests] daily d{DailyRace.Today} twist={daily.twist}: bonus={flow.LastDailyBonus} streak={flow.LastDailyStreak}");
            Assert.AreEqual(DailyChallenge.FirstFinishReward(1), flow.LastDailyBonus);
            Assert.AreEqual(1, flow.LastDailyStreak);
            Assert.IsTrue(save.Data.progress.daily.Claimed(DailyRace.Today));
            long coinsAfterFirst = save.Data.Coins;
            Assert.IsTrue(save.Data.progress.bests.Exists(b => b.eventId == daily.eventId && b.bestTimeMs > 0));

            flow.RetryRace();
            Object.FindAnyObjectByType<DebugBus>().Execute("autopilot", "on");
            while (race.State == RaceState.Countdown) yield return null;
            yield return Ticks(50 * 3);
            race.QaFinishNow();
            yield return null;
            Assert.AreEqual(0, flow.LastDailyBonus, "the bonus is paid once a day");
            Assert.AreEqual(1, save.Data.progress.daily.claims.Count);
            Assert.GreaterOrEqual(save.Data.Coins, coinsAfterFirst);
            flow.EnterMenu();
        }

        private static Button FindButton(Component root, string name)
        {
            foreach (var b in root.GetComponentsInChildren<Button>(true)) if (b.name == name) return b;
            Assert.Fail("no button " + name); return null;
        }
    }
}
