using NUnit.Framework;
using RacingGame;

namespace RacingGame.Tests
{
    public class RaceResultsTests
    {
        [Test]
        public void PlayerPropertyFindsPlayerEntry()
        {
            var results = new RaceResults
            {
                IsArena = false,
                LapsRequired = 3,
                RaceDuration = 120.5
            };

            var ai1 = new RaceResultEntry { Name = "AI_1", IsPlayer = false, Position = 2 };
            var player = new RaceResultEntry { Name = "Player", IsPlayer = true, Position = 1 };
            var ai2 = new RaceResultEntry { Name = "AI_2", IsPlayer = false, Position = 3 };

            results.Entries.Add(ai1);
            results.Entries.Add(player);
            results.Entries.Add(ai2);

            Assert.AreSame(player, results.Player);
            Assert.AreEqual("Player", results.Player.Name);
            Assert.AreEqual(1, results.Player.Position);
        }

        [Test]
        public void PlayerPropertyReturnsNullWhenNoPlayerPresent()
        {
            var results = new RaceResults();
            results.Entries.Add(new RaceResultEntry { Name = "AI_1", IsPlayer = false });
            results.Entries.Add(new RaceResultEntry { Name = "AI_2", IsPlayer = false });

            Assert.IsNull(results.Player);
        }

        [Test]
        public void ResultEntryFieldsHoldValues()
        {
            var entry = new RaceResultEntry
            {
                Name = "Racer1",
                IsPlayer = true,
                Position = 2,
                Finished = true,
                Survived = true,
                TotalTime = 55.4,
                BestLap = 17.2,
                Laps = 3,
                Kills = 4,
                Deaths = 1
            };

            Assert.AreEqual("Racer1", entry.Name);
            Assert.IsTrue(entry.IsPlayer);
            Assert.AreEqual(2, entry.Position);
            Assert.IsTrue(entry.Finished);
            Assert.IsTrue(entry.Survived);
            Assert.AreEqual(55.4, entry.TotalTime);
            Assert.AreEqual(17.2, entry.BestLap);
            Assert.AreEqual(3, entry.Laps);
            Assert.AreEqual(4, entry.Kills);
            Assert.AreEqual(1, entry.Deaths);
        }
    }
}
