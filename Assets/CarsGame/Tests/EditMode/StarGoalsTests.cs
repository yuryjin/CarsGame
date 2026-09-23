using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class StarGoalsTests
    {
        [Test]
        public void StarMaskEvaluatesTop3KillsAndZeroDeaths()
        {
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            ev.star1TargetPosition = 3;
            ev.star2TargetKills = 2;
            ev.star3ZeroDeaths = true;

            var entry = new RaceResultEntry
            {
                IsPlayer = true,
                Position = 1,
                Finished = true,
                Kills = 2,
                Deaths = 0,
                TotalTime = 45.0,
                BestLap = 15.0
            };

            int mask = ev.StarMask(entry);
            Assert.AreEqual(7, mask, "1st place + 2 kills + 0 deaths = 3 stars (mask 7)");
            Assert.AreEqual(3, RaceEventDefinition.CountStars(mask));

            Object.DestroyImmediate(ev);
        }

        [Test]
        public void StarMaskHandlesPartialCompletionAndDNF()
        {
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            ev.star1TargetPosition = 3;
            ev.star2TargetKills = 2;
            ev.star3ZeroDeaths = true;

            var entry = new RaceResultEntry
            {
                IsPlayer = true,
                Position = 4,
                Finished = true,
                Kills = 2,
                Deaths = 1,
                TotalTime = 60.0,
                BestLap = 20.0
            };

            int mask = ev.StarMask(entry);
            Assert.AreEqual(2, mask, "4th place + 2 kills + 1 death = only Star 2 (mask 2)");
            Assert.AreEqual(1, RaceEventDefinition.CountStars(mask));

            entry.Position = 2;
            entry.Finished = false;
            int dnfMask = ev.StarMask(entry);
            Assert.AreEqual(2, dnfMask, "DNF keeps kill star if completed, but finishes require Finished=true");

            Object.DestroyImmediate(ev);
        }
    }
}
