using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class SteadyCameraPlayTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator SpeedAndCornerEffectsCanBeDisabledAndRestoredWithoutChangingShakePreference()
        {
            float capture = Time.captureDeltaTime;
            bool saved = false, dynamic = true, shake = true;
            GameObject target = null, view = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                PauseController.ResetAll();
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float until = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue(PlatformGlue.IsReady);
                yield return null; yield return null;
                var settings = PlatformGlue.Save.Data.settings;
                saved = true; dynamic = settings.dynamicCamera; shake = settings.cameraShake;
                settings.dynamicCamera = true; settings.cameraShake = false;
                target = new GameObject("SteadyCameraTarget", typeof(Rigidbody));
                var body = target.GetComponent<Rigidbody>();
                body.useGravity = false; target.transform.position = Vector3.up * 1000f;
                view = new GameObject("SteadyCameraView", typeof(Camera));
                var camera = view.GetComponent<Camera>(); camera.enabled = false; camera.fieldOfView = 60f;
                var follow = view.AddComponent<CarCameraFollow>();
                follow.fovAttack = follow.fovRelease = 10000f;
                follow.positionSmoothSpeed = follow.rotationSmoothSpeed = 10000f;
                follow.SetTarget(target.transform);
                body.linearVelocity = Vector3.forward * 50f;
                body.angularVelocity = Vector3.up * 0.6f;
                for (int i = 0; i < 15; i++) yield return null;
                Assert.Greater(camera.fieldOfView, 65f, "Control: ordinary driving widens the view");
                Assert.Greater(Mathf.Abs(follow.CurrentRoll), 1f, "Control: ordinary cornering leans the view");
                settings.dynamicCamera = false;
                for (int i = 0; i < 3; i++) yield return null;
                Assert.AreEqual(follow.steadyFov, camera.fieldOfView, .01f);
                Assert.AreEqual(0f, follow.CurrentRoll);
                foreach (float speed in new[] { 0f, 15f, 50f })
                {
                    body.linearVelocity = target.transform.forward * speed;
                    for (int i = 0; i < 3; i++) yield return null;
                    Assert.AreEqual(follow.steadyFov, camera.fieldOfView, .01f);
                    Assert.AreEqual(0f, follow.CurrentRoll);
                }
                follow.SnapToTarget();
                Assert.AreEqual(follow.steadyFov, camera.fieldOfView, .01f);
                Assert.AreEqual(follow.offset.z - .5f, target.transform.InverseTransformPoint(camera.transform.position).z, .01f);
                settings.dynamicCamera = true;
                body.linearVelocity = target.transform.forward * 50f;
                for (int i = 0; i < 15; i++) yield return null;
                Assert.Greater(camera.fieldOfView, 65f);
                Assert.Greater(Mathf.Abs(follow.CurrentRoll), 1f);
                Assert.IsFalse(settings.cameraShake, "Zoom preference does not rewrite the separate shake option");
            }
            finally
            {
                if (view != null) Object.DestroyImmediate(view);
                if (target != null) Object.DestroyImmediate(target);
                if (saved && PlatformGlue.Save != null)
                {
                    PlatformGlue.Save.Data.settings.dynamicCamera = dynamic;
                    PlatformGlue.Save.Data.settings.cameraShake = shake;
                }
                Time.captureDeltaTime = capture;
                PauseController.ResetAll();
            }
        }
    }
}
