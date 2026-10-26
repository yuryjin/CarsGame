using System.Linq;
using NUnit.Framework;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public class RaceCupBoundaryTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void EveryStarCombinationRequiresSixStarsAndAllThreeFinishes(int cupIndex)
        {
            var cup = RaceCups.All[cupIndex];
            // Exhaust all 512 combinations of the three independent star masks, plus missing finishes.
            for (int a = 0; a < 8; a++)
            for (int b = 0; b < 8; b++)
            for (int c = 0; c < 8; c++)
            {
                var masks = new[] { a, b, c };
                int stars = masks.Sum(mask => ConvertStars(mask));
                var progress = new ProgressData();
                for (int i = 0; i < 3; i++)
                    progress.bests.Add(new EventBest { eventId = cup.Events[i], stars = masks[i], bestTimeMs = 1000 });
                Assert.AreEqual(stars, cup.Stars(progress));
                Assert.AreEqual(stars >= 6, cup.Complete(progress), $"{cup.Id}: {a}/{b}/{c}");
                for (int missing = 0; missing < 3; missing++)
                {
                    progress.bests[missing].bestTimeMs = 0;
                    Assert.IsFalse(cup.Complete(progress), $"{cup.Id}: missing finish {missing}");
                    progress.bests[missing].bestTimeMs = 1000;
                }
            }
        }

        private static int ConvertStars(int mask) => new[] { 1, 2, 4 }.Count(bit => (mask & bit) != 0);

        [Test]
        public void UnrelatedEventsAndUnknownStarBitsCannotUnlockACup()
        {
            var progress = new ProgressData();
            progress.bests.Add(null);
            progress.bests.Add(new EventBest { eventId = "outside-cup", bestTimeMs = 1000, stars = 7 });
            foreach (var cup in RaceCups.All)
            foreach (var id in cup.Events)
                progress.bests.Add(new EventBest { eventId = id, bestTimeMs = 1000, stars = 8 });
            foreach (var cup in RaceCups.All)
            {
                Assert.AreEqual(0, cup.Stars(progress));
                Assert.IsFalse(cup.Complete(progress));
            }
            Assert.IsFalse(RaceCups.GrantCompleted(progress));
        }

        [Test]
        public void AllCupRewardsSurviveSaveReloadAndRepeatedGrantWithoutDuplicates()
        {
            var save = new SaveData();
            save.progress.cosmetics = null; // Older saves may not have an ownership list.
            foreach (var cup in RaceCups.All)
            foreach (var id in cup.Events)
                save.progress.bests.Add(new EventBest { eventId = id, bestTimeMs = 1000, stars = 3 });
            Assert.IsTrue(RaceCups.GrantCompleted(save.progress));
            var reloaded = SaveMigrator.FromJson(SaveMigrator.ToJson(save));
            Assert.IsFalse(RaceCups.GrantCompleted(reloaded.progress));
            CollectionAssert.AreEquivalent(RaceCups.All.Select(cup => cup.Reward), reloaded.progress.cosmetics);
            Assert.AreEqual(0, reloaded.Coins, "Cosmetic rewards must not mint currency");
        }
    }
}
