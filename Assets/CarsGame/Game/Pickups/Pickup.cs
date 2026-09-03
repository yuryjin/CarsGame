using UnityEngine;

namespace RacingGame
{
    public enum PickupKind { Health, Ammo, Nitro }

    /// <summary>
    /// Trigger pickup on the track: heals, refills the magazine or gives a nitro burst to the
    /// first car that drives through it, then hides until the spawner respawns it. Visuals are a
    /// spinning primitive with a colour per kind (no assets).
    /// </summary>
    public sealed class Pickup : MonoBehaviour
    {
        public PickupKind kind;
        public float healAmount = 35f;
        public float nitroSeconds = 3f;
        public bool Taken { get; private set; }
        public int TakenTick { get; private set; }

        private Transform visual;
        private static Material[] materials;

        public static Pickup Create(PickupKind kind, Vector3 position, Transform parent)
        {
            var go = new GameObject("Pickup_" + kind);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            int layer = LayerMask.NameToLayer("Track");
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true; col.radius = 1.3f;
            var p = go.AddComponent<Pickup>();
            p.kind = kind;
            p.BuildVisual();
            return p;
        }

        private void BuildVisual()
        {
            var v = GameObject.CreatePrimitive(kind == PickupKind.Health ? PrimitiveType.Capsule : kind == PickupKind.Ammo ? PrimitiveType.Cube : PrimitiveType.Sphere);
            Destroy(v.GetComponent<Collider>());
            v.transform.SetParent(transform, false);
            v.transform.localPosition = Vector3.up * 0.9f;
            v.transform.localScale = Vector3.one * 0.7f;
            var r = v.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.sharedMaterial = MaterialFor(kind);
            visual = v.transform;
        }

        // TODO(M8): serialized material assets instead of Shader.Find.
        private static Material MaterialFor(PickupKind kind)
        {
            if (materials == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                materials = new Material[3];
                var colors = new[] { new Color(0.2f, 0.95f, 0.35f), new Color(1f, 0.8f, 0.2f), new Color(0.3f, 0.6f, 1f) };
                for (int i = 0; i < 3; i++)
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
            if (visual != null) visual.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (Taken) return;
            var car = other.GetComponentInParent<CarController>();
            if (car == null) return;
            var health = car.GetComponent<CarHealth>();
            if (health != null && health.IsDead) return;      // wrecks take nothing
            switch (kind)
            {
                case PickupKind.Health: { if (health == null || health.Health >= health.maxHealth) return; health.Heal(healAmount); break; }
                case PickupKind.Ammo: { var c = car.GetComponent<CarCannon>(); if (c == null || c.AmmoMax <= 0 || (c.Ammo >= c.AmmoMax && !c.IsReloading)) return; c.RefillAmmo(); break; }
                case PickupKind.Nitro: car.ApplyBoost(nitroSeconds); break;
            }
            Taken = true;
            TakenTick = SimulationDriver.Instance.Tick;
            EffectPool.Instance.PlayFlash(transform.position + Vector3.up * 0.9f, new Color(0.8f, 1f, 0.9f), 0.3f, 1.6f, 5f, 3f);
            SfxPlayer.Instance.Play(SfxSynth.LapDing, transform.position, 0.6f, kind == PickupKind.Nitro ? 1.3f : 1f);
            gameObject.SetActive(false);
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
