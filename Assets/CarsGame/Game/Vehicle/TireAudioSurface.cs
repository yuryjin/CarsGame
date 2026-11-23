using UnityEngine;

namespace RacingGame
{
    public enum TireSurface { Asphalt, Sand, Bridge }

    // Some driving surfaces share the Track physics layer but sound different under the wheels.
    public sealed class TireAudioSurface : MonoBehaviour
    {
        public TireSurface surface;
    }
}
