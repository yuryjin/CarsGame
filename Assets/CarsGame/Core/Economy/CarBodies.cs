using System;
using System.Collections.Generic;

namespace RacingGame.Core.Economy
{
    /// <summary>Body archetype: each asks for its own driving style.</summary>
    public enum BodyClass { Buggy, Sport, Armored, Tank }

    /// <summary>Extra parts a body wears. Visual only; the handling lives in <see cref="BodyStats"/>.</summary>
    [Flags]
    public enum BodyKit
    {
        None = 0,
        RollCage = 1 << 0,
        LightBar = 1 << 1,
        SpareWheel = 1 << 2,
        Snorkel = 1 << 3,
        Wing = 1 << 4,
        BigWing = 1 << 5,
        Scoop = 1 << 6,
        RamBar = 1 << 7,
        SidePlates = 1 << 8,
        RoofRack = 1 << 9,
        Spikes = 1 << 10,
        SixWheels = 1 << 11,
        Skirts = 1 << 12,
        Stacks = 1 << 13,
        Antenna = 1 << 14,
        TwinBarrel = 1 << 15,
        ReactiveArmor = 1 << 16,
        Bed = 1 << 17,
        Fins = 1 << 18,
    }

    /// <summary>
    /// Handling and toughness as factors of the standard sports car (all 1 = today's car). Acceleration and
    /// yaw response are per unit of mass, so a heavier body is not automatically slower or lazier: that comes
    /// from <see cref="Accel"/> and <see cref="Agility"/>. The mass itself decides who wins a shove and how much
    /// a crash hurts (collision damage is impulse / mass).
    /// </summary>
    public readonly struct BodyStats
    {
        public readonly float Mass, TopSpeed, Accel, Brake, Grip, Steer, Agility, Health, Offroad, TurretArc;
        public BodyStats(float mass, float topSpeed, float accel, float brake, float grip, float steer, float agility, float health, float offroad, float turretArc = 1f)
        {
            Mass = mass; TopSpeed = topSpeed; Accel = accel; Brake = brake; Grip = grip; Steer = steer;
            Agility = agility; Health = health; Offroad = offroad; TurretArc = turretArc;
        }
        public static BodyStats Standard => new BodyStats(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// Physical size in metres, car space (x right, y up, z forward, ground at y = 0). The box is the hull
    /// collider, the wheels are the rolling contact spheres. The start grid columns are 4.6 m apart and its rows 7 m,
    /// so width stays under 2.4 m and length under 5 m.
    /// </summary>
    public readonly struct BodyShape
    {
        public readonly float Length, Width, Height, Clearance, WheelRadius, HalfBase, HalfTrack;
        public BodyShape(float length, float width, float height, float clearance, float wheelRadius, float halfBase, float halfTrack)
        {
            Length = length; Width = width; Height = height; Clearance = clearance;
            WheelRadius = wheelRadius; HalfBase = halfBase; HalfTrack = halfTrack;
        }
        /// <summary>The baked RaceCar prefab: box 1.85 × 0.70 × 4.20 from 0.10 m up, wheels r 0.34 at ±0.94 / ±1.25.</summary>
        public static BodyShape Standard => new BodyShape(4.20f, 1.85f, 0.70f, 0.10f, 0.34f, 1.25f, 0.94f);
    }

    public readonly struct BodyDef
    {
        public readonly string Id;
        public readonly BodyClass Class;
        public readonly BodyStats Stats;
        public readonly BodyShape Shape;
        public readonly BodyKit Kit;
        public readonly UnlockKind Unlock;
        public readonly int Cost;
        public BodyDef(string id, BodyClass cls, BodyStats stats, BodyShape shape, BodyKit kit, UnlockKind unlock, int cost)
        { Id = id; Class = cls; Stats = stats; Shape = shape; Kit = kit; Unlock = unlock; Cost = cost; }
        public string NameKey => "body." + Id;
        public string OwnedKey => "body:" + Id;
        public bool IsStandard => Id == CarBodies.DefaultId;
    }

    /// <summary>
    /// Twenty car bodies in four classes. Ids are stored in the save (selected body in <c>progress.body</c>,
    /// bought ones as "body:id" in <c>progress.cosmetics</c>), so they must never be renamed.
    /// Buggy: light, quick off the line, twitchy, laughs at grass, breaks easily.
    /// Sport: the reference car and its sidegrades.
    /// Armored: heavy, tough, wins shoves, brakes late at its peril.
    /// Tank: slow on tarmac but drives straight across grass, soaks up damage, turret sweeps wide.
    /// </summary>
    public static class CarBodies
    {
        public const string DefaultId = "classic";

        //                                  mass  top   accel brake grip  steer agil  hp    offrd turret
        public static readonly BodyDef[] All =
        {
            // --- Buggy: small hull, big wheels, high clearance.
            new BodyDef("dune", BodyClass.Buggy, new BodyStats(0.70f, 1.04f, 1.25f, 1.00f, 0.88f, 1.15f, 1.30f, 0.70f, 1.70f),
                new BodyShape(3.70f, 1.90f, 0.70f, 0.30f, 0.42f, 1.20f, 0.92f), BodyKit.RollCage | BodyKit.SpareWheel, UnlockKind.Coins, 500),
            new BodyDef("flea", BodyClass.Buggy, new BodyStats(0.60f, 1.00f, 1.40f, 1.05f, 0.85f, 1.25f, 1.45f, 0.60f, 1.80f),
                new BodyShape(3.20f, 1.80f, 0.65f, 0.30f, 0.40f, 1.02f, 0.88f), BodyKit.RollCage | BodyKit.LightBar, UnlockKind.Coins, 900),
            new BodyDef("scorpion", BodyClass.Buggy, new BodyStats(0.78f, 1.06f, 1.18f, 1.00f, 0.90f, 1.10f, 1.20f, 0.78f, 1.60f),
                new BodyShape(3.90f, 1.95f, 0.70f, 0.28f, 0.42f, 1.30f, 0.94f), BodyKit.RollCage | BodyKit.Stacks | BodyKit.Snorkel, UnlockKind.Coins, 1400),
            new BodyDef("sidewinder", BodyClass.Buggy, new BodyStats(0.72f, 1.08f, 1.20f, 0.95f, 0.82f, 1.20f, 1.35f, 0.68f, 1.60f),
                new BodyShape(3.80f, 2.00f, 0.60f, 0.26f, 0.40f, 1.25f, 0.97f), BodyKit.RollCage | BodyKit.Wing | BodyKit.Fins, UnlockKind.Rewarded, 0),
            new BodyDef("baja", BodyClass.Buggy, new BodyStats(0.85f, 1.10f, 1.10f, 1.00f, 0.92f, 1.05f, 1.10f, 0.80f, 2.00f),
                new BodyShape(4.30f, 2.05f, 0.80f, 0.32f, 0.44f, 1.45f, 0.98f), BodyKit.RollCage | BodyKit.LightBar | BodyKit.SpareWheel | BodyKit.Bed, UnlockKind.Coins, 2200),

            // --- Sport: the prefab's sleek car, re-proportioned.
            new BodyDef(DefaultId, BodyClass.Sport, BodyStats.Standard, BodyShape.Standard, BodyKit.None, UnlockKind.Free, 0),
            new BodyDef("arrow", BodyClass.Sport, new BodyStats(0.95f, 1.06f, 1.05f, 1.00f, 0.95f, 0.95f, 0.95f, 0.90f, 0.95f),
                new BodyShape(4.40f, 1.82f, 0.64f, 0.09f, 0.34f, 1.32f, 0.93f), BodyKit.BigWing, UnlockKind.Coins, 800),
            new BodyDef("viper", BodyClass.Sport, new BodyStats(0.92f, 1.02f, 1.10f, 1.05f, 1.08f, 1.05f, 1.10f, 0.88f, 0.95f),
                new BodyShape(4.00f, 1.95f, 0.66f, 0.09f, 0.34f, 1.18f, 0.99f), BodyKit.Wing | BodyKit.Scoop, UnlockKind.Coins, 1300),
            new BodyDef("comet", BodyClass.Sport, new BodyStats(1.00f, 1.10f, 1.00f, 0.95f, 0.93f, 0.92f, 0.90f, 0.92f, 0.90f),
                new BodyShape(4.50f, 1.84f, 0.62f, 0.09f, 0.34f, 1.36f, 0.93f), BodyKit.Fins, UnlockKind.Coins, 1900),
            new BodyDef("phantom", BodyClass.Sport, new BodyStats(1.05f, 1.04f, 1.04f, 1.00f, 1.04f, 1.00f, 1.00f, 1.05f, 1.00f),
                new BodyShape(4.35f, 1.90f, 0.74f, 0.10f, 0.35f, 1.30f, 0.96f), BodyKit.Stacks | BodyKit.Scoop, UnlockKind.Coins, 2600),

            // --- Armored: boxy hull, slit windows, bull bars.
            new BodyDef("bulwark", BodyClass.Armored, new BodyStats(1.55f, 0.94f, 0.82f, 0.90f, 1.08f, 0.92f, 0.85f, 1.50f, 1.10f),
                new BodyShape(4.40f, 2.10f, 1.05f, 0.16f, 0.40f, 1.40f, 0.98f), BodyKit.RamBar | BodyKit.SidePlates, UnlockKind.Coins, 800),
            new BodyDef("rhino", BodyClass.Armored, new BodyStats(1.75f, 0.92f, 0.78f, 0.90f, 1.10f, 0.90f, 0.80f, 1.60f, 1.10f),
                new BodyShape(4.50f, 2.15f, 1.00f, 0.16f, 0.40f, 1.40f, 1.00f), BodyKit.RamBar | BodyKit.Spikes | BodyKit.SidePlates, UnlockKind.Coins, 1500),
            new BodyDef("hauler", BodyClass.Armored, new BodyStats(1.65f, 0.95f, 0.80f, 0.90f, 1.05f, 0.88f, 0.80f, 1.70f, 1.05f),
                new BodyShape(4.70f, 2.10f, 1.25f, 0.18f, 0.40f, 1.55f, 0.98f), BodyKit.RoofRack | BodyKit.SpareWheel | BodyKit.Antenna, UnlockKind.Coins, 1800),
            new BodyDef("warden", BodyClass.Armored, new BodyStats(1.85f, 0.93f, 0.76f, 0.90f, 1.14f, 0.90f, 0.82f, 1.75f, 1.30f),
                new BodyShape(4.80f, 2.20f, 1.10f, 0.20f, 0.42f, 1.65f, 1.02f), BodyKit.SixWheels | BodyKit.RamBar | BodyKit.Antenna, UnlockKind.Coins, 2400),
            new BodyDef("raider", BodyClass.Armored, new BodyStats(1.40f, 0.97f, 0.90f, 0.95f, 1.02f, 0.95f, 0.90f, 1.35f, 1.15f),
                new BodyShape(4.30f, 2.05f, 0.95f, 0.16f, 0.40f, 1.38f, 0.97f), BodyKit.RamBar | BodyKit.LightBar | BodyKit.Snorkel, UnlockKind.Rewarded, 0),

            // --- Tank: tracks, low hull, a real turret that sweeps twice as wide.
            new BodyDef("tortoise", BodyClass.Tank, new BodyStats(2.30f, 0.80f, 0.70f, 1.20f, 1.35f, 1.25f, 0.90f, 2.00f, 1.50f, 2.0f),
                new BodyShape(4.40f, 2.25f, 0.85f, 0.14f, 0.30f, 1.60f, 0.86f), BodyKit.None, UnlockKind.Coins, 1200),
            new BodyDef("grizzly", BodyClass.Tank, new BodyStats(2.50f, 0.82f, 0.68f, 1.20f, 1.35f, 1.20f, 0.88f, 2.20f, 1.50f, 2.0f),
                new BodyShape(4.60f, 2.30f, 0.90f, 0.14f, 0.30f, 1.70f, 0.88f), BodyKit.Skirts | BodyKit.Antenna, UnlockKind.Coins, 2000),
            new BodyDef("mammoth", BodyClass.Tank, new BodyStats(2.80f, 0.76f, 0.62f, 1.15f, 1.40f, 1.20f, 0.82f, 2.50f, 1.40f, 1.8f),
                new BodyShape(4.80f, 2.35f, 0.95f, 0.14f, 0.30f, 1.80f, 0.90f), BodyKit.TwinBarrel | BodyKit.Skirts | BodyKit.Stacks, UnlockKind.Coins, 3000),
            new BodyDef("lynx", BodyClass.Tank, new BodyStats(2.00f, 0.86f, 0.78f, 1.20f, 1.30f, 1.30f, 1.00f, 1.75f, 1.60f, 2.2f),
                new BodyShape(4.30f, 2.20f, 0.75f, 0.14f, 0.30f, 1.55f, 0.85f), BodyKit.Antenna, UnlockKind.Coins, 2200),
            new BodyDef("titan", BodyClass.Tank, new BodyStats(2.70f, 0.78f, 0.64f, 1.15f, 1.38f, 1.20f, 0.85f, 2.35f, 1.45f, 2.5f),
                new BodyShape(4.90f, 2.35f, 0.95f, 0.14f, 0.30f, 1.85f, 0.90f), BodyKit.ReactiveArmor | BodyKit.Skirts | BodyKit.Antenna, UnlockKind.Coins, 3500),
        };

        public static readonly BodyClass[] Classes = { BodyClass.Buggy, BodyClass.Sport, BodyClass.Armored, BodyClass.Tank };

        public static int Index(string id) { for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return i; return -1; }

        /// <summary>Unknown or empty ids get the standard car.</summary>
        public static BodyDef Get(string id) { int i = Index(string.IsNullOrEmpty(id) ? DefaultId : id); return i >= 0 ? All[i] : All[Index(DefaultId)]; }

        public static BodyDef Default => Get(DefaultId);

        public static List<BodyDef> OfClass(BodyClass cls)
        {
            var list = new List<BodyDef>(5);
            foreach (var b in All) if (b.Class == cls) list.Add(b);
            return list;
        }

        public static string ClassKey(BodyClass cls) => "body.class." + cls.ToString().ToLowerInvariant();

        public static bool Owns(IList<string> owned, string id)
        {
            int i = Index(id);
            return i >= 0 && Cosmetics.Owns(owned, All[i].OwnedKey, All[i].Unlock);
        }

        /// <summary>The body to race with: the selection when it is owned, else the standard car (a stale id from another device).</summary>
        public static string Selected(IList<string> owned, string selected) =>
            !string.IsNullOrEmpty(selected) && Owns(owned, selected) ? selected : DefaultId;

        /// <summary>Buy with coins; adds "body:id". False when not for coins, already owned or too expensive.</summary>
        public static bool TryBuy(IList<string> owned, string id, long coins, out int cost)
        {
            cost = 0;
            int i = Index(id);
            if (i < 0 || owned == null) return false;
            cost = All[i].Cost;
            if (All[i].Unlock != UnlockKind.Coins || owned.Contains(All[i].OwnedKey) || coins < cost) return false;
            owned.Add(All[i].OwnedKey);
            return true;
        }

        /// <summary>Grant after a watched rewarded ad; false when the body is not a rewarded one or already owned.</summary>
        public static bool GrantRewarded(IList<string> owned, string id) => GrantRewardBody(owned, id);

        public static string GrantChallenge(IList<string> owned, BodyClass body, int shortcuts, int rams, bool finished)
        {
            if (!finished) return null;
            string id = body == BodyClass.Buggy && shortcuts > 0 ? "sidewinder" : body == BodyClass.Armored && rams > 0 ? "raider" : null;
            return id != null && GrantRewardBody(owned, id) ? id : null;
        }

        private static bool GrantRewardBody(IList<string> owned, string id)
        {
            int i = Index(id);
            if (i < 0 || owned == null || All[i].Unlock != UnlockKind.Rewarded || Owns(owned, id)) return false;
            owned.Add(All[i].OwnedKey);
            return true;
        }

        /// <summary>
        /// A rival's body: the class follows the driver's role (racers take light cars, shooters sport or armored,
        /// defenders armored or tanks), the variant the race seed, so one event always shows the same field.
        /// </summary>
        public static BodyDef ForRival(int role, int aiIndex, int seed)
        {
            uint h = unchecked((uint)(seed * 73856093) ^ (uint)((aiIndex + 1) * 19349663));
            h ^= h >> 13; h = unchecked(h * 0x5bd1e995u); h ^= h >> 15;
            BodyClass a, b;
            switch (role)
            {
                case 1: a = BodyClass.Sport; b = BodyClass.Armored; break;     // shooter
                case 2: a = BodyClass.Armored; b = BodyClass.Tank; break;      // defender
                default: a = BodyClass.Buggy; b = BodyClass.Sport; break;      // racer
            }
            var list = OfClass((h & 1) == 0 ? a : b);
            return list[(int)((h >> 1) % (uint)list.Count)];
        }

        // ---- 0..1 ratings for the garage bars (min..max over all bodies).

        public static float SpeedRating(in BodyDef b) => Norm(b.Stats.TopSpeed, s => s.TopSpeed);
        public static float AccelRating(in BodyDef b) => Norm(b.Stats.Accel, s => s.Accel);
        public static float HandlingRating(in BodyDef b) => Norm(Handling(b.Stats), Handling);
        public static float ArmorRating(in BodyDef b) => Norm(b.Stats.Health, s => s.Health);
        public static float OffroadRating(in BodyDef b) => Norm(b.Stats.Offroad, s => s.Offroad);

        private static float Handling(BodyStats s) => (float)Math.Pow(s.Grip * s.Steer * s.Agility, 1.0 / 3.0);

        private static float Norm(float v, Func<BodyStats, float> pick)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var b in All) { float x = pick(b.Stats); if (x < lo) lo = x; if (x > hi) hi = x; }
            // A floor so the weakest body still shows a sliver of a bar.
            return hi - lo < 1e-5f ? 1f : 0.12f + 0.88f * (v - lo) / (hi - lo);
        }
    }
}
