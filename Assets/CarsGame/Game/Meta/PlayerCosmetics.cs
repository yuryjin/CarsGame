using UnityEngine;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;

namespace RacingGame
{
    /// <summary>Puts the selected paint and livery of the save on a car (the player's race car).</summary>
    public static class PlayerCosmetics
    {
        public static Color PaintColor(string id)
        {
            var p = Cosmetics.Paint(string.IsNullOrEmpty(id) ? Cosmetics.DefaultPaint : id);
            return new Color(p.R, p.G, p.B);
        }

        /// <summary>Selected stripe colour: a chosen paint, else automatic contrast against the body.</summary>
        public static Color StripeColor(ProgressData progress)
        {
            if (progress != null && !string.IsNullOrEmpty(progress.stripe) && Cosmetics.OwnsPaint(progress.cosmetics, progress.stripe)) return PaintColor(progress.stripe);
            var body = PaintColor(SelectedPaint(progress));
            Cosmetics.ContrastStripe(body.r, body.g, body.b, out float r, out float g, out float b);
            return new Color(r, g, b);
        }

        /// <summary>Selected ids fall back to the defaults when not owned (e.g. a stale selection from another device).</summary>
        public static string SelectedPaint(ProgressData progress) =>
            progress != null && !string.IsNullOrEmpty(progress.paint) && Cosmetics.OwnsPaint(progress.cosmetics, progress.paint) ? progress.paint : Cosmetics.DefaultPaint;

        public static string SelectedLivery(ProgressData progress) =>
            progress != null && !string.IsNullOrEmpty(progress.livery) && Cosmetics.OwnsLivery(progress.cosmetics, progress.livery) ? progress.livery : Cosmetics.DefaultLivery;

        public static void Apply(GameObject car, ProgressData progress)
        {
            if (car == null) return;
            var paint = car.GetComponent<CarPaint>();
            if (paint != null) paint.SetColor(PaintColor(SelectedPaint(progress)));
            var pattern = Cosmetics.Livery(SelectedLivery(progress)).Pattern;
            var livery = car.GetComponent<CarLivery>();
            if (livery == null && pattern == LiveryPattern.None) return;
            if (livery == null) livery = car.AddComponent<CarLivery>();
            livery.Apply(pattern, StripeColor(progress));
        }
    }
}
