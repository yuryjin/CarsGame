using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class CameraRecoveryPlayTests
    {
        private GameFlow flow;
        private float capture;
        private bool hadSettings, oldDynamic;
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (hadSettings && PlatformGlue.Save != null) PlatformGlue.Save.Data.settings.dynamicCamera = oldDynamic;
            PauseController.ResetAll(); Time.captureDeltaTime = capture;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator RecoveringFromTheCeilingImmediatelyRestoresTheCameraAndNewRacesStartUpright()
        {
            yield return RunRecovery(true);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SteadyViewStillFollowsCeilingGravityAndRecovery()
        {
            yield return RunRecovery(false);
        }

        private IEnumerator RunRecovery(bool dynamicView)
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            hadSettings = true; oldDynamic = PlatformGlue.Save.Data.settings.dynamicCamera;
            PlatformGlue.Save.Data.settings.dynamicCamera = dynamicView;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            Assert.IsTrue(flow.StartPractice(PracticeLesson.WallRide));
            while (race.State == RaceState.Countdown) yield return null;
            var car = race.Player.Car;
            var map = Object.FindAnyObjectByType<StarRingTrack>();
            float distance = map.Layout.TunnelStart + 45f;
            var path = TrackPath.Instance;
            var input = car.gameObject.AddComponent<ScriptedDriverInput>();
            car.SetInputSource(input); input.Set(1f, 0f);
            car.Teleport(path.Position(distance) + Vector3.up * (StarRingTrack.TubeHeight - .4f),
                Quaternion.LookRotation(path.Tangent(distance), Vector3.down));
            car.Body.linearVelocity = path.Tangent(distance) * 22f;
            for (int tick = 0; tick < 50; tick++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(car.WallRiding, "Fixture is physically attached to the ceiling");
            var camera = Camera.main;
            Assert.Less(Vector3.Dot(camera.transform.up, Vector3.up), -.6f, "The camera has followed the inverted car");

            car.ResetCar();
            Assert.Greater(Vector3.Dot(camera.transform.up, Vector3.up), .7f,
                "Recovery must restore an upright view immediately, without rolling through the road");
            Assert.Less(Vector3.Distance(camera.transform.position, car.transform.position), 10f);
            var follow = camera.GetComponent<CarCameraFollow>();
            Assert.AreEqual(0f, follow.CurrentRoll, .001f);
            Assert.AreEqual(dynamicView ? follow.fovAtRest : follow.steadyFov, camera.fieldOfView, .01f);

            car.Teleport(car.transform.position + new Vector3(180f, 0f, 50f), Quaternion.Euler(0f, 90f, 0f));
            Assert.Less(Vector3.Distance(camera.transform.position, car.transform.position), 10f,
                "A long recovery cannot leave the player off camera while smoothing catches up");
            flow.EnterMenu();
            Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
            yield return null;
            Assert.Greater(Vector3.Dot(camera.transform.up, Vector3.up), .7f);
            Assert.Less(Vector3.Distance(camera.transform.position, race.Player.Car.transform.position), 10f);
        }
    }
}
