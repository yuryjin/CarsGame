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

        public void Apply() { ApplyColor(color); }

        private void ApplyColor(Color c)
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (paintRenderers.Count == 0) Collect();
            block.Clear();
            block.SetColor(BaseColorId, c);
            block.SetColor(ColorId, c);
            for (int i = 0; i < paintRenderers.Count; i++) if (paintRenderers[i] != null) paintRenderers[i].SetPropertyBlock(block);
        }

        private float flashUntil, flashLength;
        private Color flashColor;

        /// <summary>Hit feedback: the body blinks towards <paramref name="c"/> and fades back to the paint colour.</summary>
        public void Flash(float seconds, Color c)
        {
            flashLength = Mathf.Max(0.02f, seconds);
            flashUntil = Time.unscaledTime + flashLength;
            flashColor = c;
            enabled = true;
            ApplyColor(c);
        }

        /// <summary>Stop a running flash without repainting (the wreck tint takes the renderers over).</summary>
        public void CancelFlash() { flashUntil = 0f; enabled = false; }

        public bool IsFlashing => enabled && Time.unscaledTime < flashUntil;

        private void Start() { if (flashUntil <= 0f) enabled = false; }   // Update only runs during a flash

        private void Update()
        {
            float left = flashUntil - Time.unscaledTime;
            if (left <= 0f) { ApplyColor(color); enabled = false; return; }
            ApplyColor(Color.Lerp(color, flashColor, left / flashLength));
        }
    }
}
