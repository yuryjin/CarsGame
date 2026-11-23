using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class FixedPacePlayTests
    {
        private float capture;
        private GameFlow flow;
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
            Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator SameRivalOnSameStraightKeepsItsSpeedOnlyInFixedPaceRace()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until)
                yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            var path = TrackPath.Instance;
            var normal = System.Array.Find(flow.tiers, t => t.tierId == "normal");
            var steady = System.Array.Find(flow.events, t => t.eventId == "pace");
            Assert.IsTrue(steady.fixedRivalPace);
            var standard = System.Array.Find(flow.events, t => t.eventId == "e2");
            Assert.IsFalse(standard.fixedRivalPace);
            var means = new float[2, 2];
            var multipliers = new float[2, 2];
            float from = 0f, smallestTurn = float.MaxValue;
            // Use the straightest usable 55 metres of the mountain road, outside its fork.
            // The braking profile also includes future bends and does not identify a straight by itself.
            for (float start = 320f; start < path.Length - 57f; start += 2f)
            {
                float turn = 0f;
                for (float d = start; d < start + 55f; d += 3f)
                    turn += Vector3.Angle(path.Tangent(d), path.Tangent(d + 3f));
                if (turn < smallestTurn)
                {
                    smallestTurn = turn;
                    from = start;
                }
            }
            Assert.Greater(from, 0f);
            for (int fixedMode = 0; fixedMode < 2; fixedMode++)
                for (int ahead = 0; ahead < 2; ahead++)
                {
                    var ev = Object.Instantiate(fixedMode == 1 ? steady : standard);
                    ev.unlockAfterEventId = string.Empty;
                    ev.reversed = false;
                    ev.aiCountOverride = -1;
                    ev.pickupCount = 0;
                    ev.laps = 2;
                    race.spawner.aiBodyVariety = false;
                    RaceDirector.Instance.Reseed(12345);
                    flow.StartRace(ev, normal);
                    Assert.AreEqual(normal.aiCount + 1, race.Participants.Count);
                    var player = race.Player;
                    var bot = race.Participants.Where(p => !p.isPlayer)
                        .OrderBy(p => p.GetComponent<AiDriver>().profile.topSpeedFactor).First();
                    var ai = bot.GetComponent<AiDriver>();
                    while (race.State == RaceState.Countdown)
                        yield return null;
                    // Cross the line from the grid so both progress counters place the later
                    // measurement on lap one, even when the leading player is past halfway.
                    var launch = player.gameObject.AddComponent<ScriptedDriverInput>();
                    launch.Set(1f, 0f);
                    player.Car.SetInputSource(launch);
                    for (int tick = 0; tick < 500 && (player.Progress.GatesPassedThisLap == 0 || bot.Progress.GatesPassedThisLap == 0); tick++)
                        yield return null;
                    Assert.Greater(player.Progress.GatesPassedThisLap, 0);
                    Assert.Greater(bot.Progress.GatesPassedThisLap, 0);
                    // Use a real Normal driver and keep the other grid cars clear of the measured straight.
                    foreach (var other in race.Participants)
                    {
                        if (other == player || other == bot) continue;
                        other.Car.inputEnabled = false;
                        other.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                        other.Car.Teleport(other.transform.position + Vector3.up * 15f, other.transform.rotation);
                    }
                    for (int i = 0; i < 90; i++)
                        RaceClock.Advance(.25f);
                    player.Car.inputEnabled = false;
                    player.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                    path.Pose(from + (ahead == 1 ? 112f : -88f), 0f, out var playerPos, out var playerRot);
                    player.Car.Teleport(playerPos + Vector3.up * 4f, playerRot);
                    player.offTrackResetSeconds = 999f;
                    path.Pose(from - 12f, 0f, out var startPos, out var startRot);
                    bot.Car.Teleport(startPos + Vector3.up * .34f, startRot);
                    var constraints = bot.Car.Body.constraints;
                    bot.Car.Body.constraints = RigidbodyConstraints.FreezeAll;
                    bot.Car.inputEnabled = false;
                    // The regular race samples its gap on an unscaled half-second clock.
                    // Feed that same sample at the same physics tick on all four runs.
                    for (int i = 0; i < 8; i++) yield return null;
                    var sample = typeof(RaceManager).GetMethod("ApplyRubberBand",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    Assert.IsNotNull(sample);
                    sample.Invoke(race, null);
                    float gap = player.Progress.TotalProgress - bot.Progress.TotalProgress;
                    Assert.Greater((ahead == 1 ? 1f : -1f) * gap, 60f);
                    Assert.Greater(race.RaceTime, RacingGame.Core.Sim.RubberBand.WarmupSeconds);
                    RaceDirector.Instance.Reseed(12345);
                    ai.Initialize(ai.profile, 0f);
                    bot.GetComponent<CarCannon>().locked = true;
                    bot.Car.Teleport(startPos + Vector3.up * .05f, startRot);
                    bot.Car.Body.constraints = constraints;
                    bot.Car.inputEnabled = true;
                    // Use one entry speed and let the real rival provide all steering, throttle and braking.
                    float stockCruise = bot.Car.maxSpeed * ai.profile.topSpeedFactor * .90f;
                    bot.Car.Body.linearVelocity = bot.transform.forward * (stockCruise / 3.6f);
                    int count = 0;
                    float speed = 0f, pace = 0f;
                    for (int tick = 0; tick < 1000; tick++)
                    {
                        float progress = path.SignedDelta(from, bot.Progress.Distance);
                        if (progress > 38f)
                            break;
                        if (progress >= 12f)
                        {
                            count++;
                            speed += Mathf.Abs(bot.Car.SpeedKmh);
                            pace += ai.speedMultiplier;
                        }
                        yield return null;
                    }
                    Assert.Greater(count, 35, $"segment from {from:F0}m");
                    means[fixedMode, ahead] = speed / count;
                    multipliers[fixedMode, ahead] = pace / count;
                    Debug.Log($"[PaceRace] fixed={fixedMode} playerAhead={ahead} segment={from + 12:F1}..{from + 38:F1}m mean={speed / count:F2}km/h multiplier={pace / count:F4} count={count} profile={ai.profile.rivalName}");
                    flow.EnterMenu();
                    Object.Destroy(ev);
                    yield return null;
                }
            Assert.AreEqual(1f, multipliers[1, 0], .001f);
            Assert.AreEqual(1f, multipliers[1, 1], .001f);
            float fixedDifference = Mathf.Abs(means[1, 1] - means[1, 0]);
            Assert.Less(fixedDifference, .25f);
            Assert.Greater(multipliers[0, 1], multipliers[0, 0] + .05f);
            Assert.Greater(means[0, 1] - means[0, 0], fixedDifference + .05f);
        }
    }
}
