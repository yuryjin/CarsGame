using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public sealed class PlayerExperienceTests
    {
        [TestCase(0d, "0:00.000")]
        [TestCase(59.9999d, "0:59.999")]
        [TestCase(60d, "1:00.000")]
        [TestCase(3599.9999d, "59:59.999")]
        [TestCase(3600d, "60:00.000")]
        [TestCase(-1d, "--:--.---")]
        [TestCase(double.NaN, "--:--.---")]
        [TestCase(double.PositiveInfinity, "--:--.---")]
        [TestCase(double.MaxValue, "--:--.---")]
        public void RaceTimeFormattingUsesCompleteMilliseconds(double seconds, string expected)
        {
            Assert.AreEqual(expected, UiFactory.FormatTime(seconds));
        }

        [Test]
        public void RaceTimeFormattingMatchesTheHudInEverySystemCulture()
        {
            var before = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                Assert.AreEqual("1:01.125", UiFactory.FormatTime(61.125));
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = before; }
        }

        [Test]
        public void HudStatusIconsAndChineseLanguageNameAreBakedForWebGl()
        {
            var font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/CarsGame/Fonts/NotoSansSC-Subset SDF.asset");
            Assert.IsNotNull(font);
            Assert.AreEqual(TMPro.AtlasPopulationMode.Static, font.atlasPopulationMode);
            foreach (char glyph in "★☆×▲▼◆●中文")
                Assert.IsTrue(font.HasCharacter(glyph), "Missing pre-baked UI glyph U+" + ((int)glyph).ToString("X4"));
        }

        [Test]
        public void OldSaveKeepsPurchasesAndStarsAndGetsTwoWeaponLoadout()
        {
            var data = SaveMigrator.FromJson("{\"version\":4,\"progress\":{\"equippedPrimary\":1,\"weapons\":[1,2,1,0],\"bests\":[{\"eventId\":\"e2\",\"stars\":7,\"bestTimeMs\":123}]}}");
            Assert.AreEqual(SaveData.CurrentVersion, data.version);
            Assert.AreEqual(1, data.progress.equippedPrimary);
            Assert.AreEqual(2, data.progress.reservePrimary);
            Assert.IsFalse(data.progress.fullArsenal);
            CollectionAssert.AreEqual(new[] { 1, 2, 1, 0 }, data.progress.weapons);
            Assert.AreEqual(7, data.progress.bests[0].stars);
        }

        [Test]
        public void LoadoutRoundTripsAndNewerSelectionSurvivesCloudMerge()
        {
            var older = new SaveData { updatedAtUnixMs = 10 };
            var newer = new SaveData { updatedAtUnixMs = 20 };
            newer.progress.equippedPrimary = 49; newer.progress.reservePrimary = 7; newer.progress.fullArsenal = true;
            var merged = SaveMigrator.FromJson(SaveMigrator.ToJson(SaveMerge.Merge(newer, older)));
            Assert.AreEqual(49, merged.progress.equippedPrimary);
            Assert.AreEqual(7, merged.progress.reservePrimary);
            Assert.IsTrue(merged.progress.fullArsenal);
        }

        [Test]
        public void CompactLoadoutRestrictsEverySwapAndFreeArsenalRestoresChoice()
        {
            var go = new GameObject("LoadoutTest");
            try
            {
                var w = go.AddComponent<CarWeapons>();
                w.ConfigureLoadout(true, PrimaryWeaponType.Shotgun, PrimaryWeaponType.HomingRocket);
                Assert.IsFalse(w.TrySwitchPrimary(PrimaryWeaponType.HeavyCannon, 10));
                Assert.AreEqual((int)PrimaryWeaponType.HomingRocket + 1, w.CyclePrimarySlot(3, 1));
                Assert.IsTrue(w.TrySwitchPrimary(PrimaryWeaponType.HomingRocket, 20));
                Assert.AreEqual(3, w.CyclePrimarySlot(50, 1));
                Assert.AreEqual(3, w.CyclePrimarySlot(50, -1));
                w.ConfigureLoadout(false, PrimaryWeaponType.Shotgun, PrimaryWeaponType.HomingRocket);
                Assert.IsTrue(w.TrySwitchPrimary(PrimaryWeaponType.HeavyCannon, 30));
                Assert.IsFalse(w.TrySwitchPrimary(PrimaryWeaponType.MachineGun, 40), "unbought MG stays locked");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void InvalidOrDuplicateLoadoutHasTwoUsableWeapons()
        {
            var go = new GameObject("LoadoutFallback");
            try
            {
                var w = go.AddComponent<CarWeapons>();
                w.ConfigureLoadout(true, PrimaryWeaponType.MachineGun, (PrimaryWeaponType)999);
                Assert.AreEqual(PrimaryWeaponType.Cannon, w.LoadoutFirst);
                Assert.AreEqual(PrimaryWeaponType.Shotgun, w.LoadoutSecond);
                w.ConfigureLoadout(true, PrimaryWeaponType.Shotgun, PrimaryWeaponType.Shotgun);
                Assert.AreEqual(PrimaryWeaponType.Cannon, w.LoadoutSecond);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(SkillGoal.DriftBoosts)]
        [TestCase(SkillGoal.Shortcuts)]
        [TestCase(SkillGoal.CeilingBoost)]
        [TestCase(SkillGoal.BombPasses)]
        public void SkillStarsNeedTheNamedActionInsteadOfKills(SkillGoal skill)
        {
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            try
            {
                ev.skillGoal = skill; ev.skillTarget = 2;
                var result = new RaceResultEntry { Kills = 99 };
                Assert.AreEqual(0, ev.StarMask(result) & 2);
                result.DriftBoosts = result.Shortcuts = result.CeilingBoosts = result.BombPasses = 1;
                Assert.AreEqual(0, ev.StarMask(result) & 2);
                result.DriftBoosts = result.Shortcuts = result.CeilingBoosts = result.BombPasses = 2;
                Assert.AreEqual(2, ev.StarMask(result) & 2);
            }
            finally { Object.DestroyImmediate(ev); }
        }

        [Test]
        public void CleanGoalsNeedFinishAndCannotIgnoreResets()
        {
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            try
            {
                ev.skillGoal = SkillGoal.CleanFinalLap; ev.finishGoal = FinishGoal.CleanRun;
                var result = new RaceResultEntry { CleanFinalLap = true, Position = 1 };
                Assert.AreEqual(0, ev.StarMask(result));
                result.Finished = true;
                Assert.AreEqual(7, ev.StarMask(result));
                result.Resets = 1;
                Assert.AreEqual(3, ev.StarMask(result), "a reset before the final lap only loses the clean-run star");
                result.CleanFinalLap = false;
                Assert.AreEqual(1, ev.StarMask(result));
            }
            finally { Object.DestroyImmediate(ev); }
        }

        [Test]
        public void CupRequiresEveryFinishAndSixStarsAndPaysCosmeticOnce()
        {
            var p = new ProgressData();
            var cup = RaceCups.All[0];
            p.bests.Add(new EventBest { eventId = "e1", stars = 7, bestTimeMs = 100 });
            p.bests.Add(new EventBest { eventId = "e2", stars = 7, bestTimeMs = 100 });
            Assert.IsFalse(cup.Complete(p), "six stars alone do not skip the third event");
            Assert.IsFalse(RaceCups.GrantCompleted(p));
            p.bests.Add(new EventBest { eventId = "e3", bestTimeMs = 100 });
            Assert.IsTrue(RaceCups.GrantCompleted(p));
            Assert.IsTrue(Cosmetics.OwnsPaint(p.cosmetics, "sun"));
            Assert.IsFalse(RaceCups.GrantCompleted(p));
            Assert.AreEqual(1, p.cosmetics.Count);
        }

        [Test]
        public void CupRewardsCanBeRecoveredAfterCrossDeviceProgressMerge()
        {
            var a = new SaveData(); var b = new SaveData();
            a.progress.bests.Add(new EventBest { eventId = "e1", stars = 3, bestTimeMs = 100 });
            b.progress.bests.Add(new EventBest { eventId = "e2", stars = 3, bestTimeMs = 100 });
            b.progress.bests.Add(new EventBest { eventId = "e3", stars = 3, bestTimeMs = 100 });
            var merged = SaveMerge.Merge(a, b);
            Assert.IsTrue(RaceCups.GrantCompleted(merged.progress));
            Assert.IsTrue(Cosmetics.OwnsPaint(merged.progress.cosmetics, "sun"));
            Assert.AreEqual(0, merged.CoinsEarned, "cosmetic rewards never mint duplicate ledger coins");
        }
    }
}
