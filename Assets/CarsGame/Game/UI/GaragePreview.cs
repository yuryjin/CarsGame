using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using RacingGame.Core.Economy;
using RacingGame.Core.Save;

namespace RacingGame
{
    /// <summary>One small offscreen camera; the displayed car contains meshes, paint and livery only.</summary>
    public sealed class GaragePreview : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private GameObject stage, model;
        private RenderTexture texture;
        private bool dragging;
        private float resumeAt;
        public GameObject Model => model;

        public void Initialize(GameObject prefab, BodyDef body, ProgressData progress)
        {
            if (prefab == null) return;
            stage = new GameObject("GarageStage");
            stage.transform.position = new Vector3(0f, -1500f, 0f);
            // The template never becomes active: no car registers with the simulation or starts its engine.
            var templateRoot = new GameObject("GarageTemplate");
            templateRoot.SetActive(false);
            var template = Instantiate(prefab, templateRoot.transform, false);
            template.GetComponent<CarCannon>()?.EnsureTurretVisuals();
            CarBody.Apply(template, body);
            PlayerProgression.ApplyWeapons(template.GetComponent<CarWeapons>(), progress.weapons, progress.equippedSecondary, progress.equippedPrimary);
            model = CopyVisuals(template.transform, stage.transform);
            model.name = "Preview_" + body.Id;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, 145f, 0f);
            model.AddComponent<CarPaint>();
            PlayerCosmetics.Apply(model, progress);
            Destroy(templateRoot);

            var cameraObject = new GameObject("GarageCamera", typeof(Camera));
            cameraObject.transform.SetParent(stage.transform, false);
            cameraObject.transform.localPosition = new Vector3(4.8f, 3f, 6f);
            cameraObject.transform.LookAt(stage.transform.position + Vector3.up * 0.8f);
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.055f, 0.085f);
            camera.fieldOfView = 34f;
            camera.nearClipPlane = 0.1f; camera.farClipPlane = 30f;
            camera.cullingMask = 1 << 2;
            camera.allowHDR = false; camera.allowMSAA = false;
            texture = new RenderTexture(768, 512, 24) { name = "GaragePreview", antiAliasing = 1 };
            texture.Create(); camera.targetTexture = texture;
            var image = gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = true;

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Turntable";
            floor.transform.SetParent(model.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.13f, 0f);
            floor.transform.localScale = new Vector3(5.4f, 0.1f, 5.4f);
            floor.GetComponent<Collider>().enabled = false;
            Destroy(floor.GetComponent<Collider>());
            floor.GetComponent<Renderer>().sharedMaterial = WeaponArt.Material(new Color(0.13f, 0.18f, 0.25f));
            foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2;
        }

        private static GameObject CopyVisuals(Transform source, Transform parent)
        {
            var copy = new GameObject(source.name);
            copy.transform.SetParent(parent, false);
            copy.transform.localPosition = source.localPosition;
            copy.transform.localRotation = source.localRotation;
            copy.transform.localScale = source.localScale;
            var filter = source.GetComponent<MeshFilter>();
            var renderer = source.GetComponent<MeshRenderer>();
            if (filter != null && renderer != null && renderer.enabled)
            {
                copy.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var target = copy.AddComponent<MeshRenderer>();
                target.sharedMaterials = renderer.sharedMaterials;
                target.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (Transform child in source) if (child.gameObject.activeSelf) CopyVisuals(child, copy.transform);
            return copy;
        }

        private void Update()
        {
            if (model != null && !dragging && Time.unscaledTime >= resumeAt) model.transform.Rotate(0f, 12f * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        public void Rotate(float degrees) { if (model != null) model.transform.Rotate(0f, degrees, 0f, Space.Self); resumeAt = Time.unscaledTime + 3f; }
        public void OnBeginDrag(PointerEventData data) { dragging = true; }
        public void OnDrag(PointerEventData data) => Rotate(-data.delta.x * 0.45f);
        public void OnEndDrag(PointerEventData data) { dragging = false; resumeAt = Time.unscaledTime + 3f; }
        private void OnDisable() { if (stage != null) stage.SetActive(false); }
        private void OnDestroy()
        {
            if (stage != null) Destroy(stage);
            if (texture != null) { texture.Release(); Destroy(texture); }
        }
    }
}
