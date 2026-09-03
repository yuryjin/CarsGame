using RacingGame.Core.Platform;

namespace RacingGame.Platform.Mock
{
    /// <summary>WebGL build without a store define: local saves (localStorage via PlayerPrefs), no ads.</summary>
    public sealed class WebMockPlatform : StandalonePlatform
    {
        public override PlatformKind Kind => PlatformKind.WebMock;
    }
}
