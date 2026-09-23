using System;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame.Core.Sim
{
    /// <summary>
    /// Represents a single active or inactive entry in the kill feed.
    /// </summary>
    public struct KillFeedEntry
    {
        public int KillerId;
        public int VictimId;
        public float TimeRemaining;
        public float TotalDuration;
        public string FormattedText;
        public bool IsActive;

        /// <summary>
        /// Fade multiplier from 1.0 down to 0.0 during the final 1 second of visibility.
        /// </summary>
        public float Alpha
        {
            get
            {
                if (!IsActive || TotalDuration <= 0f) return 0f;
                return Mathf.Clamp01(TimeRemaining / 1.0f);
            }
        }
    }

    /// <summary>
    /// Ring buffer storing up to 3 most recent eliminations for 4 seconds each.
    /// Manages racer name registration, string caching, and slot rotation with zero allocations at runtime.
    /// </summary>
    public sealed class KillFeedBuffer
    {
        public const int MaxEntries = 3;
        public const float DefaultDuration = 4.0f;

        private readonly KillFeedEntry[] entries = new KillFeedEntry[MaxEntries];
        private readonly string[] racerNames = new string[32];
        private readonly string[,] killStringCache = new string[32, 32];
        private string defaultUnknownName = "Car";

        public int Capacity => MaxEntries;

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < MaxEntries; i++)
                {
                    if (entries[i].IsActive) count++;
                }
                return count;
            }
        }

        public KillFeedEntry GetEntry(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxEntries) return default;
            return entries[slotIndex];
        }

        /// <summary>
        /// Registers or updates the display name for a car ID, invalidating cached kill strings for this car.
        /// </summary>
        public void RegisterRacerName(int carId, string name)
        {
            if (carId >= 0 && carId < racerNames.Length)
            {
                racerNames[carId] = name;
                for (int v = 0; v < racerNames.Length; v++)
                {
                    killStringCache[carId, v] = null;
                    killStringCache[v, carId] = null;
                }
            }
        }

        public string GetRacerName(int carId)
        {
            if (carId >= 0 && carId < racerNames.Length && racerNames[carId] != null)
                return racerNames[carId];
            return carId > 0 ? $"Car {carId}" : defaultUnknownName;
        }

        /// <summary>
        /// Gets the cached elimination string for the killer and victim pair in the format "[Killer] × [Victim]".
        /// </summary>
        public string GetKillText(int killerId, int victimId)
        {
            if (killerId >= 0 && killerId < racerNames.Length && victimId >= 0 && victimId < racerNames.Length)
            {
                string cached = killStringCache[killerId, victimId];
                if (cached != null) return cached;

                string kName = GetRacerName(killerId);
                string vName = GetRacerName(victimId);
                string formatted = FormatKill(kName, vName);
                killStringCache[killerId, victimId] = formatted;
                return formatted;
            }

            return FormatKill(GetRacerName(killerId), GetRacerName(victimId));
        }

        public static string FormatKill(string killerName, string victimName)
        {
            return $"{killerName} × {victimName}";
        }

        /// <summary>
        /// Pre-populates all combinations among registered racer names to ensure zero string allocations during combat.
        /// </summary>
        public void PrecacheAll()
        {
            for (int k = 0; k < racerNames.Length; k++)
            {
                for (int v = 0; v < racerNames.Length; v++)
                {
                    if (racerNames[k] != null || racerNames[v] != null)
                    {
                        killStringCache[k, v] = FormatKill(GetRacerName(k), GetRacerName(v));
                    }
                }
            }
        }

        /// <summary>
        /// Pushes a new elimination event into the ring buffer. Pushes the oldest entry out if full.
        /// </summary>
        public void Push(int killerId, int victimId, float duration = DefaultDuration)
        {
            string formattedText = GetKillText(killerId, victimId);
            PushEntry(new KillFeedEntry
            {
                KillerId = killerId,
                VictimId = victimId,
                TimeRemaining = duration,
                TotalDuration = duration,
                FormattedText = formattedText,
                IsActive = true
            });
        }

        /// <summary>
        /// Pushes a pre-built entry directly into the buffer.
        /// </summary>
        public void PushEntry(KillFeedEntry entry)
        {
            entries[2] = entries[1];
            entries[1] = entries[0];
            entries[0] = entry;
        }

        /// <summary>
        /// Advances time and updates slot decay.
        /// </summary>
        public void Update(float deltaTime)
        {
            for (int i = 0; i < MaxEntries; i++)
            {
                if (entries[i].IsActive)
                {
                    entries[i].TimeRemaining -= deltaTime;
                    if (entries[i].TimeRemaining <= 0f)
                    {
                        entries[i].IsActive = false;
                        entries[i].TimeRemaining = 0f;
                    }
                }
            }
        }

        /// <summary>
        /// Clears all active slots and resets duration counters.
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < MaxEntries; i++)
            {
                entries[i] = default;
            }
        }
    }
}
