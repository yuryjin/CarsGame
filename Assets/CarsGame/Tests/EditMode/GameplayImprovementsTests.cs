using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Combat;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public class GameplayImprovementsTests
    {
        [Test]
        public void LowSpeedSteeringMakesTightCornersReachableWithoutChangingFastTurns()
        {
            var go = new GameObject("SteeringModel");
            try
            {
                var car = go.AddComponent<CarController>();
                car.Body.inertiaTensor = new Vector3(2000, 2200, 2000);
                car.Body.angularDamping = 2.5f;
                car.lowSpeedSteeringBoost = 1f;
                float oldCorner = car.MaxSpeedForCurvature(0.15f);
                float fastSteer = car.SteerForCurvature(0.01f, 20f);
                car.lowSpeedSteeringBoost = 3f;
                Assert.Greater(car.MaxSpeedForCurvature(0.15f), oldCorner);
                Assert.AreEqual(fastSteer, car.SteerForCurvature(0.01f, 20f), 0.0001f);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PresetsKeepPurchasesAndNeverEquipUnownedWeapons()
        {
            var save = new SaveData();
            save.Local("test").coinsEarned = 900;
            save.progress.upgrades = new[] { 1, 2, 3 };
            foreach (CarBuild build in System.Enum.GetValues(typeof(CarBuild)))
            {
                PlayerProgression.SelectBuild(save.progress, build);
                Assert.AreEqual(0, save.progress.equippedPrimary);
                Assert.AreEqual(0, save.progress.equippedSecondary);
                Assert.AreEqual(900, save.Coins);
                CollectionAssert.AreEqual(new[] { 1, 2, 3 }, save.progress.upgrades);
            }
            save.progress.weapons = new[] { 1, 2, 3 };
            PlayerProgression.SelectBuild(save.progress, CarBuild.Firepower);
            Assert.AreEqual(1, save.progress.equippedPrimary);
            Assert.AreEqual(1, save.progress.equippedSecondary);
            PlayerProgression.SelectBuild(save.progress, CarBuild.Speed);
            Assert.AreEqual(0, save.progress.equippedPrimary);
            Assert.AreEqual(2, save.progress.equippedSecondary);
            Assert.Greater(BuildCatalog.For(CarBuild.Speed).MaxSpeedFactor, 1);
            Assert.Less(BuildCatalog.For(CarBuild.Speed).MaxHealthFactor, 1);
            Assert.Greater(BuildCatalog.For(CarBuild.Survival).MaxHealthFactor, 1);
            Assert.Less(BuildCatalog.For(CarBuild.Survival).TorqueFactor, 1);
            Assert.Greater(BuildCatalog.For(CarBuild.Firepower).DamageFactor, 1);
            Assert.Less(BuildCatalog.For(CarBuild.Firepower).MaxHealthFactor, 1);
        }

        [Test]
        public void PresetMigratesRoundTripsAndFollowsNewerCloudSave()
        {
            var old = SaveMigrator.FromJson("{\"version\":3,\"progress\":{\"upgrades\":[2,1,3],\"weapons\":[1,0,1]}}");
            Assert.AreEqual(0, old.progress.buildPreset);
            CollectionAssert.AreEqual(new[] { 2, 1, 3 }, old.progress.upgrades);
            PlayerProgression.SelectBuild(old.progress, CarBuild.Firepower);
            old.updatedAtUnixMs = 10;
            var restored = SaveMigrator.FromJson(SaveMigrator.ToJson(old));
            Assert.AreEqual(3, restored.progress.buildPreset);
            var newer = new SaveData { updatedAtUnixMs = 20 };
            newer.progress.buildPreset = 2;
            Assert.AreEqual(2, SaveMerge.Merge(restored, newer).progress.buildPreset);
            CollectionAssert.AreEqual(old.progress.upgrades, SaveMerge.Merge(restored, newer).progress.upgrades);
        }

        [Test]
        public void FinishGoalsRequireTheirActualConditionsAndACompletedRace()
        {
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            try
            {
                var entry = new RaceResultEntry { Finished = true, Position = 1, TotalTime = 100 };
                ev.finishGoal = FinishGoal.WinWithoutRepair;
                Assert.AreEqual(4, ev.StarMask(entry) & 4);
                entry.RepairsUsed = 1;
                Assert.AreEqual(0, ev.StarMask(entry) & 4);
                entry.RepairsUsed = 0; entry.Position = 2;
                Assert.AreEqual(0, ev.StarMask(entry) & 4);
                ev.finishGoal = FinishGoal.BeatTime; ev.targetSeconds = 100;
                Assert.AreEqual(4, ev.StarMask(entry) & 4);
                entry.TotalTime = 100.001;
                Assert.AreEqual(0, ev.StarMask(entry) & 4);
                entry.TotalTime = 90; entry.Finished = false;
                Assert.AreEqual(0, ev.StarMask(entry) & 4);
            }
            finally { Object.DestroyImmediate(ev); }
        }

        [Test]
        public void HitFeedbackDistinguishesShieldDamageFatalAndRejectedHits()
        {
            var go = new GameObject("FeedbackTest");
            try
            {
                var status = go.AddComponent<CarStatus>();
                var health = go.AddComponent<CarHealth>();
                health.SetMaxHealth(100, true);
                health.respawnOnDeath = false;
                int events = 0;
                HitResult last = default;
                health.HitResolved += (hit, result) => { events++; last = result; };
                status.ApplyStatus(StatusKind.Shield, 6f);
                health.ApplyHit(new HitInfo(null, 20));
                Assert.IsTrue(last.ShieldAbsorbed);
                Assert.AreEqual(100, health.Health);
                health.ApplyHit(new HitInfo(null, 20));
                Assert.IsFalse(last.ShieldAbsorbed);
                Assert.AreEqual(20, last.DamageDealt);
                health.Heal(10);
                Assert.AreEqual(1, health.RepairsUsed);
                health.Heal(100);
                health.Heal(10); // full HP is not another repair
                Assert.AreEqual(2, health.RepairsUsed);
                health.ApplyHit(new HitInfo(null, 200));
                Assert.IsTrue(last.Fatal);
                health.ApplyHit(new HitInfo(null, 20));
                Assert.AreEqual(3, events, "A wreck must not confirm another hit.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TacticalPickupsStayInTheirSectorAndOutsideBendsInBothDirections()
        {
            var go = new GameObject("PickupTrack");
            try
            {
                for (int i = 0; i < 64; i++)
                {
                    var point = new GameObject("point").transform;
                    point.SetParent(go.transform);
                    float a = i * Mathf.PI * 2f / 64;
                    point.position = new Vector3(Mathf.Sin(a) * 100, 0, Mathf.Cos(a) * 100);
                }
                var path = go.AddComponent<TrackPath>();
                foreach (bool reverse in new[] { false, true })
                {
                    path.reversed = reverse; path.Build();
                    for (int i = 0; i < 9; i++)
                    {
                        var kind = (PickupKind)(i % 4);
                        PickupSpawner.Placement(path, kind, i, 9, 60, 2.6f, out float d, out float lane);
                        float relative = path.Wrap(d - path.startLineDistance);
                        float sector = (path.Length - 60) / 9;
                        Assert.That(relative, Is.InRange(60 + sector * i, 60 + sector * (i + 1)));
                        Assert.LessOrEqual(Mathf.Abs(lane), path.halfWidth - 1.2f);
                        float turn = Vector3.SignedAngle(path.Tangent(d - 6), path.Tangent(d + 6), Vector3.up);
                        if (kind == PickupKind.Health) Assert.Less(turn * lane, 0, "Repair belongs on the outside of the bend.");
                    }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void RivalRolesEquipDistinctWeapons()
        {
            var go = new GameObject("RivalWeapons");
            var profile = ScriptableObject.CreateInstance<AiDriverProfile>();
            try
            {
                var weapons = go.AddComponent<CarWeapons>();
                profile.role = RivalRole.Racer; profile.Equip(weapons);
                Assert.AreEqual(SecondaryWeaponType.None, weapons.SecondaryType);
                Assert.AreEqual(PrimaryWeaponType.Cannon, weapons.PrimaryType);
                profile.role = RivalRole.Shooter; profile.Equip(weapons);
                Assert.AreEqual(PrimaryWeaponType.MachineGun, weapons.PrimaryType);
                profile.role = RivalRole.Defender; profile.Equip(weapons);
                Assert.AreEqual(SecondaryWeaponType.Mines, weapons.SecondaryType);
                Assert.AreEqual(3, weapons.SecondaryAmmo);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(profile); }
        }
    }
}
