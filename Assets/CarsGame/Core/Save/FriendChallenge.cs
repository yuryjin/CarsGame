using System;
using System.IO;
using System.Text;

namespace RacingGame.Core.Save
{
    [Serializable]
    public sealed class FriendChallenge
    {
        public string eventId;
        public int targetMs;
        public RecordSetup setup;
        public const int MaxCodeLength = 300;
        private const string Prefix = "CG1-";

        public static string Encode(EventBest best)
        {
            if (best == null || best.bestTimeMs <= 0 || best.bestTimeMs > Ghost.GhostRecord.MaxDurationMilliseconds
                || !Token(best.eventId) || best.setup == null || !best.setup.IsKnown || best.setup.continued) return "";
            var s = best.setup;
            if (!Valid(s)) return "";
            using var bytes = new MemoryStream();
            using var w = new BinaryWriter(bytes, Encoding.ASCII, true);
            w.Write(best.eventId); w.Write((int)best.bestTimeMs); w.Write(s.trackHash); w.Write((byte)s.laps);
            w.Write(s.tier); w.Write(s.body); w.Write((byte)s.build); w.Write((byte)s.primary); w.Write((byte)s.reserve);
            w.Write((byte)s.secondary); w.Write(s.fullArsenal);
            for (int i = 0; i < 3; i++) w.Write((byte)s.upgrades[i]);
            for (int i = 0; i < 4; i++) w.Write((byte)s.weapons[i]);
            w.Write(Hash(bytes.GetBuffer(), (int)bytes.Length));
            return Prefix + Convert.ToBase64String(bytes.ToArray()).TrimEnd('=').Replace('+','-').Replace('/','_');
        }

        public static FriendChallenge Decode(string code, out string error)
        {
            error = "challenge.invalid";
            if (string.IsNullOrEmpty(code) || code.Length > MaxCodeLength) return null;
            code = code.Trim();
            if (!code.StartsWith(Prefix, StringComparison.Ordinal))
            {
                if (code.StartsWith("CG", StringComparison.Ordinal) && code.IndexOf('-') > 2) error = "challenge.version";
                return null;
            }
            try
            {
                string encoded = code.Substring(Prefix.Length).Replace('-', '+').Replace('_', '/');
                byte[] raw = Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='));
                if (raw.Length < 20) return null;
                using var bytes = new MemoryStream(raw);
                using var r = new BinaryReader(bytes, Encoding.ASCII);
                var c = new FriendChallenge { eventId = ReadToken(r), targetMs = r.ReadInt32() };
                var s = c.setup = new RecordSetup { captured = true, trackHash = r.ReadInt32(), laps = r.ReadByte(),
                    tier = ReadToken(r), body = ReadToken(r), build = r.ReadByte(), primary = r.ReadByte(), reserve = r.ReadByte(), secondary = r.ReadByte() };
                byte full = r.ReadByte(); if (full > 1) return null; s.fullArsenal = full == 1;
                s.upgrades = Array.ConvertAll(r.ReadBytes(3), value => (int)value);
                s.weapons = Array.ConvertAll(r.ReadBytes(4), value => (int)value);
                if (r.ReadUInt32() != Hash(raw, raw.Length - 4) || bytes.Position != bytes.Length || !Token(c.eventId) || !Valid(s)
                    || c.targetMs <= 0 || c.targetMs > Ghost.GhostRecord.MaxDurationMilliseconds) return null;
                error = null;
                return c;
            }
            catch (FormatException) { return null; }
            catch (IOException) { return null; }
        }

        private static string ReadToken(BinaryReader reader)
        {
            int count = reader.ReadByte();
            if (count < 1 || count > 40) throw new FormatException();
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException();
            return Encoding.ASCII.GetString(bytes);
        }
        private static bool Token(string s) => s != null && s.Length > 0 && s.Length <= 40
            && Array.TrueForAll(s.ToCharArray(), c => c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-');
        private static bool Valid(RecordSetup s) => Token(s.tier) && Token(s.body) && s.laps > 0 && s.laps <= 20 && s.build >= 0 && s.build <= 3
            && s.primary >= 0 && s.primary <= 255 && s.reserve >= 0 && s.reserve <= 255 && s.secondary >= 0 && s.secondary <= 3
            && s.upgrades?.Length == 3 && Array.TrueForAll(s.upgrades, x => x >= 0 && x <= Economy.UpgradeCatalog.MaxLevel)
            && s.weapons?.Length == 4 && Array.TrueForAll(s.weapons, x => x >= 0 && x <= Economy.WeaponCatalog.MaxLevel);
        private static uint Hash(byte[] data, int count)
        {
            uint hash = 2166136261;
            for (int i = 0; i < count; i++) hash = unchecked((hash ^ data[i]) * 16777619);
            return hash;
        }
    }
}
