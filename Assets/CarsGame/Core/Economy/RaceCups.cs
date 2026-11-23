using System;
using RacingGame.Core.Save;

namespace RacingGame.Core.Economy
{
    public sealed class RaceCup
    {
        public readonly string Id, Reward;
        public readonly string[] Events;
        public const int TargetStars = 6;
        public RaceCup(string id, string reward, params string[] events) { Id = id; Reward = reward; Events = events; }
        public string NameKey => "cup." + Id;
        public string RewardNameKey => Reward.Replace(':', '.');

        public int Stars(ProgressData progress)
        {
            int total = 0;
            foreach (var id in Events)
            {
                int mask = progress?.bests?.Find(b => b != null && b.eventId == id)?.stars ?? 0;
                for (int bit = 1; bit <= 4; bit <<= 1) if ((mask & bit) != 0) total++;
            }
            return total;
        }

        public int Finishes(ProgressData progress)
        {
            int total = 0;
            foreach (var id in Events)
                if (progress?.bests?.Exists(b => b != null && b.eventId == id && b.bestTimeMs > 0) == true) total++;
            return total;
        }

        public bool Complete(ProgressData progress) => Stars(progress) >= TargetStars && Finishes(progress) == Events.Length;
    }

    public static class RaceCups
    {
        public static readonly RaceCup[] All =
        {
            new RaceCup("rookie", "paint:sun", "e1", "e2", "e3"),
            new RaceCup("combat", "livery:twin", "e4", "e5", "e18"),
            new RaceCup("explorer", "paint:gold", "e17", "e19", "e20")
        };

        public const string ChapterReward = "livery:band";
        public static readonly string[] ChapterEvents = { "e6", "e7", "e8" };

        public static int ChapterStep(ProgressData p)
        {
            if (p?.canyonChapterWon == true) return ChapterEvents.Length;
            for (int i = 0; i < ChapterEvents.Length - 1; i++)
            {
                string here = ChapterEvents[i], next = ChapterEvents[i + 1];
                if (p?.bests?.Exists(b => b != null && b.eventId == here && b.bestTimeMs > 0) != true
                    || p.unlockedEvents?.Contains(next) != true) return i;
            }
            return ChapterEvents.Length - 1;
        }

        public static void FinishChapter(ProgressData p, string eventId, bool wonDuel)
        {
            if (wonDuel && eventId == ChapterEvents[2] && ChapterStep(p) == 2) p.canyonChapterWon = true;
            if (p.canyonChapterWon && !p.cosmetics.Contains(ChapterReward)) p.cosmetics.Add(ChapterReward);
        }

        public static RaceCup ForEvent(string id) => Array.Find(All, c => Array.IndexOf(c.Events, id) >= 0);

        // Cosmetic ownership is already a monotonic union in cloud saves; no duplicate coin payouts.
        public static bool GrantCompleted(ProgressData progress)
        {
            if (progress == null) return false;
            progress.cosmetics ??= new System.Collections.Generic.List<string>();
            bool changed = false;
            foreach (var cup in All)
                if (cup.Complete(progress) && !progress.cosmetics.Contains(cup.Reward))
                { progress.cosmetics.Add(cup.Reward); changed = true; }
            return changed;
        }
    }
}
