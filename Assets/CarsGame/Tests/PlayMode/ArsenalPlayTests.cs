using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RacingGame.Tests
{
    public sealed class ArsenalPlayTests
    {
        private GameObject stage;
        private RenderTexture texture;
        private float previousScale, previousCaptureDelta;

        [SetUp]
        public void SetUp()
        {
            previousScale = Time.timeScale;
            previousCaptureDelta = Time.captureDeltaTime;
            Time.captureDeltaTime = 0.02f;
            PauseController.ResetAll();
            Time.timeScale = 1f;
            stage = new GameObject("ArsenalPreviewStage");
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = previousScale;
            Time.captureDeltaTime = previousCaptureDelta;
            if (texture != null) { texture.Release(); Object.DestroyImmediate(texture); }
            if (stage != null) Object.DestroyImmediate(stage);
        }

        [UnityTest]
        public IEnumerator PageHotkeysAndNumberZeroReachTheFiftiethWeapon()
        {
            var savedBackground = InputSystem.settings.backgroundBehavior;
            var savedEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var go = new GameObject("ArsenalInput");
            go.transform.SetParent(stage.transform);
            go.AddComponent<CarCannon>();
            go.AddComponent<CarWeapons>();
            var input = go.AddComponent<PlayerDriverInput>();
            try
            {
                yield return null;
                PauseController.ResetAll();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                yield return null; yield return null;
                Assert.IsTrue(input.WeaponWheelOpen, $"paused={PauseController.Reasons}, Q={Keyboard.current?.qKey.isPressed}, enabled={input.isActiveAndEnabled}");
                for (int page = 1; page < PrimaryWeaponCatalog.PageCount; page++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.Tab));
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                    yield return null;
                    Assert.AreEqual(page, input.SelectionPage);
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.Digit0));
                yield return null; yield return null;
                Assert.AreEqual(50, input.Sample(0).SelectPrimary);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.LeftShift, Key.Tab));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                yield return null;
                Assert.AreEqual(3, input.SelectionPage);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = savedBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = savedEditorInput;
                Object.Destroy(go);
            }
        }

        [UnityTest]
        public IEnumerator BoxerExtendsAlternatingHands_AndBananaSettlesAfterLanding()
        {
            var mount = new GameObject("BoxerCar").transform;
            mount.SetParent(stage.transform);
            var rig = new WeaponRig(mount, PrimaryWeaponType.Boxer);
            var shot = PrimaryWeaponCatalog.Get(PrimaryWeaponType.Boxer);
            var body = rig.Root.Find("PopUpBoxer");
            float idle = body.localPosition.y;
            rig.Play(mount.position + Vector3.forward * 5f + Vector3.up, shot);
            float elapsed = 0f;
            while (elapsed < 0.18f) { yield return null; elapsed += Time.deltaTime; rig.Update(); }
            Assert.Greater(body.localPosition.y, idle + 0.4f);
            var left = body.Find("Glove0");
            var right = body.Find("Glove1");
            Assert.Greater(right.localPosition.z, left.localPosition.z + 1f);
            rig.Play(mount.position + Vector3.right * 5f + Vector3.up, shot);
            rig.SamplePose(0.18f);
            Assert.Greater(left.localPosition.z, right.localPosition.z + 1f);
            Assert.Greater(Vector3.Dot(rig.Root.forward, Vector3.right), 0.95f);
            rig.Stop();
            Assert.AreEqual(idle, body.localPosition.y, 0.001f);

            var mineRoot = new GameObject("BananaMine").transform;
            mineRoot.SetParent(stage.transform);
            var banana = MineVisual.Build(mineRoot);
            banana.Show(MineKind.Contact, false, null, MineSkin.Banana);
            banana.AnimateSkin(0.18f);
            Transform model = mineRoot.Find("Banana");
            Assert.Greater(model.localPosition.y, 0.4f);
            banana.AnimateSkin(2f);
            Assert.AreEqual(Vector3.zero, model.localPosition);
            Assert.AreEqual(Vector3.one, model.localScale);
            banana.Show(MineKind.Contact, false, null);
            Assert.IsFalse(model.gameObject.activeSelf);
            banana.Show(MineKind.Contact, false, null, MineSkin.Banana);
            Assert.AreEqual(Vector3.zero, model.localPosition, "Reused skin resets its animation.");
            rig.Dispose();
        }

        [UnityTest]
        public IEnumerator RenderRepresentativeWeaponsForVisualReview()
        {
            var cameraGo = new GameObject("ArsenalPreviewCamera", typeof(Camera));
            cameraGo.transform.SetParent(stage.transform);
            var camera = cameraGo.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.045f, 0.065f);
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.transform.position = new Vector3(10f, 13f, 20f);
            camera.transform.LookAt(new Vector3(0f, 0.5f, 3f));
            var lightGo = new GameObject("KeyLight", typeof(Light));
            lightGo.transform.SetParent(stage.transform);
            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var floor = WeaponArt.Part(stage.transform, "Floor", PrimitiveType.Cube, new Vector3(0f, -0.3f, 3f),
                new Vector3(24f, 0.3f, 17f), WeaponArt.Material(new Color(0.12f, 0.15f, 0.2f)));
            var types = new[] { PrimaryWeaponType.Boxer, PrimaryWeaponType.CircularSaw, PrimaryWeaponType.Drill,
                PrimaryWeaponType.Sledgehammer, PrimaryWeaponType.BattleAxe, PrimaryWeaponType.LaserSaw };
            for (int i = 0; i < types.Length; i++)
            {
                var mount = new GameObject(types[i].ToString()).transform;
                mount.SetParent(stage.transform);
                mount.position = new Vector3((i % 3 - 1) * 5f, 0f, (i / 3) * 6f);
                WeaponArt.Part(mount, "CarBody", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(1.5f, 0.5f, 2.6f), WeaponArt.Material(new Color(0.18f, 0.28f, 0.38f)));
                var rig = new WeaponRig(mount, types[i]);
                rig.Play(mount.position + Vector3.forward * 3f + Vector3.up, PrimaryWeaponCatalog.Get(types[i]));
                rig.SamplePose(Mathf.Min(0.18f, PrimaryWeaponCatalog.Get(types[i]).Interval * 0.45f));
                Assert.IsNotNull(rig.Root.Find(types[i] == PrimaryWeaponType.Boxer ? "PopUpBoxer" : "ToolHead"));
            }
            var bananaRoot = new GameObject("GiantBanana").transform;
            bananaRoot.SetParent(stage.transform);
            bananaRoot.position = new Vector3(0f, 0f, -4f);
            var banana = MineVisual.Build(bananaRoot);
            banana.Show(MineKind.Contact, false, null, MineSkin.Banana);
            banana.AnimateSkin(0.6f);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                texture = new RenderTexture(1440, 1000, 24);
                camera.targetTexture = texture;
                yield return null; yield return null; yield return null;
                var previous = RenderTexture.active;
                RenderTexture.active = texture;
                var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                image.Apply();
                string output = System.Environment.GetEnvironmentVariable("CG_ARSENAL_PREVIEW") ?? Path.Combine(Application.temporaryCachePath, "arsenal-preview.png");
                File.WriteAllBytes(output, image.EncodeToPNG());
                Debug.Log("Arsenal preview: " + output);
                RenderTexture.active = previous;
                Object.DestroyImmediate(image);
                camera.targetTexture = null;
            }
            yield return null;
        }
    }
}
