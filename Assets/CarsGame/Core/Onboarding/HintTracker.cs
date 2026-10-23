using System;

namespace RacingGame.Core.Onboarding
{
    /// <summary>Situations that deserve a one-time tip the first time they happen.</summary>
    public enum Hint { Grass = 0, UnderFire = 1, AimFire = 2, PickupHealth = 3, PickupAmmo = 4, PickupNitro = 5,
        Drift = 6, Shortcut = 7, WallRide = 8, DriftSuccess = 9, ShortcutSuccess = 10, CeilingSuccess = 11 }

    /// <summary>
    /// Which contextual tips a player has already seen (a bit mask kept in the save) and pacing
    /// between them, so a busy first lap does not stack five tips. Pure: the caller passes the clock.
    /// A tip that arrives during the cooldown is not consumed and can fire the next time it happens.
    /// </summary>
    public sealed class HintTracker
    {
        public const float DefaultCooldown = 6f;
        public int Mask { get; private set; }
        public float Cooldown { get; }
        private float lastShown = float.NegativeInfinity;

        public HintTracker(int seenMask, float cooldownSeconds = DefaultCooldown)
        {
            Mask = seenMask;
            Cooldown = Math.Max(0f, cooldownSeconds);
        }

        public bool Seen(Hint hint) => (Mask & Bit(hint)) != 0;

        /// <summary>True (and marks it seen) if the tip may be shown now.</summary>
        public bool TryShow(Hint hint, float now)
        {
            if (Seen(hint) || now - lastShown < Cooldown) return false;
            Mask |= Bit(hint);
            lastShown = now;
            return true;
        }

        private static int Bit(Hint hint) => 1 << (int)hint;
    }
}
