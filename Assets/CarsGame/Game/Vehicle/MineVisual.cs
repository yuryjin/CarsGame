using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace RacingGame
{
    /// <summary>
    /// Look of a pooled <see cref="MineHazard"/>, built from primitives without art assets: a flat
    /// anti-tank disc with prongs (contact mine), the same disc on a yellow warning pad (field mine), or a
    /// taped brick with an antenna (charge). A light on top tells the state from a distance: amber while
    /// arming, slow red when a contact mine is live, a red blink on a charge that speeds up as the fuse
    /// runs out. Nothing here feeds back into the simulation.
    /// </summary>
    public sealed class MineVisual
    {
        private static readonly Color ArmingColor = new Color(1f, 0.72f, 0.1f);
        private static readonly Color LiveColor = new Color(1f, 0.12f, 0.08f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static Material bodyMat, metalMat, hazardMat, chargeMat, tapeMat, lightMat;

        private GameObject contact, pad, charge;
        private Transform root, banana;
        private readonly Dictionary<MineSkin, GameObject> skins = new Dictionary<MineSkin, GameObject>();
        private readonly Transform[] peels = new Transform[3];
        private MineSkin activeSkin;
        private float shownAt;
        private Renderer contactLight, chargeLight, contactBody;
        private Renderer activeLight;
        private MaterialPropertyBlock block;
        private bool lightOn;
        private Color lightColor;

        public static MineVisual Build(Transform root)
        {
            EnsureMaterials();
            var v = new MineVisual { block = new MaterialPropertyBlock(), root = root };

            v.contact = Group(root, "Contact");
            v.contactBody = Part(v.contact.transform, "Body", PrimitiveType.Cylinder, new Vector3(0f, 0.09f, 0f), new Vector3(1.1f, 0.09f, 1.1f), bodyMat);
            Part(v.contact.transform, "Cap", PrimitiveType.Sphere, new Vector3(0f, 0.18f, 0f), new Vector3(0.62f, 0.2f, 0.62f), metalMat);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                Part(v.contact.transform, "Prong" + i, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(a) * 0.36f, 0.24f, Mathf.Sin(a) * 0.36f),
                    new Vector3(0.07f, 0.07f, 0.07f), metalMat);
            }
            v.contactLight = Part(v.contact.transform, "Light", PrimitiveType.Sphere, new Vector3(0f, 0.3f, 0f), new Vector3(0.2f, 0.2f, 0.2f), lightMat);
            v.pad = Part(root, "WarningPad", PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 0f), new Vector3(1.9f, 0.012f, 1.9f), hazardMat).gameObject;

            v.charge = Group(root, "Charge");
            Part(v.charge.transform, "Brick", PrimitiveType.Cube, new Vector3(0f, 0.16f, 0f), new Vector3(0.9f, 0.32f, 0.56f), chargeMat);
            Part(v.charge.transform, "TapeA", PrimitiveType.Cube, new Vector3(-0.24f, 0.16f, 0f), new Vector3(0.1f, 0.34f, 0.58f), tapeMat);
            Part(v.charge.transform, "TapeB", PrimitiveType.Cube, new Vector3(0.24f, 0.16f, 0f), new Vector3(0.1f, 0.34f, 0.58f), tapeMat);
            Part(v.charge.transform, "Antenna", PrimitiveType.Cylinder, new Vector3(0.34f, 0.6f, 0.18f), new Vector3(0.035f, 0.28f, 0.035f), tapeMat);
            v.chargeLight = Part(v.charge.transform, "Light", PrimitiveType.Sphere, new Vector3(0f, 0.36f, 0f), new Vector3(0.18f, 0.18f, 0.18f), lightMat);

            v.charge.SetActive(false);
            v.pad.SetActive(false);
            return v;
        }

        /// <summary>Pick the model for this activation. A spawn material (legacy API) repaints the contact body.</summary>
        public void Show(MineKind kind, bool field, Material bodyOverride, MineSkin skin = MineSkin.Standard)
        {
            bool isCharge = kind == MineKind.Charge;
            activeSkin = isCharge || field ? MineSkin.Standard : skin;
            shownAt = Time.time;
            if (activeSkin != MineSkin.Standard && !skins.ContainsKey(activeSkin)) BuildSkin(activeSkin);
            foreach (var pair in skins) pair.Value.SetActive(pair.Key == activeSkin);
            contact.SetActive(!isCharge && activeSkin == MineSkin.Standard);
            charge.SetActive(isCharge);
            pad.SetActive(!isCharge && field);
            contactBody.sharedMaterial = bodyOverride != null ? bodyOverride : bodyMat;
            activeLight = isCharge ? chargeLight : contactLight;
            lightOn = false;
            lightColor = Color.clear;
            AnimateSkin(0f);
        }

        public void Blink(int tick, int armTick, int deathTick, MineKind kind, float time)
        {
            AnimateSkin(time - shownAt);
            if (activeLight == null) return;
            Color color;
            bool on;
            if (tick < armTick)
            {
                color = ArmingColor;
                on = Mathf.Repeat(time * 6f, 1f) < 0.5f;
            }
            else if (kind == MineKind.Charge && deathTick >= 0)
            {
                // 2 Hz right after arming, 10 Hz in the last moments of the fuse: everyone can read the danger.
                float left = Mathf.Clamp01((deathTick - tick) / (float)Mathf.Max(1, deathTick - armTick));
                float hz = Mathf.Lerp(10f, 2f, left);
                color = LiveColor;
                on = Mathf.Repeat(time * hz, 1f) < 0.5f;
            }
            else
            {
                color = LiveColor;
                on = Mathf.Repeat(time * 1.5f, 1f) < 0.35f;
            }
            if (on == lightOn && color == lightColor) return;
            lightOn = on;
            lightColor = color;
            Color shown = on ? color : color * 0.25f;
            block.Clear();
            block.SetColor(BaseColorId, shown);
            block.SetColor(ColorId, shown);
            block.SetColor(EmissionId, on ? color * 4f : Color.black);
            activeLight.SetPropertyBlock(block);
        }

        private void BuildSkin(MineSkin skin)
        {
            var group = Group(root, skin.ToString());
            skins.Add(skin, group);
            Transform t = group.transform;
            var yellow = WeaponArt.Material(new Color(1f, 0.84f, 0.03f));
            var green = WeaponArt.Material(new Color(0.15f, 0.65f, 0.12f));
            var metal = WeaponArt.Material(new Color(0.5f, 0.55f, 0.6f));
            if (skin == MineSkin.Banana)
            {
                banana = t;
                WeaponArt.Banana(t, yellow);
                WeaponArt.Part(t, "Stem", PrimitiveType.Capsule, new Vector3(1.68f, 1.25f, 0f), new Vector3(0.22f, 0.3f, 0.22f), green).localRotation = Quaternion.Euler(0f, 0f, -35f);
                for (int i = 0; i < peels.Length; i++)
                {
                    peels[i] = Group(t, "PeelHinge" + i).transform;
                    peels[i].localPosition = new Vector3(-1.45f, 0.8f, 0f);
                    WeaponArt.Part(peels[i], "Peel", PrimitiveType.Capsule, new Vector3(0f, 0.55f, 0f), new Vector3(0.38f, 0.65f, 0.15f), yellow);
                }
                for (int i = -1; i <= 1; i += 2)
                {
                    WeaponArt.Part(t, "GooglyEye", PrimitiveType.Sphere, new Vector3(i * 0.25f, 0.7f, 0.28f), Vector3.one * 0.25f, WeaponArt.Material(Color.white));
                    WeaponArt.Part(t, "Pupil", PrimitiveType.Sphere, new Vector3(i * 0.25f, 0.7f, 0.4f), Vector3.one * 0.12f, WeaponArt.Material(Color.black));
                }
            }
            else if (skin == MineSkin.Cactus)
            {
                WeaponArt.Part(t, "Cactus", PrimitiveType.Capsule, Vector3.up * 0.65f, new Vector3(0.65f, 0.75f, 0.65f), green);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6f;
                    var spike = WeaponArt.Cone(t, "Needle", 0.07f, 0.35f, metal);
                    spike.localPosition = new Vector3(Mathf.Cos(a) * 0.3f, 0.3f + (i % 3) * 0.35f, Mathf.Sin(a) * 0.3f);
                    spike.localRotation = Quaternion.FromToRotation(Vector3.up, new Vector3(Mathf.Cos(a), 0.3f, Mathf.Sin(a)));
                }
            }
            else if (skin == MineSkin.BearTrap)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var jaw = Group(t, "Jaw").transform;
                    jaw.localPosition = new Vector3(side * 0.35f, 0.2f, 0f);
                    jaw.localRotation = Quaternion.Euler(0f, 0f, side * 35f);
                    WeaponArt.Part(jaw, "JawRail", PrimitiveType.Cube, Vector3.zero, new Vector3(0.3f, 0.15f, 1.6f), metal);
                    for (int i = 0; i < 6; i++)
                        WeaponArt.Cone(jaw, "Tooth", 0.12f, 0.4f, metal).localPosition = new Vector3(0f, 0f, -0.65f + i * 0.26f);
                }
            }
            else if (skin == MineSkin.Spring)
            {
                for (int i = 0; i < 9; i++)
                    WeaponArt.Part(t, "SpringRing", PrimitiveType.Cylinder, Vector3.up * (0.08f + i * 0.08f), new Vector3(0.65f, 0.018f, 0.65f), metal);
                WeaponArt.Part(t, "SpringPlate", PrimitiveType.Cube, Vector3.up * 0.8f, new Vector3(1.2f, 0.1f, 1.2f), yellow);
            }
            else
            {
                Color tint = skin == MineSkin.Glue ? new Color(0.6f, 1f, 0.05f) : skin == MineSkin.Ice ? Color.cyan : new Color(0.75f, 0.25f, 1f);
                var material = WeaponArt.Material(tint, true);
                WeaponArt.Part(t, "TrapDisc", PrimitiveType.Cylinder, Vector3.up * 0.08f, new Vector3(1.7f, 0.08f, 1.7f), material);
                if (skin == MineSkin.Ice)
                    for (int i = 0; i < 6; i++)
                        WeaponArt.Cone(t, "IceCrystal", 0.2f, 0.7f, material).localPosition = new Vector3(Mathf.Sin(i) * 0.5f, 0.1f, Mathf.Cos(i) * 0.5f);
                if (skin == MineSkin.Emp)
                    WeaponArt.Part(t, "Coil", PrimitiveType.Sphere, Vector3.up * 0.5f, Vector3.one * 0.7f, material);
            }
        }

        /// <summary>Banana lands with squash, two bounces and unfolding peel. Skin never changes mine physics.</summary>
        public void AnimateSkin(float age)
        {
            if (activeSkin != MineSkin.Banana || banana == null) return;
            float settle = Mathf.Clamp01(age / 1.1f);
            float bounce = Mathf.Abs(Mathf.Sin(settle * Mathf.PI * 3f)) * (1f - settle);
            banana.localPosition = Vector3.up * (bounce * 1.4f);
            banana.localScale = new Vector3(1f + bounce * 0.14f, 1f - bounce * 0.18f, 1f + bounce * 0.14f);
            banana.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(settle * Mathf.PI * 6f) * (1f - settle) * 18f);
            for (int i = 0; i < peels.Length; i++)
                peels[i].localRotation = Quaternion.Euler(Mathf.SmoothStep(0f, 80f, settle) * Mathf.Cos(i * 2.094f), 0f,
                    Mathf.SmoothStep(0f, 80f, settle) * Mathf.Sin(i * 2.094f));
        }

        private static GameObject Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Renderer Part(Transform parent, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(shape);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private static void EnsureMaterials()
        {
            if (bodyMat != null) return;
            // Same approach as Pickup: URP/Lit ships with every build because the track uses it.
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            bodyMat = Make(shader, "Mine_Body", new Color(0.2f, 0.23f, 0.16f), 0.35f);
            metalMat = Make(shader, "Mine_Metal", new Color(0.42f, 0.42f, 0.4f), 0.6f);
            hazardMat = Make(shader, "Mine_Warning", new Color(0.95f, 0.72f, 0.08f), 0.2f, 0.35f);
            chargeMat = Make(shader, "Mine_Charge", new Color(0.74f, 0.64f, 0.44f), 0.2f);
            tapeMat = Make(shader, "Mine_Tape", new Color(0.12f, 0.12f, 0.12f), 0.3f);
            lightMat = Make(shader, "Mine_Light", LiveColor, 0.8f, 2f);
        }

        private static Material Make(Shader shader, string name, Color color, float smoothness, float emission = 0f)
        {
            var m = new Material(shader) { name = name };
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, color);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (emission > 0f && m.HasProperty(EmissionId)) { m.EnableKeyword("_EMISSION"); m.SetColor(EmissionId, color * emission); }
            return m;
        }
    }
}
