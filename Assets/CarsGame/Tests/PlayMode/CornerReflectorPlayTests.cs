using System.Collections;
using System.IO;
using NUnit.Framework;
using RacingGame.World.Gen;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class CornerReflectorPlayTests
    {
        [UnityTest, Timeout(240000)]
        public IEnumerator NightEntryAndApexShareAMobileCamera()
        {
            string dir = System.Environment.GetEnvironmentVariable("CG_REFLECTOR_SHOTS");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("Set CG_REFLECTOR_SHOTS to capture the Mobile frames.");
            float clock = Time.captureDeltaTime;
            int quality = QualitySettings.GetQualityLevel();
            RaceEventDefinition ev = null;
            GameFlow flow = null;
            bool previousAuto = false; int previousQuality = 0;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float timeout = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < timeout) yield return null;
                yield return null; yield return null;
                flow = Object.FindAnyObjectByType<GameFlow>();
                ev = Object.Instantiate(System.Array.Find(flow.events, e => e != null && e.eventId == "e1"));
                ev.night = true; ev.sky = null; ev.aiCountOverride = 0; ev.pickupCount = 0;
                previousAuto = PlatformGlue.Save.Data.settings.qualityAuto; previousQuality = PlatformGlue.Save.Data.settings.quality;
                PlatformGlue.Save.Data.settings.qualityAuto = false; PlatformGlue.Save.Data.settings.quality = 0;
                flow.StartRace(ev, flow.tiers[0]);
                var race = Object.FindAnyObjectByType<RaceManager>();
                while (race.State == RaceState.Countdown) yield return null;
                PlatformGlue.ApplyQuality();
                Assert.AreEqual("Mobile", QualitySettings.names[QualitySettings.GetQualityLevel()]);
                var camera = Camera.main;
                var path = race.Player.Progress.Path;
                float corner = PropLayout.SharpestCorners(path.Spline, 3, 82f)[0].d;
                var follow = camera.GetComponent<CarCameraFollow>(); if (follow != null) follow.enabled = false;
                var shake = camera.GetComponent<CameraImpulse>(); if (shake != null) shake.enabled = false;
                Directory.CreateDirectory(dir);
                foreach (float lead in new[] { 33f, 5f })
                {
                    float d = corner - lead;
                    path.Pose(d, 0f, out var p, out var rot);
                    race.Player.Car.Teleport(p + Vector3.up * .4f, rot);
                    camera.transform.SetPositionAndRotation(p + Vector3.up * 3.2f - rot * Vector3.forward * 5.2f,
                        Quaternion.LookRotation(path.Position(d + 14f) + Vector3.up * 1.2f - (p + Vector3.up * 3.2f - rot * Vector3.forward * 5.2f)));
                    camera.fieldOfView = 62f;
                    string point = lead > 10f ? "approach" : "apex";
                    TrackForkPlayTests.Capture(camera, Path.Combine(dir, point + ".png"));
                    Debug.Log($"[NightReflector] {point} camera={camera.transform.position:F3} rotation={camera.transform.rotation:F4} corner={corner:F2} profile={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
                }
            }
            finally
            {
                if (flow != null) flow.EnterMenu();
                if (ev != null) Object.Destroy(ev);
                if (flow != null) { PlatformGlue.Save.Data.settings.qualityAuto = previousAuto; PlatformGlue.Save.Data.settings.quality = previousQuality; }
                QualitySettings.SetQualityLevel(quality, true);
                Time.captureDeltaTime = clock;
            }
        }
    }
}
