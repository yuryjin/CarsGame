using UnityEngine;
using UnityEngine.Scripting;
using RacingGame.Core.Platform;

namespace RacingGame.Platform
{
    /// <summary>
    /// Chooses the platform implementation at boot from build-time defines (one host SDK per
    /// store build; no runtime sniffing) and installs it into <see cref="PlatformRuntime"/>.
    /// </summary>
    [Preserve]
    public static class PlatformBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (PlatformRuntime.Current != null) return;
            IPlatformServices impl;
#if UNITY_EDITOR
            impl = new Mock.EditorMockPlatform();
#elif UNITY_WEBGL && PLATFORM_YANDEX
            impl = new Yandex.YandexPlatform();
#elif UNITY_WEBGL
            impl = new Mock.WebMockPlatform();
#else
            impl = new Mock.StandalonePlatform();
#endif
            PlatformRuntime.Install(impl);
            _ = impl.InitializeAsync();
        }
    }
}
