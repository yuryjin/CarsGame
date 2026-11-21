using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame
{
    public enum PickupKind { Health, Ammo, Nitro, Item }

    /// <summary>
    /// Trigger pickup on the track: heals, refills ammo, gives a nitro burst or grants an item
    /// (Shield / Repair based on target HP) to the first car that drives through it.
    /// Visuals are a spinning primitive with a colour per kind.
    /// </summary>
    public sealed class Pickup : MonoBehaviour
    {
        public PickupKind kind;
        public float healAmount = 35f;
        public float nitroSeconds = 3f;
        public float itemRepairThreshold = 60f;
        public float itemRepairHeal = 40f;
        public float itemShieldDuration = 6f;
        public bool Taken { get; private set; }
        /// <summary>(pickup, car that took it) — for toasts and first-time tips.</summary>
        public static event System.Action<Pickup, CarController> PickedUp;
        public int TakenTick { get; private set; }

        private const float ArrowHeight = 2.2f; // tip height above the road; the model tops out around 1.6 m
        private Transform visual;
        private Transform arrow;
        private float bobPhase;
        private static Material[] materials;
        private static Mesh arrowMesh;

        public static Pickup Create(PickupKind kind, Vector3 position, Transform parent)
        {
            var go = new GameObject("Pickup_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            int layer = LayerMask.NameToLayer("Track");
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true; col.center = Vector3.up;
            col.size = new Vector3(1.6f, 3f, 3.6f); // long along the road, narrow enough to require choosing a lane
            var p = go.AddComponent<Pickup>();
            p.kind = kind;
            p.BuildVisual();
            return p;
        }

        private void BuildVisual()
        {
            if (PropModelLibrary.TryGetPickup(kind, out var model, out var modelMaterial))
            {
                // Imported model (Tools/art/conform_prop.py): one renderer, colours and glow in the vertices.
                visual = PropModelLibrary.Spawn(transform, "Model", model, modelMaterial);
                visual.localPosition = Vector3.up * 0.9f;
            }
            else
            {
                PrimitiveType prim = kind == PickupKind.Health ? PrimitiveType.Capsule :
                                     kind == PickupKind.Ammo ? PrimitiveType.Cube :
                                     kind == PickupKind.Nitro ? PrimitiveType.Sphere :
                                     PrimitiveType.Cylinder;
                var v = GameObject.CreatePrimitive(prim);
                Destroy(v.GetComponent<Collider>());
                v.transform.SetParent(transform, false);
                v.transform.localPosition = Vector3.up * 0.9f;
                v.transform.localScale = Vector3.one;
                var vr = v.GetComponent<Renderer>();
                vr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                vr.sharedMaterial = MaterialFor(kind);
                visual = v.transform;
            }
            // A glowing arrow pointing down at the pickup: readable from a distance and over other cars.
            var arrowGo = new GameObject("Arrow", typeof(MeshFilter), typeof(MeshRenderer));
            arrow = arrowGo.transform;
            arrow.SetParent(transform, false);
            arrow.localPosition = Vector3.up * ArrowHeight;
            arrowGo.GetComponent<MeshFilter>().sharedMesh = ArrowMesh();
            var ar = arrowGo.GetComponent<MeshRenderer>();
            ar.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ar.sharedMaterial = MaterialFor(kind);
            bobPhase = (transform.position.x + transform.position.z) * 0.37f;
        }

        /// <summary>Flat-shaded octagonal arrow, tip at the origin pointing down: cone head + shaft. Shared by all pickups.</summary>
        private static Mesh ArrowMesh()
        {
            if (arrowMesh != null) return arrowMesh;
            const int sides = 8;
            const float headRadius = 0.5f, headHeight = 0.6f, shaftRadius = 0.16f, shaftTop = 1.25f;
            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f) { var t = b; b = c; c = t; }
                tris.Add(verts.Count); verts.Add(a);
                tris.Add(verts.Count); verts.Add(b);
                tris.Add(verts.Count); verts.Add(c);
            }
            Vector3 Ring(int i, float radius, float y)
            {
                float angle = (i % sides) * Mathf.PI * 2f / sides;
                return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
            }
            for (int i = 0; i < sides; i++)
            {
                Vector3 h0 = Ring(i, headRadius, headHeight), h1 = Ring(i + 1, headRadius, headHeight);
                Vector3 radial = (h0 + h1) * 0.5f; radial.y = 0f;
                Tri(Vector3.zero, h0, h1, radial);                          // head side
                Tri(Vector3.up * headHeight, h0, h1, Vector3.up);           // head top (under the shaft)
                Vector3 s0 = Ring(i, shaftRadius, headHeight), s1 = Ring(i + 1, shaftRadius, headHeight);
                Vector3 t0 = Ring(i, shaftRadius, shaftTop), t1 = Ring(i + 1, shaftRadius, shaftTop);
                Tri(s0, s1, t1, radial); Tri(s0, t1, t0, radial);            // shaft side
                Tri(Vector3.up * shaftTop, t0, t1, Vector3.up);             // shaft cap
            }
            arrowMesh = new Mesh { name = "PickupArrow" };
            arrowMesh.SetVertices(verts);
            arrowMesh.SetTriangles(tris, 0);
            arrowMesh.RecalculateNormals();
            arrowMesh.RecalculateBounds();
            return arrowMesh;
        }

        private static Material MaterialFor(PickupKind kind)
        {
            if (materials == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                materials = new Material[4];
                var colors = new[]
                {
                    new Color(0.2f, 0.95f, 0.35f), // Health (green)
                    new Color(1f, 0.8f, 0.2f),     // Ammo (yellow)
                    new Color(0.3f, 0.6f, 1f),     // Nitro (blue)
                    new Color(0.95f, 0.45f, 0.95f) // Item (magenta/purple)
                };
                for (int i = 0; i < 4; i++)
                {
                    var m = new Material(shader) { name = "Pickup_" + (PickupKind)i };
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", colors[i]);
                    if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", colors[i] * 1.5f); }
                    materials[i] = m;
                }
            }
            return materials[(int)kind];
        }

        private void Update()
        {
            if (visual == null) return;
            visual.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);
            visual.localPosition = new Vector3(0f, 0.9f + 0.15f * Mathf.Sin(Time.time * 2.5f + bobPhase), 0f);
            if (arrow != null) arrow.localPosition = new Vector3(0f, ArrowHeight + 0.25f * Mathf.Abs(Mathf.Sin(Time.time * 3f + bobPhase)), 0f);
        }

        public void OnTriggerEnter(Collider other)
        {
            if (Taken) return;
            var car = other.GetComponentInParent<CarController>();
            if (car == null) return;
            var health = car.GetComponent<CarHealth>();
            if (health != null && health.IsDead) return;      // wrecks take nothing

            switch (kind)
            {
                case PickupKind.Health:
                {
                    if (health == null || health.Health >= health.maxHealth) return;
                    health.Heal(healAmount);
                    break;
                }
                case PickupKind.Ammo:
                {
                    var c = car.GetComponent<CarCannon>();
                    var w = car.GetComponent<CarWeapons>();
                    if (c != null) c.RefillAmmo();
                    if (w != null) w.RefillAll();
                    break;
                }
                case PickupKind.Nitro:
                {
                    car.ApplyBoost(nitroSeconds);
                    break;
                }
                case PickupKind.Item:
                {
                    var status = car.GetComponent<CarStatus>();
                    if (health != null && health.Health <= itemRepairThreshold)
                    {
                        health.Heal(itemRepairHeal);
                        if (status != null) status.ClearDebuffs();
                    }
                    else if (status != null)
                    {
                        status.ApplyStatus(StatusKind.Shield, itemShieldDuration);
                    }
                    break;
                }
            }

            Taken = true;
            TakenTick = SimulationDriver.Instance != null ? SimulationDriver.Instance.Tick : 0;
            if (EffectPool.Instance != null)
            {
                EffectPool.Instance.PlayFlash(transform.position + Vector3.up * 0.9f, new Color(0.8f, 1f, 0.9f), 0.3f, 1.6f, 5f, 3f);
            }
            if (SfxPlayer.Instance != null)
            {
                SfxPlayer.Instance.Play(SfxSynth.LapDing, transform.position, 0.6f, kind == PickupKind.Nitro ? 1.3f : 1f);
            }
            gameObject.SetActive(false);
            PickedUp?.Invoke(this, car);
        }

        /// <summary>Respawn unless a car is sitting on the spot (it would pop straight back into it).</summary>
        public bool TryRespawn()
        {
            int carLayer = LayerMask.NameToLayer("Car");
            int mask = carLayer >= 0 ? 1 << carLayer : Physics.AllLayers;
            if (Physics.CheckSphere(transform.position + Vector3.up * 0.5f, 2.5f, mask, QueryTriggerInteraction.Ignore)) return false;
            Taken = false;
            gameObject.SetActive(true);
            return true;
        }
    }
}
