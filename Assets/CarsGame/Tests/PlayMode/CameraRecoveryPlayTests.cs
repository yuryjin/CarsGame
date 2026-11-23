using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using RacingGame.Core.Platform;

namespace RacingGame.Tests
{
    public sealed class CameraRecoveryPlayTests
    {
        private GameFlow flow;
        private float capture;
        private bool hadSettings, oldDynamic;
        private int oldDistance;
        [SetUp] public void Setup() { capture = Time.captureDeltaTime; }
        [TearDown] public void Cleanup()
        {
            if (flow != null) flow.EnterMenu();
            if (hadSettings && PlatformGlue.Save != null)
            {
                PlatformGlue.Save.Data.settings.dynamicCamera = oldDynamic;
                PlatformGlue.Save.Data.settings.cameraDistance = oldDistance;
                PlatformGlue.Save.MarkDirty();
            }
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

        [UnityTest, Timeout(240000)]
        public IEnumerator DuneAndTitanFrameTheBendAtBothDistancesAndRememberTheChoice()
        {
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            var settings = PlatformGlue.Save.Data.settings;
            hadSettings = true; oldDynamic = settings.dynamicCamera; oldDistance = settings.cameraDistance;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var race = flow.GetComponent<RaceManager>();
            Button Button(string name) => GameObject.Find(name).GetComponent<Button>();
            foreach (string body in new[] { "dune", "titan" })
            foreach (bool dynamicView in new[] { true, false })
            {
                settings.dynamicCamera = dynamicView;
                Assert.IsTrue(flow.StartTestDrive(body));
                while (race.State == RaceState.Countdown) yield return null;
                var car = race.Player.Car;
                Assert.AreEqual(body, car.GetComponent<CarBody>().BodyId);
                var path = TrackPath.Instance;
                float start = 10f;
                while (start < path.Length - 70f && Vector3.Angle(path.Tangent(start), path.Tangent(start + 35f)) < 24f) start += 3f;
                Assert.Less(start, path.Length - 70f, "A curved section is required");
                var ai = car.gameObject.AddComponent<AiDriver>(); ai.Initialize(null, 0f);
                car.GetComponent<PlayerDriverInput>().enabled = false; car.SetInputSource(ai);
                var camera = Camera.main;
                var follow = camera.GetComponent<CarCameraFollow>();
                foreach (int distance in new[] { -1, 1 })
                {
                    Button("PauseButton").onClick.Invoke(); yield return null;
                    Button("PauseSettings").onClick.Invoke(); yield return null;
                    Button("CameraDistance" + (distance + 1)).onClick.Invoke(); yield return null;
                    Button("Back").onClick.Invoke(); yield return null;
                    Button("Resume").onClick.Invoke(); yield return null;
                    Assert.AreEqual(distance, settings.cameraDistance);
                    Vector3 heading = path.Tangent(start);
                    car.Teleport(path.Position(start) + Vector3.up * 0.6f, Quaternion.LookRotation(heading));
                    float factor = distance < 0 ? .8f : 1.3f;
                    float expected = (follow.offset.z + (dynamicView ? .4f : -.5f)) * factor;
                    Assert.AreEqual(expected, car.transform.InverseTransformPoint(camera.transform.position).z, .05f,
                        "Recovery must cut immediately to the selected chase distance");
                    car.Body.linearVelocity = heading * 14f;
                    int hint = -1, visible = 0, samples = 0;
                    float travelled = 0f, turn = 0f;
                    for (int frame = 0; frame < 330 && travelled < 45f; frame++)
                    {
                        yield return null;
                        float at = path.Project(car.transform.position, ref hint);
                        travelled = path.SignedDelta(start, at);
                        turn = Mathf.Max(turn, Vector3.Angle(heading, car.transform.forward));
                        Vector3 marker = camera.WorldToViewportPoint(path.Position(at + 16f) + Vector3.up * .2f);
                        if (marker.z > 0f && marker.x > 0.03f && marker.x < .97f && marker.y > .02f && marker.y < .98f) visible++;
                        samples++;
                    }
                    Debug.Log($"[CameraDistance] {body} dynamic={dynamicView} distance={distance} road={visible}/{samples} travelled={travelled:F1}m heading={turn:F1}");
                    Assert.Greater(travelled, 35f);
                    Assert.Greater(turn, 15f, "The car must turn under physical driving input");
                    Assert.GreaterOrEqual(visible, samples * .8f, "The upcoming road remains framed");
                }
                flow.EnterMenu(); yield return null; yield return null;
            }
            var flush = PlatformGlue.Save.FlushAsync();
            while (!flush.IsCompleted) yield return null;
            Assert.IsFalse(flush.IsFaulted);
            typeof(PlatformGlue).GetProperty(nameof(PlatformGlue.Save)).SetValue(null, null);
            yield return SceneManager.LoadSceneAsync("SampleScene");
            deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            Assert.AreEqual(1, PlatformGlue.Save.Data.settings.cameraDistance);
            flow = Object.FindAnyObjectByType<GameFlow>();
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
            oldDistance = PlatformGlue.Save.Data.settings.cameraDistance;
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
