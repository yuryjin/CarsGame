using System;
using UnityEngine;

namespace RacingGame
{
    /// <summary>A runtime arena with a floor, four walls and inward-facing spawn points.</summary>
    public sealed class ArenaLayout : MonoBehaviour
    {
        public const float HalfSize = 50f;

        public static ArenaLayout Create(Material material)
        {
            var arena = new GameObject("Arena").AddComponent<ArenaLayout>();
            // Keep the runtime arena clear of the circuit and its baked environment.
            arena.transform.position = new Vector3(1000f, 0f, 0f);
            arena.AddBox("Floor", new Vector3(0f, -1f, 0f), new Vector3(102f, 2f, 102f), material);
            arena.AddBox("North", new Vector3(0f, 2.5f, HalfSize), new Vector3(102f, 5f, 2f), material);
            arena.AddBox("South", new Vector3(0f, 2.5f, -HalfSize), new Vector3(102f, 5f, 2f), material);
            arena.AddBox("East", new Vector3(HalfSize, 2.5f, 0f), new Vector3(2f, 5f, 98f), material);
            arena.AddBox("West", new Vector3(-HalfSize, 2.5f, 0f), new Vector3(2f, 5f, 98f), material);
            return arena;
        }

        private void AddBox(string boxName, Vector3 position, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = boxName;
            box.transform.SetParent(transform, false);
            box.transform.localPosition = position;
            box.transform.localScale = size;
            int trackLayer = LayerMask.NameToLayer("Track");
            if (trackLayer >= 0) box.layer = trackLayer;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
        }

        public void GetSpawnPose(int slot, int total, out Vector3 position, out Quaternion rotation)
        {
            if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total));
            if (slot < 0 || slot >= total) throw new ArgumentOutOfRangeException(nameof(slot));
            float angle = slot * Mathf.PI * 2f / total;
            var radial = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle));
            position = transform.TransformPoint(radial * (HalfSize - 10f) + Vector3.up * 0.5f);
            rotation = transform.rotation * Quaternion.LookRotation(-radial, Vector3.up);
        }

        public bool TryGetRecoveryPose(CarController car, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (car == null) return false;

            int carLayer = LayerMask.NameToLayer("Car");
            int mask = carLayer >= 0 ? 1 << carLayer : Physics.AllLayers;
            Physics.SyncTransforms();
            // More candidates than the maximum eight cars, including the centre as a fallback.
            for (int i = 0; i <= 16; i++)
            {
                GetSpawnPose(i % 16, 16, out position, out rotation);
                if (i == 16) position = transform.TransformPoint(Vector3.up * 0.5f);
                var overlaps = Physics.OverlapBox(position + Vector3.up * 0.5f,
                    new Vector3(1.2f, 0.7f, 2.5f), rotation, mask, QueryTriggerInteraction.Ignore);
                bool occupied = false;
                foreach (var hit in overlaps)
                    if (!hit.transform.IsChildOf(car.transform)) { occupied = true; break; }
                if (!occupied) return true;
            }
            return false;
        }
    }
}
