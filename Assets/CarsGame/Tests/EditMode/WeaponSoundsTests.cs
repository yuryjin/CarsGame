using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RacingGame.Tests
{
    public class WeaponSoundsTests
    {
        private static bool ClipFileExists(string name) =>
            File.Exists(Path.Combine(Application.dataPath, "CarsGame", "Resources", "Sfx", name + ".ogg"));

        [Test]
        public void EveryWeaponFiresARealClip()
        {
            foreach (PrimaryWeaponType t in Enum.GetValues(typeof(PrimaryWeaponType)))
            {
                var v = WeaponSounds.FireVoice(t);
                Assert.IsTrue(ClipFileExists(v.Clip), t + " → " + v.Clip);
                Assert.Greater(v.Volume, 0f, t.ToString());
                Assert.That(v.Pitch, Is.InRange(0.5f, 2f), t.ToString());
            }
        }

        [Test]
        public void EveryMeleeHitHasARealClip()
        {
            foreach (PrimaryWeaponType t in Enum.GetValues(typeof(PrimaryWeaponType)))
                Assert.IsTrue(ClipFileExists(WeaponSounds.MeleeHitVoice(t).Clip), t.ToString());
            foreach (WeaponMode m in Enum.GetValues(typeof(WeaponMode)))
                Assert.IsTrue(ClipFileExists(WeaponSounds.ByMode(m).Clip), m.ToString());
        }

        [Test]
        public void WeaponFamiliesSoundDifferent()
        {
            Assert.AreNotEqual(WeaponSounds.FireVoice(PrimaryWeaponType.Cannon).Clip, WeaponSounds.FireVoice(PrimaryWeaponType.MachineGun).Clip);
            Assert.AreNotEqual(WeaponSounds.FireVoice(PrimaryWeaponType.Flamethrower).Clip, WeaponSounds.FireVoice(PrimaryWeaponType.Sniper).Clip);
            Assert.AreEqual("hit_pan", WeaponSounds.MeleeHitVoice(PrimaryWeaponType.FryingPan).Clip);
        }

        [Test]
        public void LibraryLoadsRecordedClipsAndMissesAreNull()
        {
            foreach (var name in new[] { "engine_loop", "explosion", "fire_cannon", "hit_metal_0", "hit_metal_1", "hit_metal_2", "hit_heavy", "nitro", "shield_break", "crash_light", "crash_heavy" })
                Assert.IsNotNull(SfxLibrary.Get(name), name);
            Assert.IsNull(SfxLibrary.Get("no_such_clip"));
            Assert.IsNull(SfxLibrary.Get(""));
        }
    }
}
