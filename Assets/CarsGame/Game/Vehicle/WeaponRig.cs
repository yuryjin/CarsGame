using UnityEngine;

namespace RacingGame
{
    /// <summary>Procedural toy mechanisms. Render-only transforms have no colliders or gameplay authority.</summary>
    public sealed class WeaponRig
    {
        public Transform Root { get; private set; }
        private readonly PrimaryWeaponType type;
        private Transform tool, shaft, torso;
        private readonly Transform[] upperArms = new Transform[2], forearms = new Transform[2], gloves = new Transform[2];
        private LineRenderer beam;
        private float firedAt = -100f, reach = 3f;
        private int punches;
        private PrimaryWeaponCatalog.Profile shot;
        private Vector3 worldEnd;

        public WeaponRig(Transform car, PrimaryWeaponType type)
        {
            this.type = type;
            shot = PrimaryWeaponCatalog.Get(type);
            Root = new GameObject("Weapon_" + type).transform;
            Root.SetParent(car, false);
            var cannon = car.GetComponent<CarCannon>();
            Root.localPosition = cannon != null && cannon.turret != null ? car.InverseTransformPoint(cannon.turret.position) : Vector3.up;
            var metal = WeaponArt.Material(new Color(0.32f, 0.38f, 0.45f));
            var color = WeaponArt.Material(WeaponArt.Tint(type));
            WeaponArt.Part(Root, "Mount", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.8f, 0.12f, 0.8f), metal);
            if (type == PrimaryWeaponType.Boxer) BuildBoxer(metal);
            else if (shot.Mode == WeaponMode.Melee) BuildTool(metal, color);
            else
            {
                WeaponArt.Part(Root, "Housing", PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f), new Vector3(0.65f, 0.5f, 0.9f), metal);
                WeaponArt.Part(Root, "Emitter", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0.6f), Vector3.one * 0.4f, color);
                beam = new GameObject("WeaponEffect").AddComponent<LineRenderer>();
                beam.transform.SetParent(Root, false);
                beam.sharedMaterial = WeaponArt.Material(WeaponArt.Tint(type), true);
                beam.startColor = beam.endColor = WeaponArt.Tint(type);
                beam.startWidth = beam.endWidth = 0.12f;
                beam.useWorldSpace = true;
                beam.enabled = false;
            }
        }

        private void BuildBoxer(Material metal)
        {
            var red = WeaponArt.Material(new Color(0.95f, 0.12f, 0.08f));
            var cream = WeaponArt.Material(new Color(1f, 0.78f, 0.42f));
            torso = new GameObject("PopUpBoxer").transform;
            torso.SetParent(Root, false);
            WeaponArt.Part(torso, "Body", PrimitiveType.Capsule, new Vector3(0f, 0.2f, 0f), new Vector3(0.65f, 0.55f, 0.5f), red);
            WeaponArt.Part(torso, "Head", PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0f), new Vector3(0.9f, 0.85f, 0.72f), cream);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                WeaponArt.Part(torso, "Eye" + i, PrimitiveType.Sphere, new Vector3(side * 0.19f, 1.02f, 0.34f), Vector3.one * 0.15f, WeaponArt.Material(Color.black));
                upperArms[i] = WeaponArt.Part(torso, "UpperLink" + i, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, metal);
                forearms[i] = WeaponArt.Part(torso, "ForearmLink" + i, PrimitiveType.Cylinder, Vector3.zero, Vector3.one, cream);
                gloves[i] = WeaponArt.Part(torso, "Glove" + i, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.65f, 0.5f, 0.65f), red);
                WeaponArt.Part(torso, "ShoulderPin" + i, PrimitiveType.Sphere, new Vector3(side * 0.45f, 0.45f, 0f), Vector3.one * 0.18f, metal);
            }
            SamplePose(100f);
        }

        private void BuildTool(Material metal, Material color)
        {
            shaft = WeaponArt.Part(Root, "ExtendingArm", PrimitiveType.Cylinder, Vector3.zero, Vector3.one, metal);
            tool = new GameObject("ToolHead").transform;
            tool.SetParent(Root, false);
            switch (type)
            {
                case PrimaryWeaponType.CircularSaw:
                case PrimaryWeaponType.LaserSaw:
                    var blade = type == PrimaryWeaponType.LaserSaw ? WeaponArt.Material(Color.cyan, true) : metal;
                    WeaponArt.Part(tool, "Disc", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.4f, 0.07f, 1.4f), blade).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    for (int i = 0; i < 16; i++)
                    {
                        float angle = i * Mathf.PI / 8f;
                        var tooth = WeaponArt.Part(tool, "Tooth" + i, PrimitiveType.Cube, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.72f, new Vector3(0.28f, 0.18f, 0.15f), blade);
                        tooth.localRotation = Quaternion.Euler(0f, 0f, i * 22.5f + 25f);
                    }
                    break;
                case PrimaryWeaponType.Drill:
                case PrimaryWeaponType.SpringSpear:
                    WeaponArt.Cone(tool, "Point", 0.5f, 1.7f, metal).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    for (int i = 0; i < 5; i++)
                        WeaponArt.Part(tool, "Thread" + i, PrimitiveType.Cylinder, new Vector3(0f, 0f, i * 0.28f), new Vector3(0.9f - i * 0.14f, 0.045f, 0.9f - i * 0.14f), color).localRotation = Quaternion.Euler(90f, 0f, 15f);
                    break;
                case PrimaryWeaponType.Sledgehammer:
                    WeaponArt.Part(tool, "Hammer", PrimitiveType.Cube, Vector3.zero, new Vector3(1.7f, 0.85f, 0.8f), metal);
                    WeaponArt.Part(tool, "Stripe", PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.89f, 0.84f), color);
                    break;
                case PrimaryWeaponType.BattleAxe:
                    WeaponArt.Part(tool, "AxeHub", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.22f, 0.7f, 0.22f), metal);
                    for (int i = -1; i <= 1; i += 2)
                        WeaponArt.Part(tool, "AxeBlade", PrimitiveType.Cube, new Vector3(i * 0.55f, 0f, 0f), new Vector3(0.85f, 1.25f, 0.12f), metal).localRotation = Quaternion.Euler(0f, 0f, i * 25f);
                    break;
                case PrimaryWeaponType.Chainsaw:
                    WeaponArt.Part(tool, "Blade", PrimitiveType.Cube, new Vector3(0f, 0f, 0.3f), new Vector3(0.55f, 0.14f, 1.8f), metal);
                    for (int i = 0; i < 8; i++)
                        for (int side = -1; side <= 1; side += 2)
                            WeaponArt.Part(tool, "ChainTooth", PrimitiveType.Cube, new Vector3(side * 0.32f, 0f, -0.5f + i * 0.23f), new Vector3(0.17f, 0.2f, 0.13f), color);
                    break;
                case PrimaryWeaponType.WreckingBall:
                    WeaponArt.Part(tool, "Ball", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 1.65f, metal);
                    break;
                case PrimaryWeaponType.GiantBoot:
                    WeaponArt.Part(tool, "BootLeg", PrimitiveType.Cube, new Vector3(0f, 0.5f, -0.25f), new Vector3(0.8f, 1.2f, 0.8f), color);
                    WeaponArt.Part(tool, "Toe", PrimitiveType.Capsule, new Vector3(0f, 0f, 0.45f), new Vector3(1.05f, 0.9f, 0.65f), color).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case PrimaryWeaponType.FryingPan:
                    WeaponArt.Part(tool, "Pan", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.5f, 0.13f, 1.5f), metal).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case PrimaryWeaponType.Plunger:
                    WeaponArt.Part(tool, "SuctionCup", PrimitiveType.Sphere, Vector3.zero, new Vector3(1.3f, 1.3f, 0.45f), color);
                    WeaponArt.Part(tool, "CupRim", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.15f), new Vector3(1.4f, 0.05f, 1.4f), color).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    break;
                case PrimaryWeaponType.RubberChicken:
                    WeaponArt.Part(tool, "ChickenBody", PrimitiveType.Capsule, Vector3.zero, new Vector3(0.65f, 0.7f, 0.5f), color);
                    WeaponArt.Part(tool, "ChickenHead", PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0f), Vector3.one * 0.5f, color);
                    WeaponArt.Cone(tool, "Beak", 0.18f, 0.45f, WeaponArt.Material(Color.red)).SetLocalPositionAndRotation(new Vector3(0f, 0.95f, 0.2f), Quaternion.Euler(90f, 0f, 0f));
                    for (int i = -1; i <= 1; i += 2)
                        WeaponArt.Part(tool, "ChickenEye", PrimitiveType.Sphere, new Vector3(i * 0.15f, 1.05f, 0.2f), Vector3.one * 0.1f, WeaponArt.Material(Color.black));
                    break;
            }
            SamplePose(100f);
        }

        public void Play(Vector3 end, PrimaryWeaponCatalog.Profile profile)
        {
            shot = profile;
            worldEnd = end;
            firedAt = Time.time;
            punches++;
            Vector3 direction = end - Root.position; direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f) Root.rotation = Quaternion.LookRotation(direction, Vector3.up);
            reach = Mathf.Clamp(Vector3.Distance(end, Root.position), 1.5f, shot.Range);
            SamplePose(0f);
        }

        public static float Extension(float elapsed, float interval)
        {
            float peak = Mathf.Min(0.18f, interval * 0.45f);
            float duration = Mathf.Max(0.28f, interval * 0.7f);
            return elapsed < peak ? Mathf.Clamp01(elapsed / peak) : Mathf.Clamp01(1f - (elapsed - peak) / (duration - peak));
        }

        public void Update() => SamplePose(Time.time - firedAt);

        public void SamplePose(float elapsed)
        {
            if (Root == null) return;
            float amount = Extension(elapsed, shot.Interval);
            if (torso != null)
            {
                torso.localPosition = new Vector3(0f, -0.5f + amount * 0.9f, 0f);
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;
                    float punch = (punches & 1) == i ? amount : amount * 0.12f;
                    Vector3 shoulder = new Vector3(side * 0.42f, 0.4f, 0f);
                    Vector3 elbow = new Vector3(side * (0.8f - punch * 0.45f), 0.12f, 0.25f + punch * reach * 0.4f);
                    Vector3 glove = new Vector3(side * (0.6f - punch * 0.5f), 0.55f, 0.6f + punch * (reach - 0.8f));
                    WeaponArt.Link(upperArms[i], shoulder, elbow, 0.14f);
                    WeaponArt.Link(forearms[i], elbow, glove, 0.17f);
                    gloves[i].localPosition = glove;
                }
            }
            if (tool != null)
            {
                Vector3 tip = new Vector3(0f, 0.25f, 0.8f + amount * (reach - 1f));
                if (type == PrimaryWeaponType.Sledgehammer || type == PrimaryWeaponType.BattleAxe || type == PrimaryWeaponType.WreckingBall)
                    tip.y += Mathf.Sin(amount * Mathf.PI) * 1.2f;
                tool.localPosition = tip;
                bool spinning = type == PrimaryWeaponType.CircularSaw || type == PrimaryWeaponType.LaserSaw || type == PrimaryWeaponType.Drill;
                tool.localRotation = Quaternion.Euler(0f, 0f, spinning ? Time.time * 900f : amount * 15f);
                WeaponArt.Link(shaft, Vector3.zero, tip, 0.16f);
            }
            if (beam != null)
            {
                beam.enabled = elapsed >= 0f && elapsed < Mathf.Max(0.14f, shot.Interval * 0.65f);
                if (!beam.enabled) return;
                Vector3 origin = Root.position + Vector3.up * 0.3f;
                if (shot.Mode == WeaponMode.Pulse)
                {
                    beam.positionCount = 49;
                    float radius = shot.Range * Mathf.Clamp01(elapsed / 0.3f);
                    for (int i = 0; i <= 48; i++)
                    {
                        float angle = i * Mathf.PI * 2f / 48f;
                        beam.SetPosition(i, origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
                    }
                }
                else if (shot.Mode == WeaponMode.Cone)
                {
                    beam.positionCount = 5;
                    Vector3 forward = Root.parent.forward;
                    beam.SetPosition(0, origin);
                    beam.SetPosition(1, origin + Quaternion.Euler(0f, -shot.Arc, 0f) * forward * shot.Range);
                    beam.SetPosition(2, origin + forward * shot.Range);
                    beam.SetPosition(3, origin + Quaternion.Euler(0f, shot.Arc, 0f) * forward * shot.Range);
                    beam.SetPosition(4, origin);
                    beam.startWidth = beam.endWidth = 0.22f;
                }
                else
                {
                    beam.positionCount = 3;
                    beam.SetPosition(0, origin);
                    beam.SetPosition(1, Vector3.Lerp(origin, worldEnd, 0.5f) + Root.right * Mathf.Sin(elapsed * 180f) * 0.12f);
                    beam.SetPosition(2, worldEnd);
                }
            }
        }

        public void Stop() { firedAt = -100f; SamplePose(100f); }
        public void Dispose() { if (Root != null) { Root.gameObject.SetActive(false); WeaponArt.Destroy(Root.gameObject); } }
    }
}
