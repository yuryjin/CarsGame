using NUnit.Framework;
using RacingGame.Core.Economy;

namespace RacingGame.Tests
{
    public class EconomyTests
    {
        [Test]
        public void FirstUpgradeAffordableAfterOneOrTwoRaces()
        {
            int mid = Economy.RaceReward(3, true, null, 1f);
            int last = Economy.RaceReward(5, true, null, 1f);
            Assert.GreaterOrEqual(mid * 1, UpgradeCatalog.Cost(0));
            Assert.GreaterOrEqual(last * 2, UpgradeCatalog.Cost(0));
        }

        [Test]
        public void FirstFinishedRaceAffordsTheFirstUpgrade()
        {
            int easyThird = Economy.RaceReward(3, true, null, 0.8f);
            Assert.Less(easyThird, UpgradeCatalog.Cost(0), "the floor matters: an easy 3rd place alone is short");
            Assert.AreEqual(UpgradeCatalog.Cost(0), Economy.WithFirstRaceFloor(easyThird, true, true));
            Assert.AreEqual(easyThird, Economy.WithFirstRaceFloor(easyThird, true, false), "only the first race");
            Assert.AreEqual(0, Economy.WithFirstRaceFloor(0, false, true), "a DNF earns no floor");
            Assert.AreEqual(300, Economy.WithFirstRaceFloor(300, true, true), "never lowers a bigger reward");
        }

        [Test]
        public void RepeatedKillsOnTheSameRivalPayLess()
        {
            int one = Economy.RaceReward(4, true, new[] { 1 }, 1f);
            int three = Economy.RaceReward(4, true, new[] { 3 }, 1f);
            int spread = Economy.RaceReward(4, true, new[] { 1, 1, 1 }, 1f);
            Assert.AreEqual(25, one - Economy.PositionPayout[3]);
            Assert.Less(three - one, 2 * 25);
            Assert.Greater(spread, three);
        }

        [Test]
        public void DnfAndTierMultiplier()
        {
            Assert.AreEqual(0, Economy.RaceReward(9, false, 0, 3, null, 1f), "DNF with no lap pays nothing");
            Assert.AreEqual(20, Economy.RaceReward(9, false, 2, 3, null, 1f), "DNF pays per completed lap");
            Assert.AreEqual(0, Economy.RaceReward(9, false, 0, 3, new[] { 3 }, 1f), "no kill farming without laps");
            Assert.AreEqual(450, Economy.RaceReward(1, true, null, 1.5f));
        }

        [Test]
        public void UpgradesHaveTradeoffsAndCostsGrow()
        {
            var levels = new[] { 0, 0, 0 };
            Assert.IsTrue(UpgradeCatalog.TryBuy(levels, UpgradeKind.Engine, 150, out int cost));
            Assert.AreEqual(150, cost);
            Assert.IsFalse(UpgradeCatalog.TryBuy(levels, UpgradeKind.Engine, 200, out cost), "second level costs 240");
            Assert.AreEqual(240, cost);
            var fx = UpgradeCatalog.For(new[] { 2, 1, 3 });
            Assert.Greater(fx.MaxSpeedFactor, 1f); Assert.Less(fx.GripFactor, 1f);
            Assert.Greater(fx.MaxHealthFactor, 1f); Assert.Less(fx.TorqueFactor, 1f);
            Assert.Greater(fx.DamageFactor, 1f); Assert.Greater(fx.FireIntervalFactor, 1f);
            var max = new[] { 4, 4, 4 };
            Assert.IsFalse(UpgradeCatalog.TryBuy(max, UpgradeKind.Armor, long.MaxValue, out _));
        }

        [Test]
        public void WeaponCatalogCostsAndEffectsScaleCorrectly()
        {
            var weapons = new[] { 0, 0, 0 };
            Assert.AreEqual(200, WeaponCatalog.Cost(0));
            Assert.IsTrue(WeaponCatalog.TryBuy(weapons, WeaponKind.Mines, 200, out int cost));
            Assert.AreEqual(200, cost);
            Assert.AreEqual(1, weapons[(int)WeaponKind.Mines]);

            // Second level cost
            Assert.AreEqual(300, WeaponCatalog.Cost(1));
            Assert.IsFalse(WeaponCatalog.TryBuy(weapons, WeaponKind.Mines, 250, out cost));
            Assert.AreEqual(300, cost);

            var fx0 = WeaponCatalog.For(0);
            Assert.AreEqual(1f, fx0.AmmoFactor);
            Assert.AreEqual(1f, fx0.CooldownFactor);

            var fx2 = WeaponCatalog.For(2);
            Assert.Greater(fx2.AmmoFactor, 1f);
            Assert.Less(fx2.CooldownFactor, 1f);
            Assert.Greater(fx2.DamageFactor, 1f);
            Assert.Less(fx2.HeatFactor, 1f);

            var maxWeapons = new[] { 3, 3, 3 };
            Assert.IsFalse(WeaponCatalog.TryBuy(maxWeapons, WeaponKind.MachineGun, long.MaxValue, out _));
        }
    }
}
