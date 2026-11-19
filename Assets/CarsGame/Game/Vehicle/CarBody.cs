using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using RacingGame.Core.Economy;
using Slot = RacingGame.BodyMesh.Slot;

namespace RacingGame
{
    /// <summary>
    /// Turns a freshly spawned RaceCar into one of the <see cref="CarBodies"/>: mass, handling and hit points,
    /// the hull collider, rolling spheres and visuals. The selected model style only changes the visuals.
    /// Apply right after Instantiate and before the drivers, upgrades and paint: upgrades multiply on top.
    /// The standard body keeps the stock driving characteristics.
    /// </summary>
    public sealed class CarBody : MonoBehaviour
    {
        public string BodyId { get; private set; } = CarBodies.DefaultId;
        public BodyClass Class { get; private set; } = BodyClass.Sport;
        /// <summary>
        /// Factor on the AI's planned corner-entry deceleration: 1 / √mass for bodies heavier than the standard car.
        /// Measured on e1 (2 solo laps, autopilot): Bulwark 57/73 s → 58/51 s, Mammoth 59/118 s → 60/62 s at a 4.5 m/s² plan.
        /// </summary>
        public float AiBrakePlan { get; private set; } = 1f;

        private readonly List<Transform> extraSpinners = new List<Transform>();
        private readonly List<Transform> alloyWheels = new List<Transform>();
        private CarController car;
        private Transform classicHull, newHull;
        private bool initialized;

        public static CarBody Apply(GameObject go, in BodyDef def)
        {
            var body = go.GetComponent<CarBody>() ?? go.AddComponent<CarBody>();
            if (body.initialized && body.BodyId == def.Id) return body;
            if (body.newHull != null)
            {
                body.newHull.gameObject.SetActive(false);
                Destroy(body.newHull.gameObject);
                body.newHull = null;
            }
            body.initialized = true;
            body.BodyId = def.Id;
            body.Class = def.Class;
            body.AiBrakePlan = 1f / Mathf.Sqrt(Mathf.Max(1f, def.Stats.Mass));
            body.car = go.GetComponent<CarController>();
            if (!def.IsStandard) ApplyPhysics(go, def);
            var model = CarBodyModels.Get(def);
            body.ApplyVisuals(model, def);
            body.SetModelStyle(PlatformGlue.ClassicCarModels);
            if (def.IsStandard) return body;

            var health = go.GetComponent<CarHealth>();
            if (health != null) health.SetMaxHealth(health.maxHealth * def.Stats.Health, true);
            var cannon = go.GetComponent<CarCannon>();
            if (cannon != null) cannon.turretYawLimit *= def.Stats.TurretArc;
            // Grass is a legal line for tracks and long-travel buggies: the lost-car reset waits longer for them.
            var participant = go.GetComponent<RaceParticipant>();
            if (participant != null) participant.offTrackResetSeconds *= Mathf.Max(1f, def.Stats.Offroad);
            return body;
        }

        // ---------------------------------------------------------------- physics
        private static void ApplyPhysics(GameObject go, in BodyDef def)
        {
            var st = def.Stats; var shape = def.Shape; var std = BodyShape.Standard;
            var rb = go.GetComponent<Rigidbody>();
            var car = go.GetComponent<CarController>();

            // PhysX recomputes the yaw inertia from the new mass and colliders (read right after Instantiate it is still
            // stale, so it is never copied). It grows about with mass × footprint; the steering torque grows by the same
            // factor times Agility, so the turn rate a driver gets is the body's Agility while hits and shoves still meet
            // the full inertia. The factor comes from the data alone, so it is the same on every machine.
            float footprint = (shape.Width * shape.Width + shape.Length * shape.Length) / (std.Width * std.Width + std.Length * std.Length);
            float yawScale = st.Mass * footprint;
            if (rb != null)
            {
                rb.mass *= st.Mass;
                rb.ResetInertiaTensor();
            }

            if (car != null)
            {
                car.maxSpeed *= st.TopSpeed;
                car.motorTorque *= st.Mass * st.Accel;       // forces, so they scale with mass to keep the accelerations
                car.reverseTorque *= st.Mass * st.Accel;
                car.brakeForce *= st.Mass * st.Brake;
                car.downforce *= st.Mass;
                car.grip *= st.Grip;
                car.maxSteerAngle *= st.Steer;
                car.steerTorqueScale *= yawScale * st.Agility;
                car.driftYawTorque *= yawScale;
                car.offTrackDeceleration /= Mathf.Max(0.1f, st.Offroad * st.Offroad);
                car.offTrackMaxSpeedKmh *= st.Offroad;
            }

            var box = go.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.center = new Vector3(0f, shape.Clearance + shape.Height * 0.5f, def.Class == BodyClass.Sport ? 0.05f * shape.Length / std.Length : 0f);
                box.size = new Vector3(shape.Width, shape.Height, shape.Length);
            }

            // Rolling spheres (built by CarController.Awake as "<wheel>_Collider") and the wheel pivots.
            if (car != null)
            {
                Place(go.transform, car.frontLeftWheel, -shape.HalfTrack, shape.HalfBase, shape.WheelRadius);
                Place(go.transform, car.frontRightWheel, shape.HalfTrack, shape.HalfBase, shape.WheelRadius);
                Place(go.transform, car.rearLeftWheel, -shape.HalfTrack, -shape.HalfBase, shape.WheelRadius);
                Place(go.transform, car.rearRightWheel, shape.HalfTrack, -shape.HalfBase, shape.WheelRadius);
                car.wheelRadius = shape.WheelRadius;
            }
        }

        private static void Place(Transform root, Transform wheel, float x, float z, float radius)
        {
            if (wheel == null) return;
            var pos = new Vector3(x, radius, z);
            wheel.localPosition = pos;
            wheel.localScale = Vector3.one * (radius / BodyShape.Standard.WheelRadius);
            var collider = root.Find(wheel.name + "_Collider");
            if (collider == null) return;
            collider.localPosition = pos;
            var sphere = collider.GetComponent<SphereCollider>();
            if (sphere != null) sphere.radius = radius;
        }

        // ---------------------------------------------------------------- visuals
        private void ApplyVisuals(BodyModel model, in BodyDef def)
        {
            var mats = new Materials(gameObject);
            var visuals = transform.Find("Car_Visuals");
            int layer = gameObject.layer;

            if (model.KeepStockVisuals)
            {
                classicHull = visuals;
                if (classicHull != null) classicHull.localScale = model.VisualScale;
            }
            else
            {
                if (visuals != null) visuals.gameObject.SetActive(false);
                classicHull = new GameObject("ClassicHull").transform;
                classicHull.SetParent(transform, false);
                Part(classicHull, "Body_Paint", model.Paint, new[] { mats.Paint }, true, layer);
                Part(classicHull, "Body_Trim", model.Trim, mats.For(model.TrimSlots), false, layer);
            }

            var kitPaint = Part(transform, "Kit_Paint", model.KitPaint, new[] { mats.Paint }, true, layer);
            var kitTrim = Part(transform, "Kit_Trim", model.KitTrim, mats.For(model.KitTrimSlots), false, layer);
            if (kitPaint != null) kitPaint.transform.localScale = model.VisualScale;
            if (kitTrim != null) kitTrim.transform.localScale = model.VisualScale;
            if (def.IsStandard) return; // Its turret, wheels and colliders already come from the prefab.

            if (model.HideWheels && car != null)
                foreach (var w in new[] { car.frontLeftWheel, car.frontRightWheel, car.rearLeftWheel, car.rearRightWheel })
                    if (w != null) foreach (var r in w.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

            foreach (float z in model.ExtraAxles) AddExtraAxle(z, def.Shape);

            var cannon = GetComponent<CarCannon>();
            if (cannon != null && cannon.barrel != null)
            {
                var turret = cannon.turret != null ? cannon.turret : cannon.barrel.parent;
                if (turret != null && turret != transform)
                {
                    turret.localPosition = model.TurretPosition;
                    if (model.HideStockTurret)
                    {
                        foreach (Transform child in turret)
                            if (child != cannon.barrel && child.GetComponent<Renderer>() != null) child.GetComponent<Renderer>().enabled = false;
                        Part(turret, "Turret_Paint", model.TurretPaint, new[] { mats.Paint }, true, layer);
                        Part(turret, "Turret_Trim", model.TurretTrim, mats.For(model.TurretTrimSlots), false, layer);
                    }
                }
                cannon.barrel.localScale = model.BarrelScale;
                if (model.HideStockBarrel)
                {
                    foreach (var r in cannon.barrel.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                    Part(cannon.barrel, "Barrel_Twin", model.Barrel, mats.For(model.BarrelSlots), false, layer);
                }
            }
        }

        internal void SetModelStyle(bool classic)
        {
            if (classicHull == null) return;
            if (!classic && newHull == null)
            {
                var prefab = Resources.Load<GameObject>("Art/TripoCars/" + Class);
                if (prefab != null)
                {
                    var def = CarBodies.Get(BodyId);
                    var s = def.Shape;
                    newHull = Instantiate(prefab, transform, false).transform;
                    newHull.name = "Tripo_" + Class;
                    // FBX hulls occupy 2 × 1 × 4 metres, centred over their base.
                    float bottom = Class == BodyClass.Tank ? 0.42f : s.Clearance;
                    float roof = CarBodyModels.Get(def).TurretPosition.y - 0.05f;
                    float width = Class == BodyClass.Buggy ? s.Width * 0.88f : s.Width;
                    newHull.localScale = new Vector3(width / 2f, roof - bottom, s.Length / 4f);
                    newHull.localPosition = Vector3.up * bottom;
                    foreach (var r in newHull.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        r.gameObject.layer = gameObject.layer;
                        r.shadowCastingMode = r.sharedMaterial != null && r.sharedMaterial.name.Contains("CarPaint")
                            ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        r.lightProbeUsage = LightProbeUsage.Off;
                        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    }
                }
            }
            bool showClassic = classic || newHull == null;
            classicHull.gameObject.SetActive(showClassic);
            if (newHull != null) newHull.gameObject.SetActive(!showClassic);
            (showClassic ? classicHull : newHull).SetAsFirstSibling();
            SetWheelStyle(showClassic);
            GetComponent<CarPaint>()?.Refresh();
            GetComponent<CarLivery>()?.RefreshBody();
            GetComponent<CarHealth>()?.RefreshBodyRenderers();
        }

        private void SetWheelStyle(bool classic)
        {
            if (Class == BodyClass.Tank || car == null) return;
            if (!classic && alloyWheels.Count == 0)
            {
                var prefab = Resources.Load<GameObject>("Art/TripoCars/Wheel");
                if (prefab == null) return;
                foreach (var pivot in new[] { car.frontLeftWheel, car.frontRightWheel, car.rearLeftWheel, car.rearRightWheel })
                    if (pivot != null && pivot.childCount > 0) AddWheel(pivot.GetChild(0), prefab);
                foreach (var spinner in extraSpinners) AddWheel(spinner, prefab);
            }
            foreach (var wheel in alloyWheels)
                foreach (Transform visual in wheel.parent)
                    visual.gameObject.SetActive(visual == wheel ? !classic : classic);
        }

        private void AddWheel(Transform spinner, GameObject prefab)
        {
            var wheel = Instantiate(prefab, spinner, false).transform;
            wheel.name = "AlloyWheel";
            if (spinner.parent.localPosition.x < 0f) wheel.Rotate(Vector3.up, 180f, Space.Self);
            foreach (var renderer in wheel.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.gameObject.layer = gameObject.layer;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            alloyWheels.Add(wheel);
        }

        private static MeshRenderer Part(Transform parent, string name, Mesh mesh, Material[] materials, bool castShadows, int layer)
        {
            if (mesh == null) return null;
            var go = new GameObject(name) { layer = layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            // Mobile budget: only the hull casts a shadow, like the stock body.
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = castShadows;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return r;
        }

        // A visual-only axle: copies of the rear wheels' models, spun by this component.
        private void AddExtraAxle(float z, BodyShape shape)
        {
            if (car == null) return;
            foreach (var src in new[] { car.rearLeftWheel, car.rearRightWheel })
            {
                if (src == null) continue;
                var copy = Instantiate(src.gameObject, transform).transform;
                copy.name = src.name + "_Mid";
                copy.localPosition = new Vector3(src.localPosition.x, shape.WheelRadius, z);
                copy.localRotation = src.localRotation;
                copy.localScale = src.localScale;
                foreach (var col in copy.GetComponentsInChildren<Collider>(true)) Destroy(col);
                if (copy.childCount > 0) extraSpinners.Add(copy.GetChild(0));
            }
            enabled = extraSpinners.Count > 0;
        }

        private void Awake() { enabled = false; }

        private void Update()
        {
            if (car == null || extraSpinners.Count == 0) return;
            // Same spin as CarController.UpdateWheelVisuals.
            float degrees = car.SpeedKmh / 3.6f * Time.deltaTime * Mathf.Rad2Deg / 0.35f;
            for (int i = 0; i < extraSpinners.Count; i++) if (extraSpinners[i] != null) extraSpinners[i].Rotate(Vector3.right, degrees, Space.Self);
        }

        /// <summary>The prefab's own materials, looked up by name, so bodies need no extra assets.</summary>
        private readonly struct Materials
        {
            public readonly Material Paint, Dark, Glass, Head, Tail, Metal, Tire;

            public Materials(GameObject car)
            {
                Material paint = null, dark = null, glass = null, head = null, tail = null, metal = null, tire = null;
                foreach (var r in car.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m == null) continue;
                        string n = m.name;
                        if (paint == null && n.Contains("CarPaint")) paint = m;
                        else if (dark == null && n.Contains("Carbon")) dark = m;
                        else if (glass == null && n.Contains("Glass")) glass = m;
                        else if (head == null && n.Contains("Headlight")) head = m;
                        else if (tail == null && n.Contains("Taillight")) tail = m;
                        else if (metal == null && n.Contains("Rim")) metal = m;
                        else if (tire == null && n.Contains("Tire")) tire = m;
                    }
                Paint = paint; Dark = dark ?? paint; Glass = glass ?? dark ?? paint; Head = head ?? glass ?? paint;
                Tail = tail ?? head ?? paint; Metal = metal ?? dark ?? paint; Tire = tire ?? dark ?? paint;
            }

            public Material For(Slot s)
            {
                switch (s)
                {
                    case Slot.Paint: return Paint;
                    case Slot.Glass: return Glass;
                    case Slot.Head: return Head;
                    case Slot.Tail: return Tail;
                    case Slot.Metal: return Metal;
                    case Slot.Tire: return Tire;
                    default: return Dark;
                }
            }

            public Material[] For(Slot[] slots)
            {
                if (slots == null) return new Material[0];
                var list = new Material[slots.Length];
                for (int i = 0; i < slots.Length; i++) list[i] = For(slots[i]);
                return list;
            }
        }
    }
}
