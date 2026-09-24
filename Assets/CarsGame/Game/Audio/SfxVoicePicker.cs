namespace RacingGame
{
    /// <summary>
    /// Chooses which pooled voice plays a new one-shot. A free voice wins; otherwise the new sound
    /// steals the voice that is quietest right now (start loudness scaled by the share of the clip
    /// still left), but only if the new sound is at least that loud; otherwise it is dropped. No
    /// clip may hold more than <c>maxPerClip</c> voices, so eight machine guns cannot cut off a
    /// finish or a kill sting. Pure bookkeeping (no Unity calls), driven by the caller's clock.
    /// </summary>
    public sealed class SfxVoicePicker
    {
        private readonly object[] clips;
        private readonly float[] loudness, start, end;
        private readonly int maxPerClip;

        public int Count => clips.Length;

        public SfxVoicePicker(int voices, int maxPerClip)
        {
            clips = new object[voices];
            loudness = new float[voices];
            start = new float[voices];
            end = new float[voices];
            this.maxPerClip = maxPerClip < 1 ? 1 : maxPerClip;
        }

        public bool IsBusy(int voice, float now) => end[voice] > now;

        /// <summary>Loudness the voice still contributes at <paramref name="now"/> (0 when finished).</summary>
        public float Remaining(int voice, float now)
        {
            if (end[voice] <= now) return 0f;
            float length = end[voice] - start[voice];
            return length <= 0f ? 0f : loudness[voice] * (end[voice] - now) / length;
        }

        /// <summary>
        /// Reserve a voice for <paramref name="clip"/> lasting <paramref name="seconds"/> from
        /// <paramref name="now"/>. Returns the voice index, or -1 when the sound should be dropped.
        /// </summary>
        public int Pick(object clip, float level, float seconds, float now)
        {
            int free = -1, quietest = -1, sameQuietest = -1, sameCount = 0;
            float quietestLevel = float.MaxValue, sameLevel = float.MaxValue;
            for (int i = 0; i < clips.Length; i++)
            {
                if (!IsBusy(i, now)) { if (free < 0) free = i; continue; }
                float r = Remaining(i, now);
                if (r < quietestLevel) { quietestLevel = r; quietest = i; }
                if (ReferenceEquals(clips[i], clip))
                {
                    sameCount++;
                    if (r < sameLevel) { sameLevel = r; sameQuietest = i; }
                }
            }

            int voice;
            if (sameCount >= maxPerClip) voice = level >= sameLevel ? sameQuietest : -1;
            else if (free >= 0) voice = free;
            else voice = level >= quietestLevel ? quietest : -1;
            if (voice < 0) return -1;

            clips[voice] = clip;
            loudness[voice] = level;
            start[voice] = now;
            end[voice] = now + (seconds > 0f ? seconds : 0f);
            return voice;
        }
    }
}
