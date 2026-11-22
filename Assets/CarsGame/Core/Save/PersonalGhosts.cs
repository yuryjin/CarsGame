using System;
using System.Collections.Generic;
using RacingGame.Core.Ghost;
using UnityEngine;

namespace RacingGame.Core.Save
{
    /// <summary>Personal replays keyed by event, layout/direction hash and lap count.</summary>
    [Serializable]
    public sealed class PersonalGhosts
    {
        // Includes base64 and JSON metadata. Leave space in the cloud profile for progression.
        public const int MaxSerializedBytes = 160 * 1024;
        [SerializeField] private List<Entry> entries = new List<Entry>();

        [Serializable] private sealed class Entry
        {
            public string eventId, replay;
            public int trackHash, laps, durationMs;
            public long savedAt;
            public bool Unassigned => string.IsNullOrEmpty(eventId);
            // Fixed JSON keys, worst-case decimal integers, separator and up to six bytes per escaped UTF-16 unit.
            public long ByteBudget => 144L + replay.Length + 6L * (eventId?.Length ?? 0);
            public bool Matches(string id, int hash, int count) => eventId == id && trackHash == hash && laps == count;
        }

        public GhostRecord Find(string eventId, int trackHash, int laps)
        {
            if (string.IsNullOrEmpty(eventId) || laps < 1) return null;
            var entry = entries.Find(e => e.Matches(eventId, trackHash, laps));
            return GhostRecord.FromBase64(entry?.replay);
        }

        public bool KeepBest(string eventId, int laps, string replay, long savedAt)
        {
            if (string.IsNullOrEmpty(eventId) || laps < 1) return false;
            var candidate = Read(eventId, laps, replay, savedAt);
            if (candidate == null || !Insert(candidate)) return false;
            Trim();
            return true;
        }

        // V1 and V2 payloads contain the hash but no explicit event or lap count. Keep the original
        // bytes until the actual circuit can confirm its completed laps from the recorded poses.
        public bool ResolveLegacy(string eventId, int trackHash, int laps, Func<GhostRecord, bool> completedLaps)
        {
            if (string.IsNullOrEmpty(eventId) || laps < 1 || completedLaps == null) return false;
            bool changed = false;
            foreach (var old in entries.FindAll(e => e.Unassigned && e.trackHash == trackHash))
            {
                var record = GhostRecord.FromBase64(old.replay);
                if (record == null || !completedLaps(record)) continue;
                entries.Remove(old);
                Insert(new Entry { eventId = eventId, trackHash = trackHash, laps = laps, durationMs = old.durationMs,
                    replay = old.replay, savedAt = old.savedAt });
                changed = true;
            }
            if (changed) Trim();
            return changed;
        }

        internal void Restore(string legacyReplay, long legacySavedAt)
        {
            var saved = entries;
            entries = new List<Entry>();
            if (saved != null)
                foreach (var row in saved)
                {
                    if (row == null || (row.Unassigned ? row.laps != 0 : row.laps < 1)) continue;
                    var entry = Read(row.eventId, row.laps, row.replay, row.savedAt);
                    if (entry != null) Insert(entry);
                }
            var legacy = Read("", 0, legacyReplay, legacySavedAt);
            if (legacy != null) Insert(legacy);
            Trim();
        }

        internal static PersonalGhosts Merge(PersonalGhosts a, PersonalGhosts b)
        {
            var result = new PersonalGhosts();
            if (a != null) foreach (var row in a.entries) result.Insert(row);
            if (b != null) foreach (var row in b.entries) result.Insert(row);
            result.Trim();
            return result;
        }

        private static Entry Read(string id, int laps, string replay, long savedAt)
        {
            var record = GhostRecord.FromBase64(replay);
            if (record == null) return null;
            // Rebuild the index from the checked payload even if hand-edited JSON claims a faster time.
            return new Entry { eventId = id ?? "", laps = laps, replay = replay, savedAt = savedAt,
                trackHash = record.TrackHash, durationMs = record.DurationMilliseconds };
        }

        private bool Insert(Entry candidate)
        {
            if (candidate.ByteBudget > MaxSerializedBytes - 32) return false;
            Entry previous;
            if (candidate.Unassigned)
            {
                // Two old profiles can contain different lap counts for the same hash.
                previous = entries.Find(e => e.replay == candidate.replay);
                if (previous != null && !previous.Unassigned) return false;
            }
            else
            {
                entries.RemoveAll(e => e.Unassigned && e.replay == candidate.replay);
                previous = entries.Find(e => e.Matches(candidate.eventId, candidate.trackHash, candidate.laps));
            }
            if (previous != null)
            {
                int order = candidate.durationMs.CompareTo(previous.durationMs);
                if (order == 0) order = string.CompareOrdinal(candidate.replay, previous.replay);
                if (order > 0 || (order == 0 && candidate.savedAt <= previous.savedAt)) return false;
                entries.Remove(previous);
            }
            entries.Add(candidate);
            return true;
        }

        private void Trim()
        {
            // Retain the most recently saved bests first, with a stable order when devices tie.
            entries.Sort((a, b) =>
            {
                int order = b.savedAt.CompareTo(a.savedAt);
                if (order == 0) order = string.CompareOrdinal(a.eventId, b.eventId);
                if (order == 0) order = a.trackHash.CompareTo(b.trackHash);
                if (order == 0) order = a.laps.CompareTo(b.laps);
                return order != 0 ? order : string.CompareOrdinal(a.replay, b.replay);
            });
            long used = 32; // archive wrapper and array brackets
            entries.RemoveAll(e =>
            {
                if (used + e.ByteBudget > MaxSerializedBytes) return true;
                used += e.ByteBudget;
                return false;
            });
        }
    }
}
