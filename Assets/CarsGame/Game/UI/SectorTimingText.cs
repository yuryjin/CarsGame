using System.Globalization;
using UnityEngine;

namespace RacingGame
{
    internal static class SectorTimingText
    {
        public static string Name(TrackPath path, int sector)
        {
            if (path == null || path.Fork == null) return Loc.Format("results.sector.number", "n", sector + 1);
            int place = path.reversed ? 2 - sector : sector;
            return Loc.Get(place == 0 ? "results.sector.bridge" : place == 1 ? "results.sector.tower" : "results.sector.pits");
        }

        public static string Delta(int milliseconds, out Color color)
        {
            int tenths = Mathf.RoundToInt(milliseconds / 100f);
            string sign = tenths < 0 ? "−" : tenths > 0 ? "+" : "";
            string seconds = sign + (Mathf.Abs(tenths) / 10m).ToString("0.0", CultureInfo.GetCultureInfo(Loc.Language));
            color = tenths < 0 ? UiFactory.Positive : tenths > 0 ? UiFactory.Accent : UiFactory.Muted;
            return Loc.Format("results.sector.delta", "t", seconds);
        }
    }
}
