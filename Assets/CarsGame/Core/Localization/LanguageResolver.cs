using System;
using System.Collections.Generic;

namespace RacingGame.Core.Localization
{
    /// <summary>
    /// Picks the UI language: an explicit user override wins if supported, then the platform
    /// language, then Yandex's documented fallbacks (be/kk/uk/uz → ru, everything else → en).
    /// </summary>
    public static class LanguageResolver
    {
        public static readonly string[] YandexLanguages =
        {
            "ar","az","be","bg","ca","cs","de","en","es","fa","fr","he","hi","hu","hy","id","it","ja","ka","kk",
            "nl","pl","pt","ro","ru","sk","sr","th","tk","tr","uk","uz","vi","zh"
        };
        private static readonly HashSet<string> RussianFallback = new HashSet<string> { "be", "kk", "uk", "uz" };

        public static string Normalize(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            code = code.Trim().ToLowerInvariant();
            int dash = code.IndexOfAny(new[] { '-', '_' });
            return dash > 0 ? code.Substring(0, dash) : code;
        }

        public static string Resolve(string platformLanguage, string userOverride, ICollection<string> supported)
        {
            if (supported == null || supported.Count == 0) return "en";
            string o = Normalize(userOverride);
            if (o.Length > 0 && supported.Contains(o)) return o;
            string p = Normalize(platformLanguage);
            if (p.Length > 0 && supported.Contains(p)) return p;
            if (RussianFallback.Contains(p) && supported.Contains("ru")) return "ru";
            return supported.Contains("en") ? "en" : First(supported);
        }

        private static string First(ICollection<string> c) { foreach (var s in c) return s; return "en"; }

        public static bool IsRightToLeft(string code) { code = Normalize(code); return code == "ar" || code == "he" || code == "fa"; }
    }
}
