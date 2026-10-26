using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace RacingGame.Core.Ghost
{
    /// <summary>V1 yaw-only compatibility and V2 timed quaternion poses. Invalid payloads never reach playback.</summary>
    internal static class GhostRecordCodec
    {
        private const int SampleBytes = 26;
        private const int MaxDecodedBytes = GhostRecord.MaxSamples * SampleBytes + 352;
        private const float MaxCoordinate = 1000000f;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static byte[] Encode(GhostRecord record)
        {
            if (!ValidHeader(record) || record.Samples.Count < 2 || record.Samples.Count > GhostRecord.MaxSamples)
                return Array.Empty<byte>();
            using (var raw = new MemoryStream())
            {
                using (var writer = new BinaryWriter(raw, Encoding.UTF8, true))
                {
                    writer.Write((byte)GhostRecord.Version); writer.Write(record.TrackHash);
                    writer.Write((short)record.TickRate); writer.Write((byte)record.SampleStep);
                    byte[] name = Encoding.UTF8.GetBytes(record.PlayerName ?? "");
                    int length = Math.Min(255, name.Length);
                    // Do not split a UTF-8 character at the byte limit.
                    while (length < name.Length && length > 0 && (name[length] & 0xc0) == 0x80) length--;
                    writer.Write((byte)length); writer.Write(name, 0, length);
                    byte[] body = Encoding.ASCII.GetBytes(record.BodyId ?? "");
                    writer.Write((byte)body.Length); writer.Write(body);
                    writer.Write(record.Samples.Count); writer.Write(record.DurationMilliseconds);
                    int previous = -1;
                    foreach (var sample in record.Samples)
                    {
                        if (!ValidSample(sample, previous, record.DurationMilliseconds)) return Array.Empty<byte>();
                        previous = sample.TimeMilliseconds;
                        writer.Write(sample.TimeMilliseconds);
                        writer.Write((int)Math.Round(sample.Position.x * 100d));
                        writer.Write((int)Math.Round(sample.Position.y * 100d));
                        writer.Write((int)Math.Round(sample.Position.z * 100d));
                        Quaternion rotation = sample.Rotation.normalized;
                        writer.Write(Quantize(rotation.x)); writer.Write(Quantize(rotation.y));
                        writer.Write(Quantize(rotation.z)); writer.Write(Quantize(rotation.w));
                        writer.Write((sbyte)Math.Round(Mathf.Clamp(sample.Steer, -1f, 1f) * 127f));
                        writer.Write(sample.Flags);
                    }
                    writer.Flush();
                    writer.Write(Hash(raw.GetBuffer(), (int)raw.Length));
                }
                using (var compressed = new MemoryStream())
                {
                    using (var gzip = new GZipStream(compressed, System.IO.Compression.CompressionLevel.Optimal, true))
                        gzip.Write(raw.GetBuffer(), 0, (int)raw.Length);
                    return compressed.Length <= GhostRecord.MaxEncodedBytes ? compressed.ToArray() : Array.Empty<byte>();
                }
            }
        }

        internal static GhostRecord Decode(byte[] data)
        {
            if (data == null || data.Length < 16 || data.Length > GhostRecord.MaxEncodedBytes) return null;
            try
            {
                using (var encoded = new MemoryStream(data))
                using (var gzip = new GZipStream(encoded, CompressionMode.Decompress))
                using (var raw = new MemoryStream())
                {
                    var buffer = new byte[8192];
                    int bytes;
                    while ((bytes = gzip.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (raw.Length + bytes > MaxDecodedBytes) return null;
                        raw.Write(buffer, 0, bytes);
                    }
                    raw.Position = 0;
                    using (var reader = new BinaryReader(raw, Encoding.UTF8, true))
                    {
                        int version = reader.ReadByte();
                        if (version != 1 && version != GhostRecord.Version) return null;
                        var record = new GhostRecord { TrackHash = reader.ReadInt32(), TickRate = reader.ReadInt16(), SampleStep = reader.ReadByte() };
                        if (!ValidHeader(record)) return null;
                        int nameLength = reader.ReadByte();
                        byte[] name = reader.ReadBytes(nameLength);
                        if (name.Length != nameLength) return null;
                        record.PlayerName = (version == 1 ? Encoding.UTF8 : StrictUtf8).GetString(name);
                        if (version >= 2)
                        {
                            int bodyLength = reader.ReadByte();
                            if (bodyLength > 32) return null;
                            byte[] body = reader.ReadBytes(bodyLength);
                            if (body.Length != bodyLength) return null;
                            record.BodyId = StrictUtf8.GetString(body);
                        }
                        int count = reader.ReadInt32();
                        if (count < 2 || count > GhostRecord.MaxSamples) return null;
                        record.DurationMilliseconds = version == 1
                            ? (int)Math.Round(count * record.SampleStep * 1000d / record.TickRate) : reader.ReadInt32();
                        if (!ValidHeader(record) || raw.Length != raw.Position + count * (version == 1 ? 16 : SampleBytes) + 4) return null;
                        int previous = -1;
                        uint legacyHash = 2166136261u;
                        for (int i = 0; i < count; i++)
                        {
                            int time = version == 1 ? (int)Math.Round(i * record.SampleStep * 1000d / record.TickRate) : reader.ReadInt32();
                            int x = reader.ReadInt32(), y = reader.ReadInt32(), z = reader.ReadInt32();
                            Quaternion rotation;
                            if (version == 1)
                            {
                                ushort yaw = reader.ReadUInt16();
                                if (yaw > 36000) return null;
                                rotation = Quaternion.Euler(0f, yaw / 100f, 0f);
                                unchecked
                                {
                                    legacyHash = (legacyHash ^ (uint)x) * 16777619u;
                                    legacyHash = (legacyHash ^ (uint)z) * 16777619u;
                                    legacyHash = (legacyHash ^ yaw) * 16777619u;
                                }
                            }
                            else
                            {
                                rotation = new Quaternion(reader.ReadInt16() / 32767f, reader.ReadInt16() / 32767f,
                                    reader.ReadInt16() / 32767f, reader.ReadInt16() / 32767f);
                                if (Mathf.Abs(Quaternion.Dot(rotation, rotation) - 1f) > .002f) return null;
                                rotation = rotation.normalized;
                            }
                            var sample = new GhostRecord.Sample { TimeMilliseconds = time,
                                Position = new Vector3(x / 100f, y / 100f, z / 100f), Rotation = rotation,
                                Steer = reader.ReadSByte() / 127f, Flags = reader.ReadByte() };
                            if (!ValidSample(sample, previous, record.DurationMilliseconds)) return null;
                            previous = time; record.Samples.Add(sample);
                        }
                        uint checksum = reader.ReadUInt32();
                        uint expected = version == 1 ? legacyHash : Hash(raw.GetBuffer(), (int)raw.Length - 4);
                        return checksum == expected ? record : null;
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException || exception is InvalidOperationException)
            { return null; }
        }

        private static short Quantize(float value) => (short)Math.Round(Mathf.Clamp(value, -1f, 1f) * 32767f);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidHeader(GhostRecord record) => record.TickRate >= 10 && record.TickRate <= 200
            && record.SampleStep >= 1 && record.SampleStep <= 50 && record.DurationMilliseconds >= 0
            && record.DurationMilliseconds <= GhostRecord.MaxDurationMilliseconds && ValidBodyId(record.BodyId);
        private static bool ValidBodyId(string id)
        {
            if (string.IsNullOrEmpty(id)) return true; // Version-one recordings have no body metadata.
            if (id.Length > 32) return false;
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_' && c != '-') return false;
            return true;
        }
        private static bool ValidSample(GhostRecord.Sample sample, int previous, int duration)
        {
            var p = sample.Position; var q = sample.Rotation;
            return sample.TimeMilliseconds > previous && sample.TimeMilliseconds <= duration
                && Finite(p.x) && Finite(p.y) && Finite(p.z)
                && Mathf.Abs(p.x) <= MaxCoordinate && Mathf.Abs(p.y) <= MaxCoordinate && Mathf.Abs(p.z) <= MaxCoordinate
                && Finite(q.x) && Finite(q.y) && Finite(q.z) && Finite(q.w)
                && Finite(Quaternion.Dot(q, q)) && Quaternion.Dot(q, q) > .0001f
                && Finite(sample.Steer) && Mathf.Abs(sample.Steer) <= 1f;
        }
        private static uint Hash(byte[] bytes, int count)
        {
            uint hash = 2166136261u;
            unchecked { for (int i = 0; i < count; i++) hash = (hash ^ bytes[i]) * 16777619u; }
            return hash;
        }
    }
}
