using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using RacingGame.Core.Localization;

namespace RacingGame
{
    /// <summary>
    /// Thin facade over com.unity.localization: resolves the language (user override → platform →
    /// Yandex fallbacks), waits for initialization once, then serves strings synchronously from the
    /// preloaded "Game" table. Missing keys show a neutral placeholder and log once, so raw keys never reach players.
    /// </summary>
    public static class Loc
    {
        public const string Table = "Game";
        public static bool Ready { get; private set; }
        public static string Language { get; private set; } = "en";
        public static bool RightToLeft => LanguageResolver.IsRightToLeft(Language);
        public static event Action LanguageChanged;

        private static readonly HashSet<string> missingLogged = new HashSet<string>();
        private static readonly Dictionary<string, object> args = new Dictionary<string, object>(4);

        /// <summary>Pick the locale and load its table. Waits for the localization system to initialise
        /// (never blocks: WebGL cannot WaitForCompletion). Safe to call more than once.</summary>
        public static void Initialize(string platformLanguage, string userOverride)
        {
            var init = LocalizationSettings.InitializationOperation;
            void Proceed()
            {
                var supported = new List<string>();
                var locales = LocalizationSettings.AvailableLocales;
                if (locales != null) foreach (var l in locales.Locales) supported.Add(l.Identifier.Code.ToLowerInvariant());
                if (supported.Count == 0) supported.Add("en");
                SetLanguage(LanguageResolver.Resolve(platformLanguage, userOverride, supported));
            }
            if (init.IsDone) Proceed(); else init.Completed += _ => Proceed();
        }

        private static int loadGeneration;
        private static StringTable loadedTable;

        public static void SetLanguage(string code)
        {
            code = LanguageResolver.Normalize(code);
            var locales = LocalizationSettings.AvailableLocales;
            Locale target = null;
            if (locales != null) foreach (var l in locales.Locales) if (l.Identifier.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) { target = l; break; }
            if (target == null) return;
            Language = code;
            if (LocalizationSettings.SelectedLocale != target) LocalizationSettings.SelectedLocale = target;

            // Ready only once THIS locale's table is in memory: a locale switch starts a new async
            // table load, and reading before it lands yields "No translation" fallbacks.
            Ready = false;
            loadedTable = null;
            int generation = ++loadGeneration;
            var op = LocalizationSettings.StringDatabase.GetTableAsync(Table, target);
            void Done()
            {
                if (generation != loadGeneration) return;
                if (op.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded || op.Result == null)
                { Debug.LogError("[Loc] table load failed for " + code + ": " + op.OperationException); return; }
                loadedTable = op.Result;
                Ready = true; LanguageChanged?.Invoke();
            }
            if (op.IsDone) Done(); else op.Completed += _ => Done();
        }

        public static string Get(string key)
        {
            if (!Ready) return Fallback(key);
            var entry = loadedTable?.GetEntry(key);
            return entry != null ? entry.GetLocalizedString() : Fallback(key);
        }

        /// <summary>Named placeholders: Format("menu.coins", "n", 120).</summary>
        public static string Format(string key, params object[] namedPairs)
        {
            string s = Get(key);
            for (int i = 0; i + 1 < namedPairs.Length; i += 2) s = s.Replace("{" + namedPairs[i] + "}", Convert.ToString(namedPairs[i + 1]));
            return s;
        }

        private static string Fallback(string key)
        {
            if (Ready && missingLogged.Add(key)) Debug.LogWarning("[Loc] missing key: " + key);
            return "…";
        }
    }
}
