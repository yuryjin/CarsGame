using System;
using System.Collections.Generic;

namespace RacingGame.Core.Economy
{
    /// <summary>How a cosmetic is unlocked.</summary>
    public enum UnlockKind { Free, Coins, Rewarded }

    /// <summary>Stripe pattern clipped onto the body by <c>CarLivery</c>.</summary>
    public enum LiveryPattern { None, CenterStripe, TwinStripes, SideBand, Hood, Tail }

    public readonly struct PaintDef
    {
        public readonly string Id;
        /// <summary>sRGB body colour.</summary>
        public readonly float R, G, B;
        public readonly UnlockKind Unlock;
        public readonly int Cost;
        public PaintDef(string id, float r, float g, float b, UnlockKind unlock, int cost) { Id = id; R = r; G = g; B = b; Unlock = unlock; Cost = cost; }
        public string NameKey => "paint." + Id;
    }

    public readonly struct LiveryDef
    {
        public readonly string Id;
        public readonly LiveryPattern Pattern;
        public readonly UnlockKind Unlock;
        public readonly int Cost;
        public LiveryDef(string id, LiveryPattern pattern, UnlockKind unlock, int cost) { Id = id; Pattern = pattern; Unlock = unlock; Cost = cost; }
        public string NameKey => "livery." + Id;
    }

    /// <summary>
    /// Paints and liveries: cosmetic only (no stats), bought with coins or unlocked with a rewarded ad.
    /// Ids are stored in the save, so they must never be renamed.
    /// </summary>
    public static class Cosmetics
    {
        public const string DefaultPaint = "rosso";
        public const string DefaultLivery = "none";

        public static readonly PaintDef[] Paints =
        {
            new PaintDef("rosso",   0.92f, 0.06f, 0.10f, UnlockKind.Free, 0),
            new PaintDef("snow",    0.92f, 0.93f, 0.95f, UnlockKind.Coins, 120),
            new PaintDef("carbon",  0.10f, 0.11f, 0.13f, UnlockKind.Coins, 120),
            new PaintDef("sun",     1.00f, 0.80f, 0.10f, UnlockKind.Coins, 180),
            new PaintDef("tangerine", 1.00f, 0.45f, 0.08f, UnlockKind.Coins, 180),
            new PaintDef("lime",    0.45f, 0.90f, 0.15f, UnlockKind.Coins, 220),
            new PaintDef("ocean",   0.08f, 0.35f, 0.95f, UnlockKind.Coins, 220),
            new PaintDef("teal",    0.05f, 0.75f, 0.70f, UnlockKind.Rewarded, 0),
            new PaintDef("violet",  0.50f, 0.18f, 0.90f, UnlockKind.Coins, 300),
            new PaintDef("bubblegum", 1.00f, 0.40f, 0.70f, UnlockKind.Rewarded, 0),
            new PaintDef("steel",   0.62f, 0.66f, 0.72f, UnlockKind.Coins, 360),
            new PaintDef("gold",    0.95f, 0.70f, 0.25f, UnlockKind.Coins, 600),
        };

        public static readonly LiveryDef[] Liveries =
        {
            new LiveryDef("none",   LiveryPattern.None, UnlockKind.Free, 0),
            new LiveryDef("stripe", LiveryPattern.CenterStripe, UnlockKind.Coins, 200),
            new LiveryDef("twin",   LiveryPattern.TwinStripes, UnlockKind.Coins, 300),
            new LiveryDef("band",   LiveryPattern.SideBand, UnlockKind.Coins, 300),
            new LiveryDef("hood",   LiveryPattern.Hood, UnlockKind.Rewarded, 0),
            new LiveryDef("tail",   LiveryPattern.Tail, UnlockKind.Coins, 250),
        };

        public static int PaintIndex(string id) { for (int i = 0; i < Paints.Length; i++) if (Paints[i].Id == id) return i; return -1; }
        public static int LiveryIndex(string id) { for (int i = 0; i < Liveries.Length; i++) if (Liveries[i].Id == id) return i; return -1; }

        public static PaintDef Paint(string id) { int i = PaintIndex(id); return i >= 0 ? Paints[i] : Paints[0]; }
        public static LiveryDef Livery(string id) { int i = LiveryIndex(id); return i >= 0 ? Liveries[i] : Liveries[0]; }

        /// <summary>Free items are always owned; everything else must be in the owned list.</summary>
        public static bool Owns(IList<string> owned, string id, UnlockKind unlock) =>
            unlock == UnlockKind.Free || (owned != null && owned.Contains(id));

        public static bool OwnsPaint(IList<string> owned, string id) { int i = PaintIndex(id); return i >= 0 && Owns(owned, "paint:" + id, Paints[i].Unlock); }
        public static bool OwnsLivery(IList<string> owned, string id) { int i = LiveryIndex(id); return i >= 0 && Owns(owned, "livery:" + id, Liveries[i].Unlock); }

        /// <summary>Buy with coins. Adds the "paint:id" / "livery:id" key; returns false when not for coins, owned or too expensive.</summary>
        public static bool TryBuy(IList<string> owned, bool livery, string id, long coins, out int cost)
        {
            cost = 0;
            int i = livery ? LiveryIndex(id) : PaintIndex(id);
            if (i < 0 || owned == null) return false;
            var unlock = livery ? Liveries[i].Unlock : Paints[i].Unlock;
            cost = livery ? Liveries[i].Cost : Paints[i].Cost;
            string key = (livery ? "livery:" : "paint:") + id;
            if (unlock != UnlockKind.Coins || owned.Contains(key) || coins < cost) return false;
            owned.Add(key);
            return true;
        }

        /// <summary>Grant after a watched rewarded ad; false when the item is not a rewarded one or already owned.</summary>
        public static bool GrantRewarded(IList<string> owned, bool livery, string id)
        {
            int i = livery ? LiveryIndex(id) : PaintIndex(id);
            if (i < 0 || owned == null) return false;
            var unlock = livery ? Liveries[i].Unlock : Paints[i].Unlock;
            string key = (livery ? "livery:" : "paint:") + id;
            if (unlock != UnlockKind.Rewarded || owned.Contains(key)) return false;
            owned.Add(key);
            return true;
        }

        /// <summary>A stripe colour that reads on the body: near-white on dark paints, near-black on light ones.</summary>
        public static void ContrastStripe(float r, float g, float b, out float sr, out float sg, out float sb)
        {
            float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;
            float v = luma > 0.55f ? 0.08f : 0.95f;
            sr = sg = sb = v;
        }
    }
}
