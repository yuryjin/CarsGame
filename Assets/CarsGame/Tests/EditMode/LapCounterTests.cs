using NUnit.Framework;
using RacingGame.Core.Track;

namespace RacingGame.Tests
{
    public class LapCounterTests
    {
        private static LapCounter Make() => new LapCounter(940f, 40f, 12, 6f);

        private static int Drive(LapCounter c, float from, float to, float step = 0.6f)
        {
            int laps = 0;
            for (float d = from; d <= to; d += step) { c.Step(d); if (c.LapJustCompleted) laps++; }
            return laps;
        }

        [Test]
        public void FullForwardLapCountsOnce()
        {
            var c = Make();
            c.Begin(30f);                       // 10 m behind the line
            int laps = Drive(c, 30f, 30f + 940f + 20f);
            Assert.AreEqual(1, laps);
            Assert.AreEqual(1, c.LapsCompleted);
        }

        [Test]
        public void StartingBehindTheLineDoesNotCountCrossingItAsALap()
        {
            var c = Make();
            c.Begin(30f);
            Drive(c, 30f, 60f);
            Assert.AreEqual(0, c.LapsCompleted);
            Assert.AreEqual(1, c.NextGate);
        }

        [Test]
        public void TeleportAcrossGatesCreditsNothing()
        {
            var c = Make();
            c.Begin(30f);
            Drive(c, 30f, 100f);                // passed gate 0 legitimately
            int before = c.NextGate;
            c.Resync(30f + 800f);               // teleport most of the way round
            Drive(c, 830f, 900f);
            Assert.AreEqual(before, c.NextGate, "gates must not advance after a teleport");
            Assert.AreEqual(0, c.LapsCompleted);
        }

        [Test]
        public void ImplausibleJumpWithoutResyncIsIgnored()
        {
            var c = Make();
            c.Begin(30f);
            c.Step(30.5f);
            c.Step(30.5f + 300f);               // 300 m in one tick
            Assert.AreEqual(0, c.NextGate, "the line was never crossed legitimately");
            Assert.AreEqual(0, c.LapsCompleted);
        }

        [Test]
        public void ReverseDrivingNeverCredits()
        {
            var c = Make();
            c.Begin(30f);
            int laps = 0;
            for (float d = 30f; d > 30f - 940f * 2f; d -= 0.6f) { c.Step(d); if (c.LapJustCompleted) laps++; }
            Assert.AreEqual(0, laps);
            Assert.AreEqual(0, c.NextGate, "no gate may be credited while reversing");
        }

        [Test]
        public void BackAndForthOverTheLineDoesNotDoubleCount()
        {
            var c = Make();
            c.Begin(30f);
            Drive(c, 30f, 50f);                 // cross gate 0 forward
            for (float d = 50f; d > 30f; d -= 0.6f) c.Step(d);   // back over it
            Drive(c, 30f, 50f);                 // forward again
            Assert.AreEqual(1, c.NextGate);
        }

        [Test]
        public void LapCompletesOnlyWhenTheLineIsCrossedAgain()
        {
            var c = Make();
            c.Begin(30f);
            float lastGate = c.GateDistance(11);              // 40 + 11 * 78.33 = 901.7
            Drive(c, 30f, lastGate + 5f);                     // past every intermediate gate
            Assert.AreEqual(0, c.LapsCompleted, "no lap before the line");
            Assert.IsTrue(c.LineArmed);
            Assert.AreEqual(0, c.NextGate);
            int laps = Drive(c, lastGate + 5f, 940f + 45f);   // across the line at 40 (= 980 unwrapped)
            Assert.AreEqual(1, laps);
            Assert.AreEqual(1, c.LapsCompleted);
            Assert.AreEqual(1, c.NextGate);
            Assert.IsFalse(c.LineArmed);
        }

        [Test]
        public void ArmedCarInTheTailRanksAboveAGridCar()
        {
            var armed = Make(); armed.Begin(30f); Drive(armed, 30f, 30f + 900f);
            var grid = Make(); grid.Begin(30f);
            Assert.IsTrue(armed.LineArmed);
            Assert.AreEqual(890f, armed.TotalProgress(armed.Wrap(930f)), 0.01f);   // lap 0, 890 m past the line
            Assert.AreEqual(-10f, grid.TotalProgress(30f), 0.01f);                  // 10 m before the line
            Assert.Greater(armed.TotalProgress(armed.Wrap(930f)), grid.TotalProgress(30f));
        }

        [Test]
        public void GridCarBeforeTheLineRanksBelowCarJustPastIt()
        {
            var behind = Make(); behind.Begin(30f);
            var past = Make(); past.Begin(30f); Drive(past, 30f, 45f);
            Assert.Less(behind.TotalProgress(30f), past.TotalProgress(45f));
            Assert.Less(behind.TotalProgress(30f), 0f);
        }

        [Test]
        public void TotalProgressIsMonotonicAcrossLaps()
        {
            var c = Make();
            c.Begin(30f);
            float last = float.NegativeInfinity;
            for (float d = 30f; d <= 30f + 940f * 2f + 20f; d += 0.6f)   // line at 40, 980, 1920
            {
                c.Step(d);
                float p = c.TotalProgress(c.Wrap(d));
                Assert.GreaterOrEqual(p + 0.001f, last, $"progress went backwards at d={d}");
                last = p;
            }
            Assert.AreEqual(2, c.LapsCompleted);
        }
    }
}
