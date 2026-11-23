using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class RivalGapPlayTests
    {
        private GameFlow flow;
        private float capture;

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }

        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            PauseController.ResetAll();
            Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RealGateTimesShowTheNamedNeighborsAndOmitUnmatchedLaps()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            flow.StartRace(Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            race.laps = 3;
            var hud = flow.GetComponent<HudScreen>();
            var labels = Array.Find(hud.GetComponentsInChildren<TextMeshProUGUI>(true), t => t.name == "RivalGaps");
            Assert.NotNull(labels);
            var player = race.Player;
            var field = new List<RaceParticipant>(race.Participants);
            field.Remove(player);
            Assert.GreaterOrEqual(field.Count, 2);
            var lead = field[0]; var tail = field[1];
            lead.displayName = "Jasper"; tail.displayName = "Morgan";
            var path = player.Progress.Path;
            float start = path.startLineDistance;
            float gate = path.Length / player.Progress.Counter.GateCount;
            var distances = new Dictionary<RaceParticipant, float>();
            foreach (var car in race.Participants)
            {
                car.transform.position = path.Position(start + 1f);
                car.Progress.BeginRace();
                distances[car] = 1f;
            }
            void Move(RaceParticipant car, float to)
            {
                float from = distances[car];
                while (from < to)
                {
                    from = Mathf.Min(to, from + 2f);
                    car.transform.position = path.Position(start + from);
                    car.Progress.SimulationStep(0);
                }
                distances[car] = from;
            }
            void WaitForGap(int tenths)
            {
                for (int i = 0; i < tenths; i++) RaceClock.Advance(.1f);
            }
            string Read()
            {
                race.SendMessage("Rank");
                hud.SendMessage("RefreshGaps");
                return labels.gameObject.activeInHierarchy ? labels.text : "";
            }
            string Line(string name, double seconds) => Loc.Format("hud.rival_gap", "name", name,
                "s", seconds.ToString("+0.0;-0.0;0.0", CultureInfo.GetCultureInfo(Loc.Language)));
            Move(lead, gate + 6f);
            Assert.AreEqual("", Read(), "The player has not reached a timed gate");
            WaitForGap(12); Move(player, gate + 4f);
            Assert.AreEqual(Line("Jasper", -1.2), Read(), "Do not fabricate a time for the car behind");
            WaitForGap(8); Move(tail, gate + 2f);
            Assert.AreEqual(Line("Jasper", -1.2) + "\n" + Line("Morgan", .8), Read());
            Assert.AreEqual(2, race.PositionOf(player));
            Move(lead, path.Length + 6f);
            Assert.AreEqual(1, lead.Progress.LapsCompleted);
            Assert.IsFalse(player.Progress.TryGapTo(lead.Progress, out _));
            Assert.AreEqual(Line("Morgan", .8), Read(), "A timestamp from the previous lap must not be reused");
            tail.Progress.BeginRace();
            Assert.IsFalse(player.Progress.TryGapTo(tail.Progress, out _), "A restarted race clears every old gate timestamp");
        }
    }
}
