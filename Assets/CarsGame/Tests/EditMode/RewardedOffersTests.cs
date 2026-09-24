using NUnit.Framework;
using RacingGame.Core.Economy;

namespace RacingGame.Tests
{
    public class RewardedOffersTests
    {
        [SetUp]
        public void SetUp()
        {
            RewardedOffers.ConsumeTrial(null);
            RewardedOffers.ClearLastRaceTrial();
        }

        [TearDown]
        public void TearDown()
        {
            RewardedOffers.ConsumeTrial(null);
            RewardedOffers.ClearLastRaceTrial();
        }

        [Test]
        public void TrialUpgradeAppliesToOneRaceWithoutChangingSavedLevels()
        {
            var saved = new[] { 1, 2, 0 };
            RewardedOffers.GrantTrial(UpgradeKind.Engine);

            var raced = RewardedOffers.ConsumeTrial(saved);

            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, saved);
            CollectionAssert.AreEqual(new[] { 2, 2, 0 }, raced);
            Assert.AreNotSame(saved, raced);
            Assert.AreEqual((int)UpgradeKind.Engine, RewardedOffers.LastRaceTrial);
            Assert.AreEqual(-1, RewardedOffers.TrialUpgrade);

            Assert.AreSame(saved, RewardedOffers.ConsumeTrial(saved), "no trial returns the saved levels unchanged");
            Assert.AreEqual(-1, RewardedOffers.LastRaceTrial);
        }

        [Test]
        public void CannotTryAnUpgradeWhenOneIsPendingOrAlreadyAtMaxLevel()
        {
            var levels = new[] { 0, 0, UpgradeCatalog.MaxLevel };
            Assert.IsTrue(RewardedOffers.CanTry(levels, UpgradeKind.Engine));
            Assert.IsFalse(RewardedOffers.CanTry(levels, UpgradeKind.Cannon));
            RewardedOffers.GrantTrial(UpgradeKind.Engine);
            Assert.IsFalse(RewardedOffers.CanTry(levels, UpgradeKind.Armor));
        }

        [Test]
        public void TrialAtMaxLevelStaysCapped()
        {
            var saved = new[] { UpgradeCatalog.MaxLevel, 1, 1 };
            RewardedOffers.GrantTrial(UpgradeKind.Engine);

            var raced = RewardedOffers.ConsumeTrial(saved);

            Assert.AreEqual(UpgradeCatalog.MaxLevel, raced[(int)UpgradeKind.Engine]);
            Assert.AreEqual((int)UpgradeKind.Engine, RewardedOffers.LastRaceTrial);
        }
    }
}
