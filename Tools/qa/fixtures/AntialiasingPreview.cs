// Copy into an isolated PlayMode test assembly for a controlled visual comparison.
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Platform;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class AntialiasingPreview
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator CompareStationaryEdgesAtTwoResolutions()
        {
            string output = Environment.GetEnvironmentVariable("CG_AA_SHOTS");
            Assert.IsNotEmpty(output); Directory.CreateDirectory(output);
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady); yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var camera = Camera.main;
            var data = camera.GetComponent("UniversalAdditionalCameraData");
            Assert.NotNull(data);
            var aa = data.GetType().GetProperty("antialiasing");
            var before = aa.GetValue(data);
            var previous = RenderTexture.active;
            try
            {
                foreach (string eventId in new[] { "e1", "e19" })
                {
                    flow.StartRace(Array.Find(flow.events, e => e.eventId == eventId), flow.tiers[0]);
                    PauseController.Set(PauseReason.User, true);
                    var car = race.Player.Car; var path = TrackPath.Instance;
                    float distance = eventId == "e19" ? 340f : 120f;
                    Vector3 forward = (path.Position(distance + 1f) - path.Position(distance)).normalized;
                    car.Teleport(path.Position(distance) + Vector3.up * .06f, Quaternion.LookRotation(forward));
                    foreach (int width in new[] { 640, 1280 })
                    {
                        int height = width * 9 / 16;
                        var target = new RenderTexture(width, height, 24);
                        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                        try
                        {
                            foreach (int mode in new[] { 0, 1, 2 })
                            {
                                aa.SetValue(data, Enum.ToObject(aa.PropertyType, mode));
                                yield return null; yield return null;
                                camera.transform.position = car.transform.position - forward * 8f + Vector3.up * 3.2f;
                                camera.transform.LookAt(car.transform.position + forward * 5f + Vector3.up * .7f);
                                camera.targetTexture = target;
                                camera.Render(); RenderTexture.active = target;
                                image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
                                File.WriteAllBytes(Path.Combine(output, $"{eventId}-{width}-aa{mode}.png"), image.EncodeToPNG());
                                camera.targetTexture = null; RenderTexture.active = previous;
                            }
                        }
                        finally { camera.targetTexture = null; RenderTexture.active = previous; target.Release(); Object.Destroy(target); Object.Destroy(image); }
                    }
                    flow.EnterMenu(); PauseController.ResetAll(); yield return null;
                }
                File.WriteAllText(Path.Combine(output, "renderer.txt"), SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType);
            }
            finally { aa.SetValue(data, before); camera.targetTexture = null; RenderTexture.active = previous; flow.EnterMenu(); PauseController.ResetAll(); }
        }
    }
}
