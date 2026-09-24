using System.Collections.Generic;
using UnityEngine;

namespace RacingGame
{
    /// <summary>Shared low-poly parts and materials for runtime weapon models.</summary>
    public static class WeaponArt
    {
        private static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        private static Mesh cone, banana;

        public static Color Tint(PrimaryWeaponType type)
        {
            switch (type)
            {
                case PrimaryWeaponType.GiantBanana: case PrimaryWeaponType.RubberChicken: return new Color(1f, 0.82f, 0.05f);
                case PrimaryWeaponType.Boxer: case PrimaryWeaponType.GloveLauncher: return new Color(0.95f, 0.1f, 0.08f);
                case PrimaryWeaponType.LaserSaw: case PrimaryWeaponType.Welder: case PrimaryWeaponType.CryoJet: return Color.cyan;
                case PrimaryWeaponType.Flamethrower: return new Color(1f, 0.35f, 0.05f);
                case PrimaryWeaponType.AcidSprayer: return new Color(0.45f, 1f, 0.08f);
                default: return Color.HSVToRGB(((int)type * 0.137f) % 1f, 0.65f, 0.95f);
            }
        }

        public static Material Material(Color color, bool emission = false)
        {
            // Alpha is unused on these opaque models; reserve it in the cache key for emissive variants.
            var key = color; key.a = emission ? 0.5f : 1f;
            if (materials.TryGetValue(key, out var material) && material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { name = "WeaponColor" };
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 2.5f); }
            materials[key] = material;
            return material;
        }

        public static Transform Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go.transform;
        }

        public static Transform Cone(Transform parent, string name, float radius, float length, Material material)
        {
            if (cone == null)
            {
                const int sides = 20;
                var vertices = new Vector3[sides + 2];
                var triangles = new int[sides * 6];
                vertices[sides] = Vector3.up;
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    vertices[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    int next = (i + 1) % sides;
                    triangles[i * 6] = i; triangles[i * 6 + 1] = sides; triangles[i * 6 + 2] = next;
                    triangles[i * 6 + 3] = i; triangles[i * 6 + 4] = next; triangles[i * 6 + 5] = sides + 1;
                }
                cone = new Mesh { name = "WeaponCone", vertices = vertices, triangles = triangles };
                cone.RecalculateNormals(); cone.RecalculateBounds();
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            go.transform.localScale = new Vector3(radius, length, radius);
            go.GetComponent<MeshFilter>().sharedMesh = cone;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        public static Transform Banana(Transform parent, Material material)
        {
            if (banana == null)
            {
                const int rings = 24, sides = 12;
                var vertices = new Vector3[(rings + 1) * (sides + 1)];
                var triangles = new int[rings * sides * 6];
                for (int i = 0; i <= rings; i++)
                {
                    float t = i / (float)rings;
                    float angle = Mathf.Lerp(-65f, 65f, t) * Mathf.Deg2Rad;
                    Vector3 center = new Vector3(Mathf.Sin(angle) * 1.75f, 0.45f + (1f - Mathf.Cos(angle)) * 1.2f, 0f);
                    Vector3 tangent = new Vector3(Mathf.Cos(angle) * 1.75f, Mathf.Sin(angle) * 1.2f, 0f).normalized;
                    Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
                    float radius = 0.3f * Mathf.Pow(Mathf.Sin(t * Mathf.PI), 0.35f);
                    if (i == 0 || i == rings) radius = 0f;
                    for (int j = 0; j <= sides; j++)
                    {
                        float a = j * Mathf.PI * 2f / sides;
                        vertices[i * (sides + 1) + j] = center + radius * (normal * Mathf.Cos(a) + Vector3.forward * Mathf.Sin(a));
                        if (i == rings || j == sides) continue;
                        int index = (i * sides + j) * 6;
                        int v = i * (sides + 1) + j;
                        triangles[index] = v; triangles[index + 1] = v + 1; triangles[index + 2] = v + sides + 1;
                        triangles[index + 3] = v + 1; triangles[index + 4] = v + sides + 2; triangles[index + 5] = v + sides + 1;
                    }
                }
                banana = new Mesh { name = "GiantBanana", vertices = vertices, triangles = triangles };
                banana.RecalculateNormals(); banana.RecalculateBounds();
            }
            var go = new GameObject("BananaBody", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = banana;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        public static void Link(Transform link, Vector3 from, Vector3 to, float width)
        {
            Vector3 delta = to - from;
            link.localPosition = (from + to) * 0.5f;
            link.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
            link.localScale = new Vector3(width, delta.magnitude * 0.5f, width);
        }

        public static void Destroy(Object item)
        {
            if (Application.isPlaying) Object.Destroy(item); else Object.DestroyImmediate(item);
        }
    }
}
