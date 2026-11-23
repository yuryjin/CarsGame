using System;
using System.Collections.Generic;

namespace RacingGame.Core.Save
{
    [Serializable] public class WeaponPin
    {
        public int slot;
        public string weapon;
        public bool pinned;
        public long changedAt;

        public static bool Has(List<WeaponPin> pins, int slot, string weapon) =>
            pins?.Exists(x => x != null && x.slot == slot && x.weapon == weapon && x.pinned) == true;

        public static void Toggle(ProgressData progress, int slot, string weapon)
        {
            if (slot < 0 || slot > 1 || string.IsNullOrEmpty(weapon)) return;
            progress.weaponPins ??= new List<WeaponPin>();
            var pin = progress.weaponPins.Find(x => x != null && x.slot == slot && x.weapon == weapon);
            if (pin == null)
            {
                pin = new WeaponPin { slot = slot, weapon = weapon };
                progress.weaponPins.Add(pin);
            }
            pin.pinned = !pin.pinned;
            pin.changedAt = Math.Max(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), pin.changedAt + 1);
        }

        public static List<WeaponPin> Merge(List<WeaponPin> a, List<WeaponPin> b)
        {
            var result = new SortedDictionary<string, WeaponPin>(StringComparer.Ordinal);
            foreach (var list in new[] { a, b }) if (list != null) foreach (var pin in list)
            {
                if (pin == null || pin.slot < 0 || pin.slot > 1 || string.IsNullOrEmpty(pin.weapon)) continue;
                string key = pin.slot + "/" + pin.weapon;
                if (!result.TryGetValue(key, out var old) || pin.changedAt > old.changedAt || pin.changedAt == old.changedAt && !pin.pinned)
                    result[key] = new WeaponPin { slot = pin.slot, weapon = pin.weapon, pinned = pin.pinned, changedAt = pin.changedAt };
            }
            return new List<WeaponPin>(result.Values);
        }
    }
}
