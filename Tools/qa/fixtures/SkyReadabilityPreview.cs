// Optional GPU visual comparison. Copy into an isolated PlayMode test assembly only.
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.World;
using RacingGame.Core.Platform;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class SkyReadabilityPreview
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator CaptureSpaceLightingComparisons()
        {
            string output = Environment.GetEnvironmentVariable("CG_SKY_SHOTS");
            Assert.IsNotEmpty(output); Directory.CreateDirectory(output);
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady); yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            string originalBody = race.spawner.playerBodyId;
            var target = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            Camera camera = null; SkyPreset preset = null;
            try
            {
                foreach (string body in new[] { "sidewinder", "classic" })
                foreach (string eventId in new[] { "e17", "e19" })
                {
                    race.spawner.playerBodyId = body;
                    flow.StartRace(Array.Find(flow.events, e => e.eventId == eventId), flow.tiers[0]);
                    PauseController.Set(PauseReason.User, true);
                    var car = race.Player.Car; var path = TrackPath.Instance;
                    Assert.AreEqual(body, car.GetComponent<CarBody>().BodyId);
                    float distance = eventId == "e19" ? 340f : 120f;
                    Vector3 forward = (path.Position(distance + 1f) - path.Position(distance)).normalized;
                    car.Teleport(path.Position(distance) + Vector3.up * .06f, Quaternion.LookRotation(forward));
                    camera = Camera.main;
                    var originalPreset = SkyDirector.Instance.Current;
                    preset = Object.Instantiate(originalPreset);
                    foreach (string variant in (Environment.GetEnvironmentVariable("CG_SKY_VARIANTS") ?? "baseline,fill,reflection,reflection-strong").Split(','))
                    {
                        preset.ambientSky = originalPreset.ambientSky; preset.ambientEquator = originalPreset.ambientEquator;
                        preset.ambientGround = originalPreset.ambientGround; preset.sunIntensity = originalPreset.sunIntensity;
                        if (variant == "fill")
                        {
                            preset.ambientSky = new Color(.50f, .56f, .72f);
                            preset.ambientEquator = new Color(.42f, .46f, .60f);
                            preset.ambientGround = new Color(.20f, .23f, .32f);
                            preset.sunIntensity = .8f;
                        }
                        SkyDirector.Instance.Apply(preset);
                        if (variant.StartsWith("reflection"))
                        {
                            Color fill = (variant == "reflection" ? new Color(.38f, .42f, .50f) : new Color(.55f, .60f, .70f)).linear;
                            var parameters = preset.ToParams();
                            parameters.Zenith += fill; parameters.Horizon += fill * .55f; parameters.Ground += fill * .18f;
                            SkyMath.FillCubemap((Cubemap)RenderSettings.customReflectionTexture, parameters, null);
                        }
                        File.AppendAllText(Path.Combine(output, "lighting.txt"), $"{eventId} {body} {variant}: sun={SkyDirector.Instance.sun.isActiveAndEnabled}/{SkyDirector.Instance.sun.intensity}, SH={RenderSettings.ambientProbe[0,0]},{RenderSettings.ambientProbe[1,0]},{RenderSettings.ambientProbe[2,0]}\n");
                        yield return null; yield return null;
                        camera.transform.position = car.transform.position - forward * 8f + Vector3.up * 3.2f;
                        camera.transform.LookAt(car.transform.position + forward * 5f + Vector3.up * .7f);
                        camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                        File.WriteAllBytes(Path.Combine(output, $"{eventId}-{body}-{variant}.png"), image.EncodeToPNG());
                        camera.targetTexture = null; RenderTexture.active = previous;
                    }
                    SkyDirector.Instance.Apply(originalPreset); Object.Destroy(preset); preset = null;
                    flow.EnterMenu(); PauseController.ResetAll(); yield return null; yield return null;
                }
                File.WriteAllText(Path.Combine(output, "renderer.txt"), SystemInfo.graphicsDeviceName + " / " + SystemInfo.graphicsDeviceType);
            }
            finally
            {
                if (camera != null) camera.targetTexture = null;
                RenderTexture.active = previous; target.Release(); Object.Destroy(target); Object.Destroy(image);
                if (preset != null) Object.Destroy(preset);
                race.spawner.playerBodyId = originalBody; flow.EnterMenu(); PauseController.ResetAll();
            }
        }
    }
}
