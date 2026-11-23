using System.Globalization;
using RacingGame.Core.Combat;
using RacingGame.Core.Save;
using RacingGame.Core.Economy;

namespace RacingGame
{
    public enum ArsenalGroup { All, Ranged, Close, Control, Traps, Pinned }

    /// <summary>Player-facing guidance derived from the same profiles used by the attacks.</summary>
    public static class WeaponGuide
    {
        public static ArsenalGroup Group(PrimaryWeaponCatalog.Profile p)
        {
            if (p.Mode == WeaponMode.Trap) return ArsenalGroup.Traps;
            if (p.Status != StatusKind.None || p.Impact < 0f) return ArsenalGroup.Control;
            return p.Mode == WeaponMode.Projectile ? ArsenalGroup.Ranged : ArsenalGroup.Close;
        }

        public static string RoleKey(PrimaryWeaponCatalog.Profile p)
        {
            if (p.Mode == WeaponMode.Trap) return "weapon.role.trap";
            if (p.Homing) return "weapon.role.homing";
            if (p.Ricochets > 0) return "weapon.role.ricochet";
            if (p.Impact < 0f) return "weapon.role.pull";
            if (p.Mode == WeaponMode.Projectile)
            {
                if (p.Pellets > 1) return p.Spread >= 10f ? "weapon.role.spread" : "weapon.role.volley";
                if (p.Interval < 0.3f) return "weapon.role.rapid";
                if (p.Damage >= 50f && p.Interval >= 1f) return "weapon.role.heavy";
            }
            return "weapon.role." + p.Mode.ToString().ToLowerInvariant();
        }

        public static string Damage(PrimaryWeaponCatalog.Profile p, float factor = 1f) => p.Pellets > 1
            ? Number(p.Damage * factor) + " × " + p.Pellets : Number(p.Damage * factor);

        private static string Number(float n) => n.ToString("0.##", CultureInfo.InvariantCulture);

        public static string Stats(PrimaryWeaponType type, ProgressData progress)
        {
            var p = PrimaryWeaponCatalog.Get(type);
            var tuning = PlayerProgression.GunFactors(UpgradeCatalog.For(progress.upgrades), BuildCatalog.For((CarBuild)progress.buildPreset));
            float seconds = SimulationDriver.SecondsToTicks(p.Interval * tuning.interval) * SimulationDriver.FixedStep;
            int mgLevel = WeaponCatalog.Level(progress.weapons, (int)WeaponKind.MachineGun);
            var heat = CarWeapons.HeatProfile(type, WeaponCatalog.For(mgLevel).HeatFactor);
            string text = Loc.Format("loadout.stats", "damage", Damage(p, tuning.damage), "interval", Number(seconds));
            text += "\n" + (p.Magazine > 0
                ? Loc.Format("loadout.magazine", "n", p.Magazine, "seconds", Number(p.Reload)) : Loc.Get("loadout.heat_only"));
            text += "\n" + Loc.Format("arsenal.thermal", "shot", Number(heat.perShot), "cool", Number(heat.cooling));
            if (p.Mode != WeaponMode.Projectile)
                text += "\n" + Loc.Format(p.Mode == WeaponMode.Trap ? "loadout.radius" : "loadout.range", "n", Number(p.Range));
            if (p.Status != StatusKind.None)
                text += "\n" + Loc.Format("loadout.effect", "effect", Loc.Get("hud.status." + p.Status.ToString().ToLowerInvariant()),
                    "seconds", Number(p.StatusSeconds));
            return text;
        }
    }
}
