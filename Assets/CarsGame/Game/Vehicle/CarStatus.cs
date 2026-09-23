using System;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    /// <summary>
    /// Tracks and updates active status effects (Frozen, Jammed, Punctured, Shield) in simulation ticks.
    /// Computes dominant physics modifiers (SpeedMultiplier, GripMultiplier) without compounding debuffs.
    /// </summary>
    [DisallowMultipleComponent]
    public class CarStatus : MonoBehaviour, ISimulated
    {
        [Header("Frozen Settings")]
        [Tooltip("Speed multiplier applied when Frozen is active.")]
        public float frozenSpeedMultiplier = 0.4f;
        [Tooltip("Grip multiplier applied when Frozen is active.")]
        public float frozenGripMultiplier = 0.4f;
        [Tooltip("Seconds of immunity against Frozen after thawing.")]
        public float frozenImmunitySeconds = 1.5f;

        [Header("Punctured Settings")]
        [Tooltip("Grip multiplier applied when Punctured is active.")]
        public float puncturedGripMultiplier = 0.6f;

        [Header("Slowed Settings")]
        [Tooltip("Grip multiplier applied when Slowed (e.g. oil slick) is active.")]
        public float slowedGripMultiplier = 0.4f;

        public event Action<StatusKind, bool> OnStatusChanged;
        public event Action OnShieldConsumed;

        private int currentTick = 0;
        private int frozenUntilTick = -1;
        private int frozenImmunityUntilTick = -1;
        private int jammedUntilTick = -1;
        private int puncturedUntilTick = -1;
        private int shieldUntilTick = -1;
        private int slowedUntilTick = -1;

        private CarController controller;

        private int CurrentTick => SimulationDriver.Instance != null ? Mathf.Max(currentTick, SimulationDriver.Instance.Tick) : currentTick;

        /// <summary>Step after the car controller (using car ID as priority).</summary>
        public int SimulationOrder => 900 + (controller != null ? controller.CarId : 0);

        public bool IsFrozen => CurrentTick < frozenUntilTick;
        public bool IsJammed => CurrentTick < jammedUntilTick;
        public bool IsPunctured => CurrentTick < puncturedUntilTick;
        public bool HasShield => CurrentTick < shieldUntilTick;
        public bool IsSlowed => CurrentTick < slowedUntilTick;
        public bool HasFrozenImmunity => CurrentTick < frozenImmunityUntilTick;

        /// <summary>Dominant speed penalty (rule of strongest debuff, never multiplied).</summary>
        public float SpeedMultiplier
        {
            get
            {
                float multiplier = 1f;
                int tick = CurrentTick;
                if (tick < frozenUntilTick)
                {
                    multiplier = Mathf.Min(multiplier, frozenSpeedMultiplier);
                }
                return Mathf.Clamp(multiplier, 0.3f, 1f);
            }
        }

        /// <summary>Dominant grip penalty (rule of strongest debuff, never multiplied).</summary>
        public float GripMultiplier
        {
            get
            {
                float multiplier = 1f;
                int tick = CurrentTick;
                if (tick < frozenUntilTick)
                {
                    multiplier = Mathf.Min(multiplier, frozenGripMultiplier);
                }
                if (tick < puncturedUntilTick)
                {
                    multiplier = Mathf.Min(multiplier, puncturedGripMultiplier);
                }
                if (tick < slowedUntilTick)
                {
                    multiplier = Mathf.Min(multiplier, slowedGripMultiplier);
                }
                return Mathf.Clamp(multiplier, 0.3f, 1f);
            }
        }

        private void Awake()
        {
            controller = GetComponent<CarController>();
        }

        private void OnEnable()
        {
            if (SimulationDriver.Instance != null)
            {
                SimulationDriver.Instance.Register(this);
            }
        }

        private void OnDisable()
        {
            var driver = SimulationDriver.Current;
            if (driver != null)
            {
                driver.Unregister(this);
            }
        }

        public void SimulationStep(int tick)
        {
            currentTick = tick;
            // Check for expirations at this tick
            if (frozenUntilTick >= 0 && tick >= frozenUntilTick)
            {
                frozenUntilTick = -1;
                frozenImmunityUntilTick = tick + SimulationDriver.SecondsToTicks(frozenImmunitySeconds);
                OnStatusChanged?.Invoke(StatusKind.Frozen, false);
            }

            if (jammedUntilTick >= 0 && tick >= jammedUntilTick)
            {
                jammedUntilTick = -1;
                OnStatusChanged?.Invoke(StatusKind.Jammed, false);
            }

            if (puncturedUntilTick >= 0 && tick >= puncturedUntilTick)
            {
                puncturedUntilTick = -1;
                OnStatusChanged?.Invoke(StatusKind.Punctured, false);
            }

            if (shieldUntilTick >= 0 && tick >= shieldUntilTick)
            {
                shieldUntilTick = -1;
                OnStatusChanged?.Invoke(StatusKind.Shield, false);
            }

            if (slowedUntilTick >= 0 && tick >= slowedUntilTick)
            {
                slowedUntilTick = -1;
                OnStatusChanged?.Invoke(StatusKind.Slowed, false);
            }
        }

        public bool ApplyStatus(StatusKind kind, float durationSeconds, float magnitude = 1f)
        {
            int durationTicks = SimulationDriver.SecondsToTicks(durationSeconds);
            return ApplyStatus(kind, durationTicks, magnitude);
        }

        public bool ApplyStatus(StatusKind kind, int durationTicks, float magnitude = 1f)
        {
            if (kind == StatusKind.None || durationTicks <= 0) return false;

            int now = CurrentTick;
            int targetTick = now + durationTicks;

            switch (kind)
            {
                case StatusKind.Frozen:
                    if (now < frozenImmunityUntilTick) return false; // Immune
                    bool wasFrozen = now < frozenUntilTick;
                    frozenUntilTick = Mathf.Max(frozenUntilTick, targetTick);
                    if (!wasFrozen) OnStatusChanged?.Invoke(StatusKind.Frozen, true);
                    return true;

                case StatusKind.Jammed:
                    bool wasJammed = now < jammedUntilTick;
                    jammedUntilTick = Mathf.Max(jammedUntilTick, targetTick);
                    if (!wasJammed) OnStatusChanged?.Invoke(StatusKind.Jammed, true);
                    return true;

                case StatusKind.Punctured:
                    bool wasPunctured = now < puncturedUntilTick;
                    puncturedUntilTick = Mathf.Max(puncturedUntilTick, targetTick);
                    if (!wasPunctured) OnStatusChanged?.Invoke(StatusKind.Punctured, true);
                    return true;

                case StatusKind.Shield:
                    bool wasShielded = now < shieldUntilTick;
                    shieldUntilTick = Mathf.Max(shieldUntilTick, targetTick);
                    if (!wasShielded) OnStatusChanged?.Invoke(StatusKind.Shield, true);
                    return true;

                case StatusKind.Slowed:
                    bool wasSlowed = now < slowedUntilTick;
                    slowedUntilTick = Mathf.Max(slowedUntilTick, targetTick);
                    if (!wasSlowed) OnStatusChanged?.Invoke(StatusKind.Slowed, true);
                    return true;

                default:
                    return false;
            }
        }

        public bool RemoveStatus(StatusKind kind)
        {
            int now = CurrentTick;
            switch (kind)
            {
                case StatusKind.Frozen:
                    if (now < frozenUntilTick)
                    {
                        frozenUntilTick = -1;
                        frozenImmunityUntilTick = now + SimulationDriver.SecondsToTicks(frozenImmunitySeconds);
                        OnStatusChanged?.Invoke(StatusKind.Frozen, false);
                        return true;
                    }
                    return false;

                case StatusKind.Jammed:
                    if (now < jammedUntilTick)
                    {
                        jammedUntilTick = -1;
                        OnStatusChanged?.Invoke(StatusKind.Jammed, false);
                        return true;
                    }
                    return false;

                case StatusKind.Punctured:
                    if (now < puncturedUntilTick)
                    {
                        puncturedUntilTick = -1;
                        OnStatusChanged?.Invoke(StatusKind.Punctured, false);
                        return true;
                    }
                    return false;

                case StatusKind.Shield:
                    if (now < shieldUntilTick)
                    {
                        shieldUntilTick = -1;
                        OnStatusChanged?.Invoke(StatusKind.Shield, false);
                        return true;
                    }
                    return false;

                case StatusKind.Slowed:
                    if (now < slowedUntilTick)
                    {
                        slowedUntilTick = -1;
                        OnStatusChanged?.Invoke(StatusKind.Slowed, false);
                        return true;
                    }
                    return false;

                default:
                    return false;
            }
        }

        public bool ConsumeShield()
        {
            if (HasShield)
            {
                shieldUntilTick = -1;
                OnShieldConsumed?.Invoke();
                OnStatusChanged?.Invoke(StatusKind.Shield, false);
                return true;
            }
            return false;
        }

        public void ClearAllStatuses()
        {
            frozenUntilTick = -1;
            frozenImmunityUntilTick = -1;
            jammedUntilTick = -1;
            puncturedUntilTick = -1;
            shieldUntilTick = -1;
            slowedUntilTick = -1;
        }

        /// <summary>Clears all negative debuffs (Frozen, Jammed, Punctured, Slowed).</summary>
        public void ClearDebuffs()
        {
            RemoveStatus(StatusKind.Frozen);
            RemoveStatus(StatusKind.Jammed);
            RemoveStatus(StatusKind.Punctured);
            RemoveStatus(StatusKind.Slowed);
        }

        public bool IsActive(StatusKind kind)
        {
            int now = CurrentTick;
            switch (kind)
            {
                case StatusKind.Frozen: return now < frozenUntilTick;
                case StatusKind.Jammed: return now < jammedUntilTick;
                case StatusKind.Punctured: return now < puncturedUntilTick;
                case StatusKind.Shield: return now < shieldUntilTick;
                case StatusKind.Slowed: return now < slowedUntilTick;
                default: return false;
            }
        }

        public int GetRemainingTicks(StatusKind kind)
        {
            int now = CurrentTick;
            int until = -1;
            switch (kind)
            {
                case StatusKind.Frozen: until = frozenUntilTick; break;
                case StatusKind.Jammed: until = jammedUntilTick; break;
                case StatusKind.Punctured: until = puncturedUntilTick; break;
                case StatusKind.Shield: until = shieldUntilTick; break;
                case StatusKind.Slowed: until = slowedUntilTick; break;
            }
            return until > now ? until - now : 0;
        }

        public float GetRemainingSeconds(StatusKind kind)
        {
            int ticks = GetRemainingTicks(kind);
            return ticks > 0 ? ticks * SimulationDriver.FixedStep : 0f;
        }
    }
}
