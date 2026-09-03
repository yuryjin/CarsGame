using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace RacingGame.Core.Ghost
{
    /// <summary>
    /// Compact recording of one lap/race for async multiplayer ghosts. Samples every N ticks:
    /// position (cm, int32×3), yaw (uint16, 1/100°), steer (sbyte), flags (byte) = 16 bytes.
    /// Serialised with a header (version, track hash, tick rate, sample step) + CRC, gzip, base64.
    /// </summary>
    public sealed class GhostRecord
    {
        public const int Version = 1;
        public struct Sample { public Vector3 Position; public float YawDegrees; public float Steer; public byte Flags; }

        public int TrackHash;
        public int TickRate = 50;
        public int SampleStep = 5;
        public string PlayerName = "";
        public readonly List<Sample> Samples = new List<Sample>();

        public float Duration => Samples.Count * SampleStep / (float)TickRate;

        public void Add(Vector3 position, float yawDegrees, float steer, byte flags)
            => Samples.Add(new Sample { Position = position, YawDegrees = yawDegrees, Steer = steer, Flags = flags });

        /// <summary>Interpolated pose at time t (seconds since start); clamps at both ends.</summary>
        public bool Evaluate(float t, out Vector3 position, out float yaw)
        {
            position = default; yaw = 0f;
            if (Samples.Count == 0) return false;
            float dt = SampleStep / (float)TickRate;
            float f = Mathf.Clamp(t / dt, 0f, Samples.Count - 1);
            int i = Mathf.FloorToInt(f);
            int j = Mathf.Min(i + 1, Samples.Count - 1);
            float u = f - i;
            position = Vector3.LerpUnclamped(Samples[i].Position, Samples[j].Position, u);
            yaw = Mathf.LerpAngle(Samples[i].YawDegrees, Samples[j].YawDegrees, u);
            return true;
        }

        public byte[] ToBytes()
        {
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                using (var w = new BinaryWriter(gz))
                {
                    w.Write((byte)Version); w.Write(TrackHash); w.Write((short)TickRate); w.Write((byte)SampleStep);
                    var name = System.Text.Encoding.UTF8.GetBytes(PlayerName ?? "");
                    w.Write((byte)Math.Min(255, name.Length)); w.Write(name, 0, Math.Min(255, name.Length));
                    w.Write(Samples.Count);
                    foreach (var s in Samples)
                    {
                        w.Write((int)Math.Round(s.Position.x * 100f)); w.Write((int)Math.Round(s.Position.y * 100f)); w.Write((int)Math.Round(s.Position.z * 100f));
                        w.Write((ushort)Math.Round(Mathf.Repeat(s.YawDegrees, 360f) * 100f));   // 0..36000 needs 16 unsigned bits
                        w.Write((sbyte)Math.Round(Mathf.Clamp(s.Steer, -1f, 1f) * 127f));
                        w.Write(s.Flags);
                    }
                    w.Write(Crc(Samples));
                }
                return ms.ToArray();
            }
        }

        public const int MaxEncodedBytes = 96 * 1024;
        public const int MaxSamples = 20000;

        public static GhostRecord FromBytes(byte[] data)
        {
            if (data == null || data.Length < 16 || data.Length > MaxEncodedBytes) return null;
            try
            {
                using (var ms = new MemoryStream(data))
                using (var gz = new GZipStream(ms, CompressionMode.Decompress))
                using (var r = new BinaryReader(gz))
                {
                    int version = r.ReadByte();
                    if (version != Version) return null;
                    var g = new GhostRecord { TrackHash = r.ReadInt32(), TickRate = r.ReadInt16(), SampleStep = r.ReadByte() };
                    if (g.TickRate < 10 || g.TickRate > 200 || g.SampleStep < 1 || g.SampleStep > 50) return null;
                    int nameLen = r.ReadByte();
                    g.PlayerName = System.Text.Encoding.UTF8.GetString(r.ReadBytes(nameLen));
                    int n = r.ReadInt32();
                    if (n < 2 || n > MaxSamples) return null;
                    for (int i = 0; i < n; i++)
                    {
                        var s = new Sample
                        {
                            Position = new Vector3(r.ReadInt32() / 100f, r.ReadInt32() / 100f, r.ReadInt32() / 100f),
                            YawDegrees = r.ReadUInt16() / 100f,
                            Steer = r.ReadSByte() / 127f,
                            Flags = r.ReadByte()
                        };
                        g.Samples.Add(s);
                    }
                    uint crc = r.ReadUInt32();
                    return crc == Crc(g.Samples) ? g : null;
                }
            }
            catch { return null; }
        }

        public string ToBase64() => Convert.ToBase64String(ToBytes());
        public static GhostRecord FromBase64(string s) { try { return string.IsNullOrEmpty(s) || s.Length > MaxEncodedBytes * 2 ? null : FromBytes(Convert.FromBase64String(s)); } catch { return null; } }

        private static uint Crc(List<Sample> samples)
        {
            uint h = 2166136261u;
            foreach (var s in samples)
            {
                unchecked
                {
                    // Quantise exactly like the writer, via int, so negative values hash identically on every runtime.
                    h = (h ^ (uint)(int)Math.Round(s.Position.x * 100f)) * 16777619u;
                    h = (h ^ (uint)(int)Math.Round(s.Position.z * 100f)) * 16777619u;
                    h = (h ^ (uint)(int)Math.Round(Mathf.Repeat(s.YawDegrees, 360f) * 100f)) * 16777619u;
                }
            }
            return h;
        }
    }
}
