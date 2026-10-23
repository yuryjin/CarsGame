using NUnit.Framework;

namespace RacingGame.Tests
{
    public sealed class WeaponGuideTests
    {
        [TestCase(PrimaryWeaponType.Shotgun, ArsenalGroup.Ranged, "spread")]
        [TestCase(PrimaryWeaponType.TwinCannon, ArsenalGroup.Ranged, "volley")]
        [TestCase(PrimaryWeaponType.MachineGun, ArsenalGroup.Ranged, "rapid")]
        [TestCase(PrimaryWeaponType.HeavyCannon, ArsenalGroup.Ranged, "heavy")]
        [TestCase(PrimaryWeaponType.HomingRocket, ArsenalGroup.Ranged, "homing")]
        [TestCase(PrimaryWeaponType.RicochetSaw, ArsenalGroup.Ranged, "ricochet")]
        [TestCase(PrimaryWeaponType.FreezeGun, ArsenalGroup.Control, "projectile")]
        [TestCase(PrimaryWeaponType.Harpoon, ArsenalGroup.Control, "pull")]
        [TestCase(PrimaryWeaponType.Chainsaw, ArsenalGroup.Close, "melee")]
        [TestCase(PrimaryWeaponType.IceTrap, ArsenalGroup.Traps, "trap")]
        public void GuidanceMatchesDistinctCombatBehaviors(PrimaryWeaponType type, ArsenalGroup group, string role)
        {
            var p = PrimaryWeaponCatalog.Get(type);
            Assert.AreEqual(group, WeaponGuide.Group(p));
            Assert.AreEqual("weapon.role." + role, WeaponGuide.RoleKey(p));
        }

        [Test]
        public void ShotgunDamageShowsEachPelletInsteadOfImplyingSevenDamageForTheWholeShot()
        {
            Assert.AreEqual("7 × 7", WeaponGuide.Damage(PrimaryWeaponCatalog.Get(PrimaryWeaponType.Shotgun)));
            Assert.AreEqual("25", WeaponGuide.Damage(PrimaryWeaponCatalog.Get(PrimaryWeaponType.Cannon)));
        }

        [Test]
        public void EveryWeaponBelongsToOneNonemptyPlayerCategory()
        {
            var counts = new int[5];
            for (int i = 0; i < PrimaryWeaponCatalog.Count; i++)
            {
                var group = WeaponGuide.Group(PrimaryWeaponCatalog.Get((PrimaryWeaponType)i));
                Assert.That((int)group, Is.InRange(1, 4));
                counts[(int)group]++;
            }
            for (int i = 1; i < counts.Length; i++) Assert.Greater(counts[i], 0);
        }
    }
}
