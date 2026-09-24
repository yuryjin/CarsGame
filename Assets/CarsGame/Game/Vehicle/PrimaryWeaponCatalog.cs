using RacingGame.Core.Combat;

namespace RacingGame
{
    public static class PrimaryWeaponCatalog
    {
        public const int Count = 10;

        public readonly struct Profile
        {
            public readonly string NameKey;
            public readonly float Interval, Speed, Damage, Recoil, Reload, Heat, Spread, StatusSeconds;
            public readonly int Magazine, Pellets;
            public readonly ProjectileKind Kind;
            public readonly StatusKind Status;

            public Profile(string name, float interval, float speed, float damage, float recoil, int magazine,
                float reload, float heat, ProjectileKind kind = ProjectileKind.Shell, int pellets = 1,
                float spread = 0f, StatusKind status = StatusKind.None, float statusSeconds = 0f)
            {
                NameKey = name; Interval = interval; Speed = speed; Damage = damage; Recoil = recoil;
                Magazine = magazine; Reload = reload; Heat = heat; Kind = kind; Pellets = pellets;
                Spread = spread; Status = status; StatusSeconds = statusSeconds;
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
            new Profile("weapon.heavy",        1.8f,   55f, 85f, 3200f, 2, 5f,   32f)
        };

        public static bool IsValid(PrimaryWeaponType type) => (int)type >= 0 && (int)type < Count;
        public static Profile Get(PrimaryWeaponType type) => Profiles[IsValid(type) ? (int)type : 0];
    }
}
