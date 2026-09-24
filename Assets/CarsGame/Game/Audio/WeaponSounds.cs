using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Fire and melee-hit sounds per primary weapon. Weapons share a handful of families (cannon,
    /// rapid fire, laser, zap, spray, swing, saw, trap…) with their own pitch, so fifty weapons
    /// need about twenty clips. Unknown or new weapons fall back by <see cref="WeaponMode"/>.
    /// </summary>
    public static class WeaponSounds
    {
        public readonly struct Voice
        {
            public readonly string Clip;
            public readonly float Volume, Pitch;
            public Voice(string clip, float volume = 0.9f, float pitch = 1f) { Clip = clip; Volume = volume; Pitch = pitch; }
        }

        public static Voice FireVoice(PrimaryWeaponType type)
        {
            switch (type)
            {
                case PrimaryWeaponType.Cannon: return new Voice("fire_cannon");
                case PrimaryWeaponType.TwinCannon: return new Voice("fire_cannon", 0.85f, 1.12f);
                case PrimaryWeaponType.HeavyCannon: return new Voice("fire_heavy", 1f);
                case PrimaryWeaponType.MachineGun: return new Voice("fire_rapid", 0.6f);
                case PrimaryWeaponType.Autocannon: return new Voice("fire_auto", 0.75f);
                case PrimaryWeaponType.SpikeGun: return new Voice("fire_pistol", 0.8f, 0.9f);
                case PrimaryWeaponType.NailFan: return new Voice("fire_pistol", 0.6f, 1.3f);
                case PrimaryWeaponType.Shotgun: return new Voice("fire_shotgun");
                case PrimaryWeaponType.Sniper: return new Voice("fire_sniper", 1f);
                case PrimaryWeaponType.FreezeGun:
                case PrimaryWeaponType.CryoJet:
                case PrimaryWeaponType.IceTrap: return new Voice("fire_freeze", 0.75f);
                case PrimaryWeaponType.EmpGun:
                case PrimaryWeaponType.TeslaCoil:
                case PrimaryWeaponType.EmpMine: return new Voice("fire_zap", 0.7f);
                case PrimaryWeaponType.LaserSaw: return new Voice("fire_laser", 0.7f, 0.8f);
                case PrimaryWeaponType.PlasmaCutter: return new Voice("fire_laser", 0.75f);
                case PrimaryWeaponType.Welder: return new Voice("fire_laser", 0.6f, 1.3f);
                case PrimaryWeaponType.SonicCannon: return new Voice("fire_pulse", 0.85f, 0.8f);
                case PrimaryWeaponType.Repulsor: return new Voice("fire_pulse", 0.8f);
                case PrimaryWeaponType.TractorBeam: return new Voice("fire_pulse", 0.6f, 0.7f);
                case PrimaryWeaponType.DiscoPulse: return new Voice("fire_pulse", 0.75f, 1.3f);
                case PrimaryWeaponType.Flamethrower: return new Voice("fire_spray", 0.8f, 0.9f);
                case PrimaryWeaponType.AcidSprayer: return new Voice("fire_spray", 0.7f, 1.2f);
                case PrimaryWeaponType.WaterCannon: return new Voice("fire_spray", 0.75f, 1.4f);
                case PrimaryWeaponType.HomingRocket: return new Voice("fire_rocket", 0.85f);
                case PrimaryWeaponType.Harpoon: return new Voice("fire_launch", 0.8f, 0.85f);
                case PrimaryWeaponType.GloveLauncher: return new Voice("fire_launch", 0.8f, 1.1f);
                case PrimaryWeaponType.AnvilLauncher: return new Voice("fire_launch", 0.9f, 0.75f);
                case PrimaryWeaponType.BowlingBall: return new Voice("fire_launch", 0.85f, 0.9f);
                case PrimaryWeaponType.GiantBanana: return new Voice("fire_launch", 0.7f, 1.3f);
                case PrimaryWeaponType.RubberChicken: return new Voice("fire_launch", 0.7f, 1.8f);
                case PrimaryWeaponType.ConfettiCannon: return new Voice("fire_launch", 0.75f, 1.5f);
                case PrimaryWeaponType.CircularSaw:
                case PrimaryWeaponType.RicochetSaw: return new Voice("fire_saw", 0.65f, 1.2f);
                case PrimaryWeaponType.Chainsaw: return new Voice("fire_saw", 0.7f, 0.85f);
                case PrimaryWeaponType.Drill: return new Voice("fire_saw", 0.65f, 1.5f);
                case PrimaryWeaponType.Boxer:
                case PrimaryWeaponType.Plunger: return new Voice("fire_swing", 0.6f, 1.2f);
                case PrimaryWeaponType.Sledgehammer:
                case PrimaryWeaponType.WreckingBall:
                case PrimaryWeaponType.GiantBoot: return new Voice("fire_swing", 0.75f, 0.75f);
                case PrimaryWeaponType.BattleAxe:
                case PrimaryWeaponType.SpringSpear:
                case PrimaryWeaponType.FryingPan: return new Voice("fire_swing", 0.65f);
                case PrimaryWeaponType.MineDispenser:
                case PrimaryWeaponType.BearTrap:
                case PrimaryWeaponType.GlueTrap:
                case PrimaryWeaponType.SpringTrap:
                case PrimaryWeaponType.CactusMine: return new Voice("fire_trap", 0.8f);
            }
            return PrimaryWeaponCatalog.IsValid(type) ? ByMode(PrimaryWeaponCatalog.Get(type).Mode) : new Voice("fire_cannon");
        }

        public static Voice ByMode(WeaponMode mode)
        {
            switch (mode)
            {
                case WeaponMode.Melee: return new Voice("fire_swing", 0.65f);
                case WeaponMode.Beam: return new Voice("fire_laser", 0.7f);
                case WeaponMode.Cone: return new Voice("fire_spray", 0.75f);
                case WeaponMode.Pulse: return new Voice("fire_pulse", 0.8f);
                case WeaponMode.Trap: return new Voice("fire_trap", 0.8f);
                default: return new Voice("fire_cannon");
            }
        }

        public static Voice MeleeHitVoice(PrimaryWeaponType type)
        {
            switch (type)
            {
                case PrimaryWeaponType.FryingPan: return new Voice("hit_pan", 0.8f);
                case PrimaryWeaponType.RubberChicken: return new Voice("hit_punch", 0.6f, 1.8f);
                case PrimaryWeaponType.CircularSaw:
                case PrimaryWeaponType.Chainsaw:
                case PrimaryWeaponType.Drill:
                case PrimaryWeaponType.LaserSaw: return new Voice("hit_metal_1", 0.7f, 1.2f);
                default: return new Voice("hit_punch", 0.7f);
            }
        }

        /// <summary>The shot sound; <paramref name="tick"/> varies the pitch a little so a burst is not one clip on repeat.</summary>
        public static void Fire(PrimaryWeaponType type, Vector3 position, int tick) => Play(FireVoice(type), position, tick, SfxSynth.Shot);

        public static void MeleeHit(PrimaryWeaponType type, Vector3 position) => Play(MeleeHitVoice(type), position, 0, SfxSynth.HitMetal);

        private static void Play(Voice v, Vector3 position, int tick, AudioClip fallback)
        {
            var sfx = SfxPlayer.Current ?? SfxPlayer.Instance;
            if (sfx == null) return;
            float jitter = 0.96f + 0.04f * (((tick % 3) + 3) % 3);
            sfx.Play(SfxLibrary.Get(v.Clip) ?? fallback, position, v.Volume, v.Pitch * jitter);
        }
    }
}
