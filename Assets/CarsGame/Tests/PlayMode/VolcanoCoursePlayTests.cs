using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class VolcanoCoursePlayTests
    {
        private GameFlow flow;
        private float capture;
        private string saved;
        [SetUp]
        public void Setup()
        {
            capture = Time.captureDeltaTime;
        }
        [TearDown]
        public void Cleanup()
        {
            if (flow != null)
                flow.EnterMenu();
            if (saved != null && PlatformGlue.Save != null)
            {
                JsonUtility.FromJsonOverwrite(saved, PlatformGlue.Save.Data);
                PlatformGlue.Save.MarkDirty();
            }
            Time.captureDeltaTime = capture;
        }
        [UnityTest, Timeout(360000)]
        public IEnumerator BothDriversFinishOnTheAllowedRoadAndAnAdmittedCarCanStillCross()
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
            var hud = flow.GetComponent<HudScreen>();
            var normal = System.Array.Find(flow.tiers, t => t.tierId == "normal");
            var source = System.Array.Find(flow.events, e => e.eventId == "volcano");
            Assert.AreEqual(2, source.laps);
            Assert.AreEqual(MountainFork.Volcano, source.mountainFork);
            var ev = Object.Instantiate(source);
            ev.aiCountOverride = 1;
            ev.pickupCount = 0;
            saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
            var levels = PlatformGlue.Save.Data.progress.upgrades;
            System.Array.Clear(levels, 0, levels.Length);
            race.spawner.playerBodyId = "classic";
            race.spawner.playerGridSlot = 0;
            race.spawner.aiBodyVariety = false;
            RaceDirector.Instance.Reseed(12345);
            flow.StartRace(ev, normal);
            var player = race.Player;
            var bot = race.Participants[0].isPlayer ? race.Participants[1] : race.Participants[0];
            var botAi = bot.GetComponent<AiDriver>();
            var pilot = player.gameObject.AddComponent<AiDriver>();
            pilot.Initialize(null, 0f);
            var manual = player.gameObject.AddComponent<ScriptedDriverInput>();
            var path = player.Progress.Path;
            var fork = path.Fork;
            var mainGate = fork.transform.Find("VolcanoMainGate").GetComponent<Collider>();
            var sideGate = fork.transform.Find("VolcanoShortcutGate").GetComponent<Collider>();
            Assert.IsFalse(mainGate.gameObject.activeSelf);
            Assert.IsTrue(sideGate.gameObject.activeSelf);
            player.Car.SetInputSource(pilot);
            bot.GetComponent<CarCannon>().locked = true;
            while (race.State == RaceState.Countdown)
                yield return null;
            bool holding = false, heldOnce = false, yielded = false, caughtUp = false, entering = false, prewarning = false, sawOpen = false;
            bool oldRoadCompleted = false, newRoadCompleted = false, botTookNewRoad = false, lastLapChanged = false;
            int gateP = player.Progress.GatesPassedThisLap, gateB = bot.Progress.GatesPassedThisLap, pGates = 0, bGates = 0;
            var botConstraints = bot.Car.Body.constraints;
            var playerConstraints = player.Car.Body.constraints;
            // Let the player clear the bend before staging the other car inside it.
            // This keeps the parked car out of the player's first-lap driving line.
            bool botReleased = false;
            bot.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
            bot.Car.inputEnabled = false;
            var racers = new[] { player, bot };
            foreach (var racer in racers)
                racer.Car.Teleported += () => Debug.Log($"[VolcanoRecovery] {racer.name} lap={racer.Progress.LapsCompleted} distance={racer.Progress.Distance:F1} state={fork.OpenVolcanoRoute} previousSurface={racer.Car.Surface} deaths={racer.Deaths} auto={racer.AutoResets}");
            int tickStart = SimulationDriver.Instance.Tick;
            var trace = new System.Text.StringBuilder("tick,name,lap,along,error,speed,surface,health,route,held,yielded,posX,posZ\n");
            string traceOutput = System.Environment.GetEnvironmentVariable("CG_VOLCANO_TRACE");
            for (int tick = 0; tick < 16500 && !(race.HasFinished(player) && race.HasFinished(bot)); tick++)
            {
                float pAlong = path.SignedDelta(fork.ApproachDistance, player.Progress.Distance);
                float bAlong = path.SignedDelta(fork.ApproachDistance, bot.Progress.Distance);
                if (!botReleased && pAlong > 55f)
                {
                    botReleased = true;
                    bot.Car.Body.constraints = botConstraints;
                    bot.Car.inputEnabled = true;
                }
                if (botReleased && !heldOnce && bot.Progress.LapsCompleted == 0 && bAlong >= 12f && bAlong <= 24f)
                {
                    Assert.AreEqual(ForkRoute.MainRoad, botAi.CurrentFork);
                    bot.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                    bot.Car.inputEnabled = false;
                    heldOnce = holding = true;
                }
                if (!prewarning && player.Progress.LapsCompleted == 0 && hud.LastHint == Loc.Get("volcano.changing"))
                {
                    prewarning = true;
                    var visible = GameObject.Find("Hints").GetComponent<TMP_Text>();
                    Assert.IsTrue(visible.isActiveAndEnabled);
                    Assert.AreEqual(hud.LastHint, visible.text);
                }
                if (player.Progress.LapsCompleted == 1 && !lastLapChanged)
                {
                    lastLapChanged = true;
                    Assert.IsTrue(prewarning && holding);
                    Assert.AreEqual(ForkRoute.Shortcut, fork.OpenVolcanoRoute);
                    Assert.IsTrue(mainGate.gameObject.activeSelf);
                    Assert.IsFalse(sideGate.gameObject.activeSelf);
                    Assert.AreEqual(ForkRoute.MainRoad, fork.VolcanoRouteFor(bot));
                    Assert.IsTrue(Physics.GetIgnoreCollision(mainGate, bot.GetComponent<Collider>()));
                    Assert.IsFalse(Physics.GetIgnoreCollision(mainGate, player.GetComponent<Collider>()));
                    bot.Car.Body.constraints = botConstraints;
                    bot.Car.inputEnabled = true;
                    holding = false;
                }
                if (player.Progress.LapsCompleted == 1 && pAlong < -25f && hud.LastHint == Loc.Get("volcano.shortcut_open"))
                    sawOpen = true;
                if (player.Progress.LapsCompleted == 1 && pAlong >= -fork.DecisionLead && pAlong < fork.MainSpan + fork.ExitRunout && !newRoadCompleted)
                {
                    if (!entering)
                    {
                        Assert.IsTrue(sawOpen);
                        Assert.AreEqual(ForkRoute.Shortcut, fork.VolcanoRouteFor(player));
                        entering = true;
                        player.Car.SetInputSource(manual);
                    }
                    fork.FollowShortcut(player.Car, player.Progress.Distance, out var steer, out var throttle);
                    manual.Set(throttle, steer);
                }
                else if (entering)
                {
                    entering = false;
                    newRoadCompleted = true;
                    player.Car.SetInputSource(pilot);
                }
                if (lastLapChanged && bot.Progress.LapsCompleted == 0 && bAlong >= fork.MainSpan)
                    oldRoadCompleted = true;
                if (bot.Progress.LapsCompleted == 1 && botAi.CurrentFork == ForkRoute.Shortcut)
                    botTookNewRoad = true;
                // Give the staged trailer time to close the gap, away from the road the two cars share.
                if (newRoadCompleted && !yielded && pAlong > fork.MainSpan + 34f)
                {
                    yielded = true;
                    player.Car.inputEnabled = false;
                    player.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                }
                if (yielded && !caughtUp && bot.Progress.LapsCompleted == 1 && bAlong > fork.MainSpan - 45f)
                {
                    caughtUp = true;
                    player.Car.Body.constraints = playerConstraints;
                    player.Car.inputEnabled = true;
                }
                if (player.Progress.GatesPassedThisLap != gateP)
                {
                    gateP = player.Progress.GatesPassedThisLap;
                    pGates++;
                }
                if (bot.Progress.GatesPassedThisLap != gateB)
                {
                    gateB = bot.Progress.GatesPassedThisLap;
                    bGates++;
                }
                if (traceOutput != null && tick % 15 == 0)
                {
                    foreach (var racer in racers)
                    {
                        float along = path.SignedDelta(fork.ApproachDistance, racer.Progress.Distance);
                        fork.Project(racer.transform.position, out float error);
                        trace.AppendLine(System.FormattableString.Invariant($"{SimulationDriver.Instance.Tick - tickStart},{racer.name},{racer.Progress.LapsCompleted},{along:F1},{Mathf.Sqrt(error):F2},{racer.Car.SpeedKmh:F1},{racer.Car.Surface},{racer.Health.Health:F1},{racer.GetComponent<AiDriver>().CurrentFork},{holding},{yielded && !caughtUp},{racer.transform.position.x:F1},{racer.transform.position.z:F1}"));
                    }
                }
                yield return null;
            }
            if (traceOutput != null) System.IO.File.WriteAllText(traceOutput, trace.ToString());
            Debug.Log($"[Volcano] prewarning={prewarning} allowedOldExit={oldRoadCompleted} nextSignal={sawOpen} playerGates={pGates} rivalGates={bGates} playerLaps={player.Progress.LapsCompleted} rivalLaps={bot.Progress.LapsCompleted} shortcuts={player.Shortcuts}/{bot.Shortcuts} resets={player.Resets}/{bot.Resets}");
            Assert.IsTrue(oldRoadCompleted && newRoadCompleted && botTookNewRoad && caughtUp);
            Assert.IsTrue(race.HasFinished(player) && race.HasFinished(bot));
            Assert.AreEqual(2, player.Progress.LapsCompleted);
            Assert.AreEqual(2, bot.Progress.LapsCompleted);
            Assert.GreaterOrEqual(pGates, 23);
            Assert.GreaterOrEqual(bGates, 23);
            Assert.AreEqual(0, player.Resets);
            Assert.AreEqual(0, bot.Resets);
            Assert.AreEqual(1, player.Shortcuts);
            Assert.AreEqual(1, bot.Shortcuts);
            flow.RetryRace();
            yield return null;
            Assert.AreEqual(ForkRoute.MainRoad, path.Fork.OpenVolcanoRoute);
            Assert.IsFalse(path.Fork.transform.Find("VolcanoMainGate").gameObject.activeSelf);
            Assert.IsTrue(path.Fork.transform.Find("VolcanoShortcutGate").gameObject.activeSelf);
            Object.Destroy(ev);
        }
    }
}
