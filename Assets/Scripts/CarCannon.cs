using UnityEngine;
using UnityEngine.InputSystem;

namespace RacingGame
{
    /// <summary>
    /// Roof-mounted cannon. Builds its own turret/barrel visuals at start and fires a
    /// <see cref="Projectile"/> forward on left mouse button, Ctrl or gamepad right shoulder.
    /// </summary>
    public class CarCannon : MonoBehaviour
    {
        [Header("Firing")]
        public float fireInterval = 0.4f;
        public float shellSpeed = 70f;
        public float shellDamage = 25f;
        public float recoilForce = 1500f;
        public bool playerControlled = true;

        [Header("Mount")]
        public Vector3 turretLocalPosition = new Vector3(0f, 1.12f, -0.45f);
        public float barrelLength = 1.3f;

        [Header("References (auto-created if empty)")]
        public Transform barrel;
        public Transform muzzle;

        private Rigidbody rb;
        private float nextFireTime;
        private float recoilOffset;
        private Vector3 barrelRestPosition;
        private Material shellMaterial;
        private Light muzzleLight;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (barrel == null || muzzle == null) BuildTurret();
            barrelRestPosition = barrel.localPosition;

            muzzleLight = muzzle.gameObject.AddComponent<Light>();
            muzzleLight.type = LightType.Point;
            muzzleLight.color = new Color(1f, 0.75f, 0.3f);
            muzzleLight.range = 6f;
            muzzleLight.intensity = 0f;
        }

        private void Update()
        {
            // Barrel recoil spring-back
            recoilOffset = Mathf.MoveTowards(recoilOffset, 0f, Time.deltaTime * 1.2f);
            barrel.localPosition = barrelRestPosition - Vector3.forward * recoilOffset;
            if (muzzleLight != null) muzzleLight.intensity = Mathf.MoveTowards(muzzleLight.intensity, 0f, Time.deltaTime * 40f);

            if (playerControlled && WantsToFire()) TryFire();
        }

        private static bool WantsToFire()
        {
            var k = Keyboard.current;
            if (k != null && (k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed)) return true;

            var m = Mouse.current;
            if (m != null && m.leftButton.isPressed) return true;

            var g = Gamepad.current;
            if (g != null && (g.rightShoulder.isPressed || g.buttonWest.isPressed)) return true;

            return false;
        }

        public bool TryFire()
        {
            if (Time.time < nextFireTime) return false;
            nextFireTime = Time.time + fireInterval;

            Vector3 direction = muzzle.forward;
            Vector3 inherited = rb != null ? rb.linearVelocity : Vector3.zero;
            Projectile.Spawn(gameObject, muzzle.position, direction * shellSpeed + inherited, shellDamage, shellMaterial);

            recoilOffset = 0.18f;
            if (muzzleLight != null) muzzleLight.intensity = 6f;
            if (rb != null) rb.AddForceAtPosition(-direction * recoilForce, muzzle.position, ForceMode.Impulse);

            return true;
        }

        private void BuildTurret()
        {
            Material dark = FindMaterialOnCar("carbon", "dark") ?? MakeMaterial(new Color(0.12f, 0.12f, 0.13f));
            shellMaterial = MakeMaterial(new Color(1f, 0.8f, 0.25f));

            var turret = new GameObject("Cannon_Turret").transform;
            turret.SetParent(transform, false);
            turret.localPosition = turretLocalPosition;
            turret.localRotation = Quaternion.identity;

            // Base ring on the roof
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Cannon_Base";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(turret, false);
            ring.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            ring.transform.localScale = new Vector3(0.55f, 0.05f, 0.55f);
            ring.GetComponent<Renderer>().sharedMaterial = dark;

            // Turret housing
            var housing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            housing.name = "Cannon_Housing";
            Destroy(housing.GetComponent<Collider>());
            housing.transform.SetParent(turret, false);
            housing.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            housing.transform.localScale = new Vector3(0.34f, 0.2f, 0.46f);
            housing.GetComponent<Renderer>().sharedMaterial = dark;

            // Barrel (pivot at the rear so recoil slides it back)
            var barrelPivot = new GameObject("Cannon_Barrel").transform;
            barrelPivot.SetParent(turret, false);
            barrelPivot.localPosition = new Vector3(0f, 0.1f, 0.1f);
            barrelPivot.localRotation = Quaternion.identity;

            var tube = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tube.name = "Barrel_Tube";
            Destroy(tube.GetComponent<Collider>());
            tube.transform.SetParent(barrelPivot, false);
            tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            tube.transform.localPosition = new Vector3(0f, 0f, barrelLength * 0.5f);
            tube.transform.localScale = new Vector3(0.11f, barrelLength * 0.5f, 0.11f);
            tube.GetComponent<Renderer>().sharedMaterial = dark;

            var brake = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            brake.name = "Muzzle_Brake";
            Destroy(brake.GetComponent<Collider>());
            brake.transform.SetParent(barrelPivot, false);
            brake.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            brake.transform.localPosition = new Vector3(0f, 0f, barrelLength - 0.08f);
            brake.transform.localScale = new Vector3(0.15f, 0.08f, 0.15f);
            brake.GetComponent<Renderer>().sharedMaterial = dark;

            var muzzlePoint = new GameObject("Muzzle").transform;
            muzzlePoint.SetParent(barrelPivot, false);
            muzzlePoint.localPosition = new Vector3(0f, 0f, barrelLength + 0.25f);
            muzzlePoint.localRotation = Quaternion.identity;

            barrel = barrelPivot;
            muzzle = muzzlePoint;
        }

        private Material FindMaterialOnCar(params string[] nameParts)
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var m = r.sharedMaterial;
                if (m == null) continue;
                string n = m.name.ToLowerInvariant();
                foreach (var part in nameParts)
                {
                    if (n.Contains(part)) return m;
                }
            }
            return null;
        }

        private static Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }
    }
}
