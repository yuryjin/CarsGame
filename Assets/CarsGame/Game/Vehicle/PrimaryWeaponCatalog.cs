using RacingGame.Core.Combat;

namespace RacingGame
{
    public enum WeaponMode { Projectile, Melee, Beam, Cone, Pulse, Trap }

    public static class PrimaryWeaponCatalog
    {
        public const int Count = 50;
        public const int SlotsPerPage = 10;
        public const int PageCount = Count / SlotsPerPage;

        public readonly struct Profile
        {
            public readonly string NameKey;
            public readonly float Interval, Speed, Damage, Recoil, Reload, Heat, Spread, StatusSeconds;
            public readonly int Magazine, Pellets;
            public readonly ProjectileKind Kind;
            public readonly StatusKind Status;
            public readonly WeaponMode Mode;
            public readonly float Range, Arc, Impact;
            public readonly MineSkin Skin;
            public readonly int Ricochets;
            public readonly bool Homing;

            public Profile(string name, float interval, float speed, float damage, float recoil, int magazine,
                float reload, float heat, ProjectileKind kind = ProjectileKind.Shell, int pellets = 1,
                float spread = 0f, StatusKind status = StatusKind.None, float statusSeconds = 0f,
                WeaponMode mode = WeaponMode.Projectile, float range = 4f, float arc = 30f, float impact = 0f,
                MineSkin skin = MineSkin.Standard, int ricochets = 0, bool homing = false)
            {
                NameKey = name; Interval = interval; Speed = speed; Damage = damage; Recoil = recoil;
                Magazine = magazine; Reload = reload; Heat = heat; Kind = kind; Pellets = pellets;
                Spread = spread; Status = status; StatusSeconds = statusSeconds;
                Mode = mode; Range = range; Arc = arc; Impact = impact; Skin = skin;
                Ricochets = ricochets; Homing = homing;
            }
        }

        private static readonly Profile[] Profiles =
        {
            new Profile("garage.cannon",       0.4f,   70f, 25f, 1500f, 5, 4f,   15f),
            new Profile("garage.machinegun",   0.125f,120f,  8f,  300f, 0, 0f,  2.5f, ProjectileKind.MachineGun),
            new Profile("weapon.shotgun",      0.9f,   85f,  7f, 1800f, 4, 3f,   18f, ProjectileKind.MachineGun, 7, 18f),
            new Profile("weapon.twin",         0.7f,   80f, 18f, 1900f, 4, 3.5f, 18f, pellets: 2, spread: 3f),
            new Profile("weapon.autocannon",   0.22f, 100f, 12f,  600f, 0, 0f,    6f),
            new Profile("weapon.sniper",       1.4f,  220f, 60f, 2200f, 3, 4.5f, 25f),
            new Profile("weapon.freeze",       1.1f,   65f,  8f,  500f, 4, 4f,   16f, ProjectileKind.Freeze, status: StatusKind.Frozen, statusSeconds: 1.5f),
            new Profile("weapon.emp",          1.3f,   85f,  5f,  400f, 3, 4f,   20f, status: StatusKind.Jammed, statusSeconds: 2f),
            new Profile("weapon.spike",        0.65f, 110f, 14f,  700f, 6, 3f,   10f, status: StatusKind.Punctured, statusSeconds: 3f),
            new Profile("weapon.heavy",        1.8f,   55f, 85f, 3200f, 2, 5f,   32f),
            new Profile("weapon.boxer", 0.8f, 1f, 28f, 0f, 0, 0f, 12f, mode: WeaponMode.Melee, range: 6f, arc: 180f, impact: 3200f),
            new Profile("weapon.circular_saw", 0.2f, 1f, 9f, 0f, 0, 0f, 4f, mode: WeaponMode.Melee, range: 4.5f, arc: 70f, impact: 350f),
            new Profile("weapon.drill", 0.18f, 1f, 11f, 0f, 0, 0f, 5f, mode: WeaponMode.Melee, range: 5f, arc: 25f, impact: 500f),
            new Profile("weapon.sledgehammer", 1.4f, 1f, 55f, 0f, 0, 0f, 22f, mode: WeaponMode.Melee, range: 5.5f, arc: 110f, impact: 5500f),
            new Profile("weapon.battle_axe", 1.05f, 1f, 44f, 0f, 0, 0f, 18f, mode: WeaponMode.Melee, range: 5f, arc: 120f, impact: 2200f),
            new Profile("weapon.chainsaw", 0.16f, 1f, 8f, 0f, 0, 0f, 4f, mode: WeaponMode.Melee, range: 4.5f, arc: 45f, impact: 300f),
            new Profile("weapon.laser_saw", 0.22f, 1f, 13f, 0f, 0, 0f, 7f, mode: WeaponMode.Melee, range: 6f, arc: 80f, impact: 400f),
            new Profile("weapon.welder", 0.12f, 1f, 7f, 0f, 0, 0f, 5f, mode: WeaponMode.Beam, range: 7f, arc: 30f, impact: 150f),
            new Profile("weapon.spring_spear", 0.9f, 1f, 32f, 0f, 0, 0f, 12f, mode: WeaponMode.Melee, range: 7f, arc: 30f, impact: 2600f),
            new Profile("weapon.wrecking_ball", 1.6f, 1f, 60f, 0f, 0, 0f, 25f, mode: WeaponMode.Melee, range: 6.5f, arc: 180f, impact: 6000f),
            new Profile("weapon.flamethrower", 0.15f, 1f, 5f, 0f, 0, 0f, 4f, mode: WeaponMode.Cone, range: 11f, arc: 28f, impact: 100f),
            new Profile("weapon.tesla_coil", 0.8f, 1f, 18f, 0f, 0, 0f, 18f, status: StatusKind.Jammed, statusSeconds: 0.5f, mode: WeaponMode.Pulse, range: 9f, impact: 400f),
            new Profile("weapon.harpoon", 1.3f, 1f, 18f, 0f, 4, 3f, 14f, mode: WeaponMode.Beam, range: 24f, arc: 18f, impact: -4200f),
            new Profile("weapon.tractor_beam", 0.2f, 1f, 3f, 0f, 0, 0f, 5f, mode: WeaponMode.Beam, range: 18f, arc: 25f, impact: -1100f),
            new Profile("weapon.repulsor", 1.1f, 1f, 10f, 0f, 0, 0f, 20f, mode: WeaponMode.Pulse, range: 10f, impact: 6500f),
            new Profile("weapon.sonic_cannon", 0.9f, 1f, 24f, 0f, 0, 0f, 16f, mode: WeaponMode.Cone, range: 17f, arc: 38f, impact: 2400f),
            new Profile("weapon.plasma_cutter", 0.25f, 1f, 16f, 0f, 0, 0f, 8f, mode: WeaponMode.Beam, range: 14f, arc: 16f, impact: 300f),
            new Profile("weapon.acid_sprayer", 0.2f, 1f, 5f, 0f, 0, 0f, 4f, status: StatusKind.Slowed, statusSeconds: 0.6f, mode: WeaponMode.Cone, range: 12f, arc: 35f, impact: 100f),
            new Profile("weapon.cryo_jet", 0.3f, 1f, 4f, 0f, 0, 0f, 6f, status: StatusKind.Frozen, statusSeconds: 0.7f, mode: WeaponMode.Cone, range: 10f, arc: 30f, impact: 200f),
            new Profile("weapon.nail_fan", 0.65f, 100f, 5f, 700f, 6, 3f, 12f, ProjectileKind.MachineGun, pellets: 9, spread: 42f, status: StatusKind.Punctured, statusSeconds: 1f),
            new Profile("weapon.giant_banana", 1.4f, 1f, 25f, 0f, 3, 4f, 8f, mode: WeaponMode.Trap, skin: MineSkin.Banana),
            new Profile("weapon.rubber_chicken", 0.85f, 1f, 24f, 0f, 0, 0f, 10f, mode: WeaponMode.Melee, range: 6f, arc: 150f, impact: 3800f),
            new Profile("weapon.glove_launcher", 1f, 55f, 30f, 1000f, 4, 3f, 14f, impact: 6500f),
            new Profile("weapon.anvil_launcher", 1.8f, 35f, 75f, 2400f, 2, 5f, 28f, impact: 6000f),
            new Profile("weapon.bowling_ball", 1.2f, 45f, 38f, 1300f, 4, 4f, 16f, impact: 5000f),
            new Profile("weapon.disco_pulse", 1.5f, 1f, 12f, 0f, 0, 0f, 18f, status: StatusKind.Slowed, statusSeconds: 1.5f, mode: WeaponMode.Pulse, range: 12f, impact: 1800f),
            new Profile("weapon.confetti_cannon", 0.9f, 65f, 3f, 500f, 5, 3f, 10f, ProjectileKind.MachineGun, pellets: 12, spread: 50f),
            new Profile("weapon.giant_boot", 1.2f, 1f, 35f, 0f, 0, 0f, 16f, mode: WeaponMode.Melee, range: 6f, arc: 160f, impact: 7500f),
            new Profile("weapon.frying_pan", 0.75f, 1f, 25f, 0f, 0, 0f, 10f, mode: WeaponMode.Melee, range: 5f, arc: 180f, impact: 2800f),
            new Profile("weapon.plunger", 1.1f, 1f, 15f, 0f, 0, 0f, 12f, status: StatusKind.Slowed, statusSeconds: 1.2f, mode: WeaponMode.Melee, range: 7f, arc: 70f, impact: -3000f),
            new Profile("weapon.mine_dispenser", 1.4f, 1f, 25f, 0f, 3, 4f, 8f, mode: WeaponMode.Trap),
            new Profile("weapon.bear_trap", 1.6f, 1f, 30f, 0f, 3, 4f, 8f, status: StatusKind.Punctured, statusSeconds: 4f, mode: WeaponMode.Trap, skin: MineSkin.BearTrap, range: 3f),
            new Profile("weapon.glue_trap", 1.5f, 1f, 5f, 0f, 4, 4f, 6f, status: StatusKind.Slowed, statusSeconds: 4f, mode: WeaponMode.Trap, skin: MineSkin.Glue, range: 4.5f),
            new Profile("weapon.ice_trap", 1.7f, 1f, 8f, 0f, 3, 4f, 8f, status: StatusKind.Frozen, statusSeconds: 2f, mode: WeaponMode.Trap, skin: MineSkin.Ice, range: 4f),
            new Profile("weapon.emp_mine", 1.8f, 1f, 10f, 0f, 3, 4f, 10f, status: StatusKind.Jammed, statusSeconds: 3f, mode: WeaponMode.Trap, skin: MineSkin.Emp, range: 6f),
            new Profile("weapon.spring_trap", 1.8f, 1f, 15f, 0f, 3, 4f, 10f, mode: WeaponMode.Trap, skin: MineSkin.Spring, range: 4f, impact: 11000f),
            new Profile("weapon.cactus_mine", 1.5f, 1f, 22f, 0f, 4, 4f, 8f, status: StatusKind.Punctured, statusSeconds: 3.5f, mode: WeaponMode.Trap, skin: MineSkin.Cactus, range: 5f),
            new Profile("weapon.water_cannon", 0.18f, 1f, 3f, 0f, 0, 0f, 3f, mode: WeaponMode.Cone, range: 16f, arc: 15f, impact: 1500f),
            new Profile("weapon.ricochet_saw", 0.9f, 65f, 28f, 900f, 4, 3f, 14f, ricochets: 3),
            new Profile("weapon.homing_rocket", 1.6f, 45f, 48f, 1000f, 3, 4f, 22f, homing: true, impact: 4500f)
        };

        public static bool IsValid(PrimaryWeaponType type) => (int)type >= 0 && (int)type < Count;
        public static Profile Get(PrimaryWeaponType type) => Profiles[IsValid(type) ? (int)type : 0];
    }
}
