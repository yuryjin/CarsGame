using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using RacingGame.Platform;
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
        public static event Action<string> LoadFailed;

        private static readonly HashSet<string> missingLogged = new HashSet<string>();

        /// <summary>Pick the locale and load its table. Waits for the localization system to initialise
        /// (never blocks: WebGL cannot WaitForCompletion). Safe to call more than once.</summary>
        public static void Initialize(string platformLanguage, string userOverride, Action<bool> completed = null)
        {
            int request = ++loadGeneration;
            var init = LocalizationSettings.InitializationOperation;
            void Proceed()
            {
                if (request != loadGeneration) return;
                if (!init.IsValid() || init.Status != AsyncOperationStatus.Succeeded)
                { NotifyFailure(userOverride ?? platformLanguage, completed); return; }
                var supported = new List<string>();
                var locales = LocalizationSettings.AvailableLocales;
                if (locales != null) foreach (var l in locales.Locales) supported.Add(l.Identifier.Code.ToLowerInvariant());
                if (supported.Count == 0) supported.Add("en");
                SetLanguage(LanguageResolver.Resolve(platformLanguage, userOverride, supported), completed);
            }
            if (init.IsDone) Proceed(); else init.Completed += _ => Proceed();
        }

        private static int loadGeneration;
        private static StringTable loadedTable;
        // SelectedLocale releases the database's tables. Own one reference to keep the last
        // working translation alive until its replacement has actually arrived.
        private static AsyncOperationHandle<StringTable> loadedHandle;
        private static Locale loadedLocale;

        public static void SetLanguage(string code, Action<bool> completed = null)
        {
            code = LanguageResolver.Normalize(code);
            var locales = LocalizationSettings.AvailableLocales;
            Locale target = null;
            if (locales != null) foreach (var l in locales.Locales) if (l.Identifier.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) { target = l; break; }
            if (target == null) { NotifyFailure(code, completed); return; }
            int generation = ++loadGeneration;
            if (LocalizationSettings.SelectedLocale != target) LocalizationSettings.SelectedLocale = target;

            var op = LocalizationSettings.StringDatabase.GetTableAsync(Table, target);
            void Done()
            {
                if (generation != loadGeneration) return;
                if (!op.IsValid() || op.Status != AsyncOperationStatus.Succeeded || op.Result == null)
                {
                    // Restoring the selected locale also clears failed cached operations, so
                    // choosing the same language again can make a fresh download attempt.
                    if (Ready && loadedLocale != null && LocalizationSettings.SelectedLocale != loadedLocale)
                        LocalizationSettings.SelectedLocale = loadedLocale;
                    else
                        LocalizationSettings.StringDatabase.ReleaseAllTables();
                    NotifyFailure(code, completed);
                    return;
                }
                var previous = loadedHandle;
                loadedHandle = Addressables.ResourceManager.Acquire(op);
                loadedTable = op.Result;
                loadedLocale = target;
                Language = code;
                Ready = true;
                if (previous.IsValid()) Addressables.Release(previous);
                completed?.Invoke(true);
                LanguageChanged?.Invoke();
            }
            if (op.IsDone) Done(); else op.Completed += _ => Done();
        }

        private static void NotifyFailure(string code, Action<bool> completed)
        {
            Debug.LogWarning("[Loc] table load failed for " + code);
            completed?.Invoke(false);
            LoadFailed?.Invoke(code);
            if (!Ready) PlatformRuntime.ReportBootFailure("localization: " + code);
        }

        public static string Get(string key)
        {
            return TryGet(key, out var value) ? value : Fallback(key);
        }

        /// <summary>Optional lookup, for example a literal name that may have a translation.
        /// Unlike Get, an absent entry does not emit a missing-key warning or a placeholder.</summary>
        public static bool TryGet(string key, out string value)
        {
            value = null;
            if (!Ready || string.IsNullOrEmpty(key)) return false;
            var entry = loadedTable?.GetEntry(key);
            if (entry == null) return false;
            value = DrivingBindings.Hint(entry.GetLocalizedString());
            return true;
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
