using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>
    /// Per-car body colour via MaterialPropertyBlock (all cars share one paint material).
    /// Owns the property block of the paint renderers: anything else that touches those
    /// blocks (the wreck tint in <see cref="CarHealth"/>) must call <see cref="Apply"/> after.
    /// </summary>
    public sealed class CarPaint : MonoBehaviour
    {
        public Color color = new Color(0.8f, 0.1f, 0.1f);
        [Tooltip("Renderers whose shared material name contains this get the colour.")]
        public string paintMaterialNameContains = "CarPaint";

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private readonly List<Renderer> paintRenderers = new List<Renderer>();
        private MaterialPropertyBlock block;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            Collect();
            Apply();
        }

        private void Collect()
        {
            paintRenderers.Clear();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m != null && m.name.Contains(paintMaterialNameContains)) paintRenderers.Add(r);
            }
        }

        public void SetColor(Color c) { color = c; Apply(); }

        public void Apply()
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (paintRenderers.Count == 0) Collect();
            block.Clear();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            foreach (var r in paintRenderers) if (r != null) r.SetPropertyBlock(block);
        }
    }
}
