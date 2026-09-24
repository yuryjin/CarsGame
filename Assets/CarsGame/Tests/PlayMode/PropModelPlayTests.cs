using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    /// <summary>Imported prop models replace the primitive visuals: fewer renderers, same slots; plus a preview render.</summary>
    public sealed class PropModelPlayTests
    {
        private GameObject stage;
        private RenderTexture texture;

        private static readonly PrimaryWeaponType[] Heads =
        {
            PrimaryWeaponType.CircularSaw, PrimaryWeaponType.LaserSaw, PrimaryWeaponType.Chainsaw, PrimaryWeaponType.Drill,
            PrimaryWeaponType.Sledgehammer, PrimaryWeaponType.BattleAxe, PrimaryWeaponType.GiantBoot, PrimaryWeaponType.FryingPan,
            PrimaryWeaponType.Plunger, PrimaryWeaponType.RubberChicken
        };

        [SetUp]
        public void SetUp() { PauseController.ResetAll(); stage = new GameObject("PropModelStage"); }

        [TearDown]
        public void TearDown()
        {
            if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
            if (stage != null) Object.DestroyImmediate(stage);
        }

        private static int Renderers(Transform t)
        {
            int n = 0;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) if (!(r is LineRenderer)) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator WeaponHeadsAndPickupsUseOneImportedMesh()
        {
            for (int i = 0; i < Heads.Length; i++)
            {
                var mount = new GameObject(Heads[i].ToString()).transform;
                mount.SetParent(stage.transform);
                mount.position = new Vector3((i % 5 - 2) * 4.5f, 0f, (i / 5) * 5f);
                var rig = new WeaponRig(mount, Heads[i]);
                rig.SamplePose(0.1f);
                var head = rig.Root.Find("ToolHead");
                Assert.IsNotNull(head, Heads[i].ToString());
                Assert.IsNotNull(head.Find("Model"), Heads[i] + " still builds primitives");
                Assert.AreEqual(1, Renderers(head), Heads[i] + ": the head is one mesh");
                Assert.LessOrEqual(Renderers(rig.Root), 3, Heads[i] + ": mount + arm + head (was up to 19)");
            }
            var kinds = new[] { PickupKind.Health, PickupKind.Ammo, PickupKind.Nitro, PickupKind.Item };
            for (int i = 0; i < kinds.Length; i++)
            {
                var p = Pickup.Create(kinds[i], new Vector3((i - 1.5f) * 3f, 0f, -5f), stage.transform);
                Assert.IsNotNull(p.transform.Find("Model"), kinds[i].ToString());
                Assert.AreEqual(2, Renderers(p.transform), kinds[i] + ": model + beacon");
            }

            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                var cameraGo = new GameObject("PropPreviewCamera", typeof(Camera));
                cameraGo.transform.SetParent(stage.transform);
                var camera = cameraGo.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.45f, 0.55f, 0.65f);
                camera.orthographic = true;
                camera.orthographicSize = 8f;
                camera.transform.position = new Vector3(8f, 12f, -16f);
                camera.transform.LookAt(new Vector3(0f, 0.5f, 0f));
                var lightGo = new GameObject("KeyLight", typeof(Light));
                lightGo.transform.SetParent(stage.transform);
                var light = lightGo.GetComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.5f;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                texture = new RenderTexture(1600, 1000, 24);
                camera.targetTexture = texture;
                yield return null; yield return null; yield return null;
                var previous = RenderTexture.active;
                RenderTexture.active = texture;
                var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                image.Apply();
                string output = System.Environment.GetEnvironmentVariable("CG_PROPS_PREVIEW") ?? Path.Combine(Application.temporaryCachePath, "props-preview.png");
                File.WriteAllBytes(output, image.EncodeToPNG());
                Debug.Log("Props preview: " + output);
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                camera.targetTexture = null;
            }
            yield return null;
        }
    }
}
