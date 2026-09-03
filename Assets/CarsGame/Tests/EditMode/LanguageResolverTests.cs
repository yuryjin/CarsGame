using NUnit.Framework;
using RacingGame.Core.Localization;

namespace RacingGame.Tests
{
    public class LanguageResolverTests
    {
        private static readonly string[] Supported = { "ru", "en", "tr" };

        [Test] public void OverrideWinsWhenSupported() => Assert.AreEqual("tr", LanguageResolver.Resolve("ru", "tr", Supported));
        [Test] public void UnsupportedOverrideFallsToPlatform() => Assert.AreEqual("ru", LanguageResolver.Resolve("ru", "zh", Supported));
        [Test] public void CisLanguagesFallToRussian() { foreach (var c in new[] { "be", "kk", "uk", "uz" }) Assert.AreEqual("ru", LanguageResolver.Resolve(c, "", Supported), c); }
        [Test] public void OthersFallToEnglish() => Assert.AreEqual("en", LanguageResolver.Resolve("ja", null, Supported));
        [Test] public void RegionTagsAreStripped() => Assert.AreEqual("ru", LanguageResolver.Resolve("ru-RU", "", Supported));
        [Test] public void Rtl() { Assert.IsTrue(LanguageResolver.IsRightToLeft("ar")); Assert.IsFalse(LanguageResolver.IsRightToLeft("ru")); }
        [Test] public void YandexListHas34() => Assert.AreEqual(34, LanguageResolver.YandexLanguages.Length);
    }
}
