using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class CameraFeedbackPlayTests
    {
        private GameObject cameraObject, targetObject;
        private float capture;
        private bool oldShake;
        private bool hadSave;

        [SetUp] public void Setup()
        {
            capture = Time.captureDeltaTime;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            PauseController.ResetAll();
            hadSave = PlatformGlue.Save != null;
            if (hadSave)
            {
                oldShake = PlatformGlue.Save.Data.settings.cameraShake;
                PlatformGlue.Save.Data.settings.cameraShake = true;
            }
        }

        [TearDown] public void Cleanup()
        {
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            if (targetObject != null) Object.DestroyImmediate(targetObject);
            if (hadSave && PlatformGlue.Save != null) PlatformGlue.Save.Data.settings.cameraShake = oldShake;
            Time.captureDeltaTime = capture;
            PauseController.ResetAll();
        }

        [UnityTest]
        public IEnumerator ResetAndDisableRestoreTheUnshakenPose()
        {
            cameraObject = new GameObject("CameraFeedbackTest");
            var impulse = cameraObject.AddComponent<CameraImpulse>();
            impulse.decay = 0f;
            Vector3 position = new Vector3(12f, 8f, -4f);
            Quaternion rotation = Quaternion.Euler(15f, 42f, 3f);
            cameraObject.transform.SetPositionAndRotation(position, rotation);
            impulse.AddImpulse(1f);
            yield return null; yield return null;
            // Sample the rendered pose before Update removes the previous frame's offset.
            yield return new WaitForFixedUpdate();
            Assert.Greater(Vector3.Distance(position, cameraObject.transform.position), 0.001f, "The fixture must actually shake");
            impulse.ResetEnergy();
            Assert.Less(Vector3.Distance(position, cameraObject.transform.position), 0.0001f, "Reset must undo the rendered offset immediately");
            Assert.Less(Quaternion.Angle(rotation, cameraObject.transform.rotation), 0.01f);

            impulse.AddImpulse(1f);
            yield return null; yield return null;
            yield return new WaitForFixedUpdate();
            impulse.enabled = false;
            Assert.AreEqual(0f, impulse.Energy);
            Assert.Less(Vector3.Distance(position, cameraObject.transform.position), 0.0001f, "Disabling cannot leave the camera displaced");
            Assert.Less(Quaternion.Angle(rotation, cameraObject.transform.rotation), 0.01f);
        }

        [UnityTest]
        public IEnumerator FollowSmoothingDoesNotAccumulateShakeAndPauseStopsMotion()
        {
            targetObject = new GameObject("CameraFeedbackTarget");
            cameraObject = new GameObject("CameraFeedbackFollow", typeof(Camera));
            var follow = cameraObject.AddComponent<CarCameraFollow>();
            follow.target = targetObject.transform;
            follow.positionSmoothSpeed = follow.rotationSmoothSpeed = 10000f;
            var impulse = cameraObject.AddComponent<CameraImpulse>();
            impulse.decay = 0f;
            yield return null; yield return null;
            Vector3 position = cameraObject.transform.position;
            Quaternion rotation = cameraObject.transform.rotation;
            impulse.AddImpulse(1f);
            for (int i = 0; i < 12; i++) yield return null;
            impulse.ResetEnergy();
            Assert.Less(Vector3.Distance(position, cameraObject.transform.position), 0.0001f,
                "The follow camera must interpolate from its clean pose, before the new impulse");
            Assert.Less(Quaternion.Angle(rotation, cameraObject.transform.rotation), 0.01f);

            impulse.AddImpulse(1f);
            yield return null; yield return null;
            PauseController.Set(RacingGame.Core.Platform.PauseReason.User, true);
            yield return null; yield return null;
            Assert.AreEqual(0f, impulse.Energy, "Pause clears shake rather than animating the background");
            Assert.Less(Vector3.Distance(position, cameraObject.transform.position), 0.0001f);
            impulse.AddImpulse(1f);
            Assert.AreEqual(0f, impulse.Energy, "Paused effects cannot queue a surprise shake on resume");
        }
    }
}
