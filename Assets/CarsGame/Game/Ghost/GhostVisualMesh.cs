using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RacingGame.Core.Economy;

namespace RacingGame
{
    /// <summary>One shared visual mesh per prefab/body; a ghost needs one transparent draw, not one per original material slot.</summary>
    internal static class GhostVisualMesh
    {
        private static readonly Dictionary<(GameObject prefab, string body), Mesh> cache = new Dictionary<(GameObject, string), Mesh>();

        internal static Mesh Get(GameObject prefab, string bodyId)
        {
            var body = CarBodies.Get(bodyId); // Canonical catalogue ID also bounds the cache for unknown remote IDs.
            var key = (prefab, body.Id);
            if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
            // Never activate the template: its gameplay components must not register with the live race.
            var root = new GameObject("GhostTemplate");
            root.SetActive(false);
            try
            {
                var template = Object.Instantiate(prefab, root.transform, false);
                template.GetComponent<CarCannon>()?.EnsureTurretVisuals();
                CarBody.Apply(template, body);
                var parts = new List<CombineInstance>();
                int vertices = 0;
                foreach (var filter in template.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                    bool visible = true;
                    for (var parent = filter.transform; parent != template.transform; parent = parent.parent)
                        if (!parent.gameObject.activeSelf) { visible = false; break; }
                    if (!visible) continue;
                    var pose = template.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                    {
                        parts.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = submesh, transform = pose });
                        vertices += filter.sharedMesh.vertexCount;
                    }
                }
                var mesh = new Mesh { name = "GhostVisual_" + body.Id,
                    indexFormat = vertices > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.CombineMeshes(parts.ToArray(), true, true);
                mesh.RecalculateBounds();
                cache[key] = mesh;
                return mesh;
            }
            finally { Object.Destroy(root); }
        }
    }
}
