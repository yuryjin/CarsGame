using System;
using System.Collections.Generic;
using UnityEngine;

namespace RacingGame.Core.Ghost
{
    /// <summary>A bounded replay of visual poses, sampled against the same clock as the race.</summary>
    public sealed class GhostRecord
    {
        public const int Version = 2;
        public const int MaxEncodedBytes = 96 * 1024;
        public const int MaxSamples = 20000;
        public const int MaxDurationMilliseconds = 24 * 60 * 60 * 1000;
        public const byte BoostFlag = 1;
        public const byte TeleportFlag = 2;

        public struct Sample
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public int TimeMilliseconds;
            public float Steer;
            public byte Flags;
            public float YawDegrees { get => Rotation.eulerAngles.y; set => Rotation = Quaternion.Euler(0f, value, 0f); }
            public float TimeSeconds => TimeMilliseconds / 1000f;
        }

        public int TrackHash;
        public int TickRate = 50;
        public int SampleStep = 5;
        public string PlayerName = "";
        public string BodyId = "";
        public readonly List<Sample> Samples = new List<Sample>();
        public int DurationMilliseconds { get; internal set; }
        public float Duration => DurationMilliseconds / 1000f;

        /// <summary>Compatibility helper for uniformly sampled yaw-only recordings.</summary>
        public void Add(Vector3 position, float yawDegrees, float steer, byte flags)
        {
            AddPose(position, Quaternion.Euler(0f, yawDegrees, 0f), Samples.Count * SampleStep / (double)TickRate, steer, flags);
            DurationMilliseconds = (int)Math.Round(Samples.Count * SampleStep * 1000d / TickRate);
        }

        public void AddPose(Vector3 position, Quaternion rotation, double elapsedSeconds, float steer, byte flags)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0d
                || elapsedSeconds * 1000d > MaxDurationMilliseconds) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            int time = (int)Math.Floor(elapsedSeconds * 1000d);
            var sample = new Sample { Position = position, Rotation = rotation.normalized, TimeMilliseconds = time,
                Steer = Mathf.Clamp(steer, -1f, 1f), Flags = flags };
            if (Samples.Count > 0 && time < Samples[Samples.Count - 1].TimeMilliseconds)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), "Replay time cannot run backwards.");
            if (Samples.Count > 0 && time == Samples[Samples.Count - 1].TimeMilliseconds)
            {
                // A final pose or a reset can land in the same millisecond as a regular sample.
                sample.Flags |= (byte)(Samples[Samples.Count - 1].Flags & TeleportFlag);
                Samples[Samples.Count - 1] = sample;
            }
            else
            {
                if (Samples.Count >= MaxSamples) throw new InvalidOperationException("Replay sample limit reached.");
                Samples.Add(sample);
            }
            DurationMilliseconds = time;
        }

        public bool Evaluate(float t, out Vector3 position, out float yaw)
        {
            bool found = EvaluatePose(t, out position, out var rotation);
            yaw = rotation.eulerAngles.y;
            return found;
        }

        /// <summary>Interpolate by recorded race time; a teleport is a cut, never a flight across the map.</summary>
        public bool EvaluatePose(float t, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            if (Samples.Count == 0 || float.IsNaN(t) || float.IsInfinity(t)) return false;
            // Float seconds cannot represent every exact millisecond; use the codec's time grid.
            double time = Math.Round(t * 1000d);
            int first = 0, last = Samples.Count - 1;
            if (time <= Samples[first].TimeMilliseconds) { position = Samples[first].Position; rotation = Samples[first].Rotation; return true; }
            if (time >= Samples[last].TimeMilliseconds) { position = Samples[last].Position; rotation = Samples[last].Rotation; return true; }
            while (last - first > 1)
            {
                int middle = (first + last) / 2;
                if (Samples[middle].TimeMilliseconds <= time) first = middle; else last = middle;
            }
            var a = Samples[first]; var b = Samples[last];
            float blend = (b.Flags & TeleportFlag) != 0 ? 0f : (float)((time - a.TimeMilliseconds) / (b.TimeMilliseconds - a.TimeMilliseconds));
            position = Vector3.LerpUnclamped(a.Position, b.Position, blend);
            rotation = Quaternion.SlerpUnclamped(a.Rotation, b.Rotation, blend);
            return true;
        }

        public byte[] ToBytes() => GhostRecordCodec.Encode(this);
        public static GhostRecord FromBytes(byte[] data) => GhostRecordCodec.Decode(data);
        public string ToBase64() => Convert.ToBase64String(ToBytes());
        public static GhostRecord FromBase64(string encoded)
        {
            try
            {
                return string.IsNullOrEmpty(encoded) || encoded.Length > (MaxEncodedBytes + 2) / 3 * 4
                    ? null : FromBytes(Convert.FromBase64String(encoded));
            }
            catch (FormatException) { return null; }
        }
    }
}
