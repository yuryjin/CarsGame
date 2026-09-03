using UnityEngine;

namespace RacingGame.World
{
    /// <summary>Serialized references to runtime-created effect materials (keeps their shaders in the build; no Shader.Find).</summary>
    public sealed class WorldAssets : MonoBehaviour
    {
        public Material dustMaterial;
        public static WorldAssets Instance { get; private set; }
        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
