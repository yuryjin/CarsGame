using UnityEngine;
using RacingGame.Core.Platform;

namespace RacingGame
{
    /// <summary>Short decaying shake applied after the follow camera has placed itself (runs later in LateUpdate).</summary>
    [DefaultExecutionOrder(50)]
    public sealed class CameraImpulse : MonoBehaviour
    {
        public float decay = 6f;
        public float amplitude = 0.35f;
        public float rollDegrees = 2.5f;
        private float energy;
        private float seed;
        private Vector3 lastOffset;
        private Quaternion lastRoll = Quaternion.identity;

        [Tooltip("Shake scale while the on-screen touch controls are shown (small screens need less).")]
        public float touchScale = 0.6f;

        /// <summary>Current impulse energy (test hook).</summary>
        public float Energy => energy;

        public void AddImpulse(float strength)
        {
            if (!isActiveAndEnabled || PauseController.IsPaused) return;
            var save = PlatformGlue.Save;
            if (save != null && !save.Data.settings.cameraShake) return;   // accessibility setting
            if (TouchControlsPresenter.IsShown) strength *= touchScale;
            energy = Mathf.Clamp01(energy + strength); seed = Random.value * 100f;
        }

        /// <summary>Clears all active shake energy immediately.</summary>
        public void ResetEnergy()
        {
            energy = 0f;
            RemoveOffset();
        }

        private void RemoveOffset()
        {
            transform.position -= lastOffset;
            transform.rotation *= Quaternion.Inverse(lastRoll);
            lastOffset = Vector3.zero;
            lastRoll = Quaternion.identity;
        }

        private void OnDisable() => ResetEnergy();

        private void Update()
        {
            // All Updates run before the follow camera's LateUpdate. Removing the previous
            // offset here keeps shake out of its smoothing, aim direction and horizon roll.
            RemoveOffset();
            if (PauseController.IsPaused || (PlatformGlue.Save != null && !PlatformGlue.Save.Data.settings.cameraShake))
                energy = 0f;
        }

        private void LateUpdate()
        {
            if (energy <= 0.001f || PauseController.IsPaused) return;
            float t = Time.time * 28f + seed;
            float e = energy * energy;
            lastOffset = transform.right * (Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * amplitude * e + transform.up * (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * amplitude * e;
            lastRoll = Quaternion.AngleAxis((Mathf.PerlinNoise(t * 0.7f, 5f) - 0.5f) * 2f * rollDegrees * e, Vector3.forward);
            transform.position += lastOffset;
            transform.rotation = transform.rotation * lastRoll;
            energy = Mathf.Max(0f, energy - decay * Time.deltaTime * (0.35f + energy));
        }
    }
}
