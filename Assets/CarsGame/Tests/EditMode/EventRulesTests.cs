using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class EventRulesTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private RaceEventDefinition previousEvent;
        private RaceEventDefinition testEvent;

        [SetUp]
        public void SetUp() { previousEvent = RaceSetup.Event; }

        [TearDown]
        public void TearDown()
        {
            RaceSetup.Event = previousEvent;
            if (testEvent != null) Object.DestroyImmediate(testEvent);
            foreach (var go in objects) Object.DestroyImmediate(go);
            objects.Clear();
        }

        private GameObject Car(string name)
        {
            var go = new GameObject(name);
            objects.Add(go);
            go.AddComponent<CarController>();
            return go;
        }

        private static void AddParticipant(RaceManager race, RaceParticipant participant)
        {
            var field = typeof(RaceManager).GetField("participants", BindingFlags.Instance | BindingFlags.NonPublic);
            ((List<RaceParticipant>)field.GetValue(race)).Add(participant);
        }

        [Test]
        public void MinesOnlyLocksCannonAndRefillsMineAmmo()
        {
            var raceObject = new GameObject("Race");
            objects.Add(raceObject);
            var race = raceObject.AddComponent<RaceManager>();
            var car = Car("Player");
            var participant = car.AddComponent<RaceParticipant>();
            var cannon = car.AddComponent<CarCannon>();
            var weapons = car.AddComponent<CarWeapons>();
            weapons.secondaryAmmo = 0;
            AddParticipant(race, participant);

            EventRules.Apply(RacingGame.Core.Daily.RaceTwist.MinesOnly, race);

            Assert.IsTrue(cannon.locked);
            Assert.AreEqual(SecondaryWeaponType.Mines, weapons.secondaryType);
            Assert.AreEqual(EventRules.MinesOnlyAmmo, weapons.secondaryAmmoMax);
            Assert.AreEqual(EventRules.MinesOnlyAmmo, weapons.secondaryAmmo);
            Assert.AreEqual(EventRules.MinesOnlyCooldown, weapons.secondaryCooldown);
        }

        [Test]
        public void ArenaAddsChargesOnlyToCarsWithoutASecondaryWeapon()
        {
            var ev = ScriptableObject.CreateInstance<RaceEventDefinition>();
            testEvent = ev;
            ev.mode = EventMode.Arena;
            RaceSetup.Event = ev;
            var raceObject = new GameObject("Race");
            objects.Add(raceObject);
            var race = raceObject.AddComponent<RaceManager>();
            var empty = Car("Empty").AddComponent<RaceParticipant>();
            var equipped = Car("Equipped").AddComponent<RaceParticipant>();
            empty.Weapons.secondaryType = SecondaryWeaponType.None;
            equipped.Weapons.secondaryType = SecondaryWeaponType.Mines;
            AddParticipant(race, empty);
            AddParticipant(race, equipped);

            EventRules.Apply(RacingGame.Core.Daily.RaceTwist.None, race);

            Assert.AreEqual(SecondaryWeaponType.RemoteCharge, empty.Weapons.secondaryType);
            Assert.AreEqual(EventRules.ArenaChargeAmmo, empty.Weapons.secondaryAmmo);
            Assert.AreEqual(EventRules.ArenaChargeCooldown, empty.Weapons.secondaryCooldown);
            Assert.AreEqual(SecondaryWeaponType.Mines, equipped.Weapons.secondaryType);
        }

        [Test]
        public void OneLifeDisablesRespawnForEveryParticipant()
        {
            var raceObject = new GameObject("Race");
            objects.Add(raceObject);
            var race = raceObject.AddComponent<RaceManager>();
            var participant = Car("Player").AddComponent<RaceParticipant>();
            var health = participant.gameObject.AddComponent<CarHealth>();
            AddParticipant(race, participant);

            EventRules.Apply(RacingGame.Core.Daily.RaceTwist.OneLife, race);

            Assert.IsFalse(health.respawnOnDeath);
        }

        [Test]
        public void PrimaryEnabledReflectsCannonLock()
        {
            Assert.IsFalse(EventRules.PrimaryEnabled(null));
            var car = Car("Car").AddComponent<CarController>();
            Assert.IsFalse(EventRules.PrimaryEnabled(car));
            var cannon = car.gameObject.AddComponent<CarCannon>();
            Assert.IsTrue(EventRules.PrimaryEnabled(car));
            cannon.locked = true;
            Assert.IsFalse(EventRules.PrimaryEnabled(car));
        }
    }
}
