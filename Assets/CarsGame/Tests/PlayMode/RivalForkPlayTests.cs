using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class RivalForkPlayTests
    {
        private GameFlow flow;
        private float capture;
        private string savedProfile;
        private RaceEventDefinition ev;
        [SetUp]
        public void Setup()
        {
            capture = Time.captureDeltaTime;
            savedProfile = null;
        }
        [TearDown]
        public void Cleanup()
        {
            if (flow != null)
                flow.EnterMenu();
            if (savedProfile != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(savedProfile, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            if (ev != null) Object.Destroy(ev);
            Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator HurtRivalCollectsRepairAndHealthyTrailerCommitsToLegalShortcut() => RunBothChoices(firstRace: false);

        [UnityTest, Timeout(300000)]
        public IEnumerator FirstRaceRivalsAlsoCompleteBothRoutes() => RunBothChoices(firstRace: true);

        private IEnumerator RunBothChoices(bool firstRace)
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            var until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            var normal = System.Array.Find(flow.tiers, t => t.tierId == "normal");
            var source = System.Array.Find(flow.events, e => e.eventId == "e1");
            savedProfile = JsonUtility.ToJson(PlatformGlue.Save.Data);
            foreach (var entry in PlatformGlue.Save.Data.progress.ledger) entry.races = 0;
            PlatformGlue.Save.Local.races = firstRace ? 0 : 1;
            ev = Object.Instantiate(source);
            ev.laps = 1;
            ev.aiCountOverride = 1;
            ev.pickupCount = 1;
            race.spawner.aiBodyVariety = false;
            foreach (bool hurt in new[] { true, false })
            {
                RaceDirector.Instance.Reseed(12345);
                flow.StartRace(ev, normal);
                var player = race.Player;
                var bot = race.Participants[0].isPlayer ? race.Participants[1] : race.Participants[0];
                var ai = bot.GetComponent<AiDriver>();
                var path = bot.Progress.Path;
                var fork = path.Fork;
                float startLane = path.Spline.LateralOffset(bot.transform.position, bot.Progress.Distance);
                Assert.AreEqual(firstRace, RaceSetup.IsIntroRace);
                Assert.Greater(firstRace ? startLane : -startLane, 1f,
                    "The first-race and regular grids must approach the bridge from opposite lanes.");
                var repair = System.Array.Find(Object.FindObjectsByType<Pickup>(), p => p.kind == PickupKind.Health
                    && Vector3.Distance(p.transform.position, path.Position(fork.RepairDistance)) < 3f);
                Assert.IsNotNull(repair);
                while (race.State == RaceState.Countdown)
                    yield return null;
                int runStart = SimulationDriver.Instance.Tick;
                player.Car.inputEnabled = false;
                player.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                path.Pose(fork.ApproachDistance + 10f, -2f, out var spot, out var facing);
                player.Car.Teleport(spot + Vector3.up * 4f, facing);
                player.offTrackResetSeconds = 999f;
                bot.GetComponent<CarCannon>().locked = true;
                if (hurt)
                    Assert.IsTrue(bot.Health.TakeDamage(bot.Health.maxHealth * .5f));
                ForkRoute expected = hurt ? ForkRoute.Repair : ForkRoute.Shortcut;
                bool chose = false, mid = false, tookRepair = false, exited = false;
                int gate = bot.Progress.GatesPassedThisLap, gates = 0, rank = 0;
                for (int frame = 0; frame < 8500 && !race.HasFinished(bot); frame++)
                {
                    float d = path.SignedDelta(fork.ApproachDistance, bot.Progress.Distance);
                    if (!chose && ai.CurrentFork != ForkRoute.Undecided)
                    {
                        Assert.AreEqual(expected, ai.CurrentFork);
                        chose = true;
                        rank = race.PositionOf(bot);
                        Assert.Less(d, 0f);
                        Assert.Less(Vector3.Distance(bot.transform.position, player.transform.position), 48f);
                    }
                    if (chose && !exited)
                    {
                        if (d <= fork.MainSpan - 1f)
                            Assert.AreEqual(expected, ai.CurrentFork);
                        else
                        {
                            exited = true;
                            Assert.AreEqual(hurt, bot.Health.RepairsUsed > 0,
                                "Only the long route provides repair before the roads merge.");
                        }
                        if (!mid && d > fork.MainSpan * .38f)
                        {
                            mid = true;
                            if (!hurt)
                            {
                                Assert.IsTrue(path.OnFork(bot.transform.position), "The rival must be on the raised bridge when hit.");
                                Assert.AreEqual(GroundSurface.Track, bot.Car.Surface);
                                Assert.IsTrue(bot.Health.TakeDamage(bot.Health.maxHealth * .5f), "new damage cannot redirect a committed shortcut");
                            }
                        }
                    }
                    tookRepair |= repair.Taken;
                    int next = bot.Progress.GatesPassedThisLap;
                    if (next != gate)
                    {
                        Assert.AreEqual((gate + 1) % bot.Progress.gateCount, next);
                        gate = next;
                        gates++;
                    }
                    yield return null;
                }
                Debug.Log($"[RivalFork] firstRace={firstRace} startLane={startLane:F2} route={expected} positionAtChoice={rank} sideRepair={tookRepair} shortcuts={bot.Shortcuts} gates={gates} lap={(SimulationDriver.Instance.Tick - runStart) * SimulationDriver.FixedStep:F2}s resets={bot.Resets}");
                Assert.IsTrue(chose && mid && exited);
                Assert.IsTrue(race.HasFinished(bot));
                Assert.AreEqual(1, bot.Progress.LapsCompleted);
                Assert.AreEqual(bot.Progress.gateCount, gates);
                Assert.AreEqual(0, bot.Resets);
                Assert.AreEqual(hurt ? 0 : 1, bot.Shortcuts);
                Assert.AreEqual(hurt, tookRepair);
                if (!hurt)
                    Assert.AreEqual(2, rank);
                flow.EnterMenu();
                yield return null;
            }
        }
    }
}
