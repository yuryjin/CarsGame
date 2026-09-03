using System;

namespace RacingGame.Core.Sim
{
    /// <summary>
    /// Independent seeded random streams so that one subsystem's draws never change another's
    /// sequence (AI decisions must not depend on how many VFX were spawned). Simulation code
    /// must use these instead of UnityEngine.Random.
    /// </summary>
    public sealed class RngStreams
    {
        public enum Stream { Ai = 0, Gameplay = 1, Spawn = 2, Count = 3 }

        private readonly Random[] streams;
        public int Seed { get; }

        public RngStreams(int seed)
        {
            Seed = seed;
            streams = new Random[(int)Stream.Count];
            for (int i = 0; i < streams.Length; i++)
            {
                // Derive per-stream seeds deterministically from the race seed.
                unchecked { streams[i] = new Random(seed * 486187739 + (i + 1) * 1000003); }
            }
        }

        public Random Get(Stream stream) => streams[(int)stream];

        public float Range(Stream stream, float minInclusive, float maxExclusive)
            => minInclusive + (float)streams[(int)stream].NextDouble() * (maxExclusive - minInclusive);

        public int Range(Stream stream, int minInclusive, int maxExclusive)
            => streams[(int)stream].Next(minInclusive, maxExclusive);
    }
}
