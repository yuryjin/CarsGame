using System;

namespace RacingGame.Core.Save
{
    /// <summary>The actual starting configuration of a timed best, including a trial upgrade.</summary>
    [Serializable]
    public sealed class RecordSetup
    {
        // JsonUtility can materialize a missing nested object as an empty instance.
        // Only an explicitly captured setup is evidence of a record's conditions.
        public bool captured;
        public bool IsKnown => captured && !string.IsNullOrEmpty(tier) && !string.IsNullOrEmpty(body);
        public string tier, body;
        public int trackHash, laps, build, primary, reserve, secondary;
        public int[] upgrades, weapons;
        public bool fullArsenal, continued;

        public RecordSetup Copy() => !IsKnown ? null : new RecordSetup
        {
            captured = true,
            tier = tier, body = body, trackHash = trackHash, laps = laps, build = build,
            primary = primary, reserve = reserve, secondary = secondary,
            upgrades = upgrades == null ? null : (int[])upgrades.Clone(),
            weapons = weapons == null ? null : (int[])weapons.Clone(),
            fullArsenal = fullArsenal, continued = continued
        };

        public static bool Same(RecordSetup a, RecordSetup b) => a != null && a.IsKnown && b != null && b.IsKnown && Compare(a, b) == 0;

        // Stable tie-break for equal times from two devices. Known provenance beats missing provenance.
        public static int Compare(RecordSetup a, RecordSetup b)
        {
            if (a != null && !a.IsKnown) a = null;
            if (b != null && !b.IsKnown) b = null;
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return 1;
            if (b == null) return -1;
            int c = string.CompareOrdinal(a.tier, b.tier); if (c != 0) return c;
            c = string.CompareOrdinal(a.body, b.body); if (c != 0) return c;
            c = a.trackHash.CompareTo(b.trackHash); if (c != 0) return c;
            c = a.laps.CompareTo(b.laps); if (c != 0) return c;
            c = a.build.CompareTo(b.build); if (c != 0) return c;
            c = a.primary.CompareTo(b.primary); if (c != 0) return c;
            c = a.reserve.CompareTo(b.reserve); if (c != 0) return c;
            c = a.secondary.CompareTo(b.secondary); if (c != 0) return c;
            c = CompareLevels(a.upgrades, b.upgrades); if (c != 0) return c;
            c = CompareLevels(a.weapons, b.weapons); if (c != 0) return c;
            c = a.fullArsenal.CompareTo(b.fullArsenal); if (c != 0) return c;
            return a.continued.CompareTo(b.continued);
        }

        private static int CompareLevels(int[] a, int[] b)
        {
            int count = Math.Max(a?.Length ?? 0, b?.Length ?? 0);
            for (int i = 0; i < count; i++)
            {
                int c = (a != null && i < a.Length ? a[i] : 0).CompareTo(b != null && i < b.Length ? b[i] : 0);
                if (c != 0) return c;
            }
            return 0;
        }
    }
}
