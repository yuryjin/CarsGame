using System.Collections.Generic;
using NUnit.Framework;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;

namespace RacingGame.Tests
{
    public class CarBodiesTests
    {
        [Test]
        public void TwentyBodiesFivePerClassWithUniqueIds()
        {
            Assert.AreEqual(20, CarBodies.All.Length);
            var ids = new HashSet<string>();
            foreach (var b in CarBodies.All) Assert.IsTrue(ids.Add(b.Id), "duplicate id " + b.Id);
            foreach (var cls in CarBodies.Classes) Assert.AreEqual(5, CarBodies.OfClass(cls).Count, cls.ToString());
            Assert.AreEqual(UnlockKind.Free, CarBodies.Default.Unlock, "the standard car is always owned");
            Assert.AreEqual(BodyClass.Sport, CarBodies.Default.Class);
        }

        [Test]
        public void StandardBodyIsTodaysCar()
        {
            var s = CarBodies.Default.Stats;
            foreach (float f in new[] { s.Mass, s.TopSpeed, s.Accel, s.Brake, s.Grip, s.Steer, s.Agility, s.Health, s.Offroad, s.TurretArc }) Assert.AreEqual(1f, f);
            Assert.AreEqual(BodyShape.Standard.Width, CarBodies.Default.Shape.Width);
            Assert.AreEqual(BodyShape.Standard.Length, CarBodies.Default.Shape.Length);
        }

        [Test]
        public void ClassesKeepTheirCharacter()
        {
            // Every buggy is lighter, quicker off the line and more fragile than every armored body and tank;
            // every tank is the heaviest, toughest and slowest on tarmac, and the best on grass after the buggies.
            foreach (var buggy in CarBodies.OfClass(BodyClass.Buggy))
            {
                Assert.Less(buggy.Stats.Mass, 1f, buggy.Id);
                Assert.Greater(buggy.Stats.Accel, 1.05f, buggy.Id);
                Assert.Less(buggy.Stats.Health, 0.85f, buggy.Id);
                Assert.Greater(buggy.Stats.Offroad, 1.5f, buggy.Id);
            }
            foreach (var armored in CarBodies.OfClass(BodyClass.Armored))
            {
                Assert.That(armored.Stats.Mass, Is.InRange(1.3f, 2f), armored.Id);
                Assert.That(armored.Stats.Health, Is.InRange(1.3f, 1.8f), armored.Id);
                Assert.Less(armored.Stats.Accel, 0.95f, armored.Id);
            }
            foreach (var tank in CarBodies.OfClass(BodyClass.Tank))
            {
                Assert.GreaterOrEqual(tank.Stats.Mass, 2f, tank.Id);
                Assert.GreaterOrEqual(tank.Stats.Health, 1.75f, tank.Id);
                Assert.Less(tank.Stats.TopSpeed, 0.9f, tank.Id);
                Assert.Greater(tank.Stats.TurretArc, 1.5f, tank.Id);
                // Tracks: the grass speed cap (70 km/h × offroad) is at or above the tank's own top speed (120 × top).
                Assert.GreaterOrEqual(70f * tank.Stats.Offroad, 120f * tank.Stats.TopSpeed, tank.Id);
            }
            foreach (var sport in CarBodies.OfClass(BodyClass.Sport))
            {
                Assert.That(sport.Stats.Mass, Is.InRange(0.9f, 1.1f), sport.Id);
                Assert.That(sport.Stats.Health, Is.InRange(0.85f, 1.1f), sport.Id);
            }
        }

        [Test]
        public void BodiesFitTheStartGrid()
        {
            // StartGrid: columns 2 × 2.3 m apart, rows 7 m; the wheels must sit under the hull.
            foreach (var b in CarBodies.All)
            {
                var s = b.Shape;
                Assert.Less(s.Width, 2.4f, b.Id);
                Assert.Less(s.Length, 5f, b.Id);
                Assert.GreaterOrEqual(s.Clearance, 0.08f, b.Id + ": the hull box must clear the 8 cm road step");
                Assert.Less(s.HalfBase * 2f, s.Length, b.Id);
                Assert.Less(s.HalfTrack, s.Width * 0.5f + 0.05f, b.Id);
            }
        }

        [Test]
        public void BuyGrantAndSelectFollowOwnership()
        {
            var owned = new List<string>();
            Assert.AreEqual(CarBodies.DefaultId, CarBodies.Selected(owned, "tortoise"), "an unowned selection falls back to the standard car");
            Assert.IsFalse(CarBodies.TryBuy(owned, "tortoise", 100, out int cost));
            Assert.AreEqual(1200, cost);
            Assert.IsTrue(CarBodies.TryBuy(owned, "tortoise", 5000, out cost));
            Assert.Contains("body:tortoise", owned);
            Assert.AreEqual("tortoise", CarBodies.Selected(owned, "tortoise"));
            Assert.IsFalse(CarBodies.TryBuy(owned, "tortoise", 5000, out _), "no double purchase");
            Assert.IsFalse(CarBodies.TryBuy(owned, "raider", 99999, out _), "rewarded bodies are not for coins");
            Assert.IsTrue(CarBodies.GrantRewarded(owned, "raider"));
            Assert.IsFalse(CarBodies.GrantRewarded(owned, "tortoise"));
            Assert.AreEqual(CarBodies.DefaultId, CarBodies.Selected(owned, "no-such-body"));
            Assert.AreEqual(CarBodies.DefaultId, CarBodies.Get("").Id);
        }

        [Test]
        public void SelectedBodySurvivesMergeAndOldSaves()
        {
            var a = new SaveData { deviceId = "a", updatedAtUnixMs = 1 };
            var b = new SaveData { deviceId = "b", updatedAtUnixMs = 2 };
            a.progress.cosmetics.Add("body:dune");
            b.progress.cosmetics.Add("body:grizzly");
            b.progress.body = "grizzly";
            var m = SaveMerge.Merge(a, b);
            Assert.Contains("body:dune", m.progress.cosmetics);
            Assert.Contains("body:grizzly", m.progress.cosmetics);
            Assert.AreEqual("grizzly", m.progress.body);

            var old = UnityEngine.JsonUtility.FromJson<SaveData>("{\"version\":4,\"progress\":{\"paint\":\"snow\"}}");
            Assert.AreEqual("", old.progress.body);
            Assert.AreEqual(CarBodies.DefaultId, CarBodies.Selected(old.progress.cosmetics, old.progress.body));
        }

        [Test]
        public void RivalBodiesFollowRoleAndAreStablePerSeed()
        {
            for (int i = 0; i < 7; i++)
            {
                var racer = CarBodies.ForRival(0, i, 1234);
                Assert.That(racer.Class, Is.EqualTo(BodyClass.Buggy).Or.EqualTo(BodyClass.Sport));
                var defender = CarBodies.ForRival(2, i, 1234);
                Assert.That(defender.Class, Is.EqualTo(BodyClass.Armored).Or.EqualTo(BodyClass.Tank));
                Assert.AreEqual(racer.Id, CarBodies.ForRival(0, i, 1234).Id);
            }
            var seen = new HashSet<string>();
            for (int seed = 0; seed < 50; seed++) for (int i = 0; i < 7; i++) for (int role = 0; role < 3; role++) seen.Add(CarBodies.ForRival(role, i, seed).Id);
            Assert.AreEqual(20, seen.Count, "every body shows up in some field");
        }

        [Test]
        public void RatingsSpanTheBar()
        {
            foreach (var b in CarBodies.All)
                foreach (float r in new[] { CarBodies.SpeedRating(b), CarBodies.AccelRating(b), CarBodies.HandlingRating(b), CarBodies.ArmorRating(b), CarBodies.OffroadRating(b) })
                    Assert.That(r, Is.InRange(0.1f, 1.001f), b.Id);
            Assert.Greater(CarBodies.ArmorRating(CarBodies.Get("mammoth")), CarBodies.ArmorRating(CarBodies.Get("flea")));
            Assert.Greater(CarBodies.SpeedRating(CarBodies.Get("baja")), CarBodies.SpeedRating(CarBodies.Get("mammoth")));
        }
    }
}
