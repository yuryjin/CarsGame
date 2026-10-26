// Copy into an isolated PlayMode assembly for actual third-person framing evidence.
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Economy;
using Object = UnityEngine.Object;
namespace RacingGame.Tests
{
    public sealed class CameraBodySurvey
    {
        [UnityTest, Timeout(600000)]
        public IEnumerator SurveyEveryBodyFromTheActualChaseCamera()
        {
            float capture = Time.captureDeltaTime;
            string saved = null; GameFlow flow = null;
            try
            {
                Time.captureDeltaTime = SimulationDriver.FixedStep;
                yield return SceneManager.LoadSceneAsync("SampleScene");
                float until = Time.realtimeSinceStartup + 60f;
                while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsTrue(PlatformGlue.IsReady); yield return null; yield return null;
                saved = JsonUtility.ToJson(PlatformGlue.Save.Data);
                flow = Object.FindAnyObjectByType<GameFlow>();
                var race = Object.FindAnyObjectByType<RaceManager>();
                string output = Environment.GetEnvironmentVariable("CG_CAMERA_SURVEY_OUT");
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output,"framing.csv"),"body,xMin,xMax,yMin,yMax,nearest\n");
                foreach (var body in CarBodies.All)
                {
                    var progress = PlatformGlue.Save.Data.progress;
                    if (!progress.cosmetics.Contains(body.OwnedKey)) progress.cosmetics.Add(body.OwnedKey);
                    progress.body = body.Id;
                    Assert.IsTrue(flow.StartPractice(PracticeLesson.Drift));
                    var car = race.Player.Car;
                    Assert.AreEqual(body.Id,car.GetComponent<CarBody>().BodyId);
                    car.Body.constraints = RigidbodyConstraints.FreezeAll;
                    while (race.State == RaceState.Countdown) yield return null;
                    car.inputEnabled = false;
                    var follow = Object.FindAnyObjectByType<CarCameraFollow>();
                    follow.SetTarget(car.transform);
                    for (int i = 0; i < 5; i++) yield return null;
                    var camera = follow.GetComponent<Camera>();
                    var previousTarget = camera.targetTexture;
                    var previousActive = RenderTexture.active;
                    var target = new RenderTexture(640,360,24);
                    var pixels = new Texture2D(640,360,TextureFormat.RGB24,false);
                    try
                    {
                        camera.targetTexture = target;
                        camera.Render();
                        RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0,0,640,360),0,0); pixels.Apply();
                        File.WriteAllBytes(Path.Combine(output,body.Id+".png"),pixels.EncodeToPNG());
                        Vector3 lo = Vector3.one * float.PositiveInfinity, hi = Vector3.one * float.NegativeInfinity;
                        foreach (var filter in car.GetComponentsInChildren<MeshFilter>())
                        {
                            var renderer = filter.GetComponent<MeshRenderer>();
                            if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                            foreach (var vertex in filter.sharedMesh.vertices)
                            {
                                Vector3 projected = camera.WorldToViewportPoint(filter.transform.TransformPoint(vertex));
                                lo = Vector3.Min(lo,projected); hi = Vector3.Max(hi,projected);
                            }
                        }
                        File.AppendAllText(Path.Combine(output,"framing.csv"),FormattableString.Invariant($"{body.Id},{lo.x:F4},{hi.x:F4},{lo.y:F4},{hi.y:F4},{lo.z:F4}\n"));
                    }
                    finally
                    {
                        camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
                        target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
                    }
                    flow.EnterMenu(); yield return null; yield return null;
                }
            }
            finally
            {
                if (flow != null) flow.EnterMenu();
                if (saved != null && PlatformGlue.Save != null) JsonUtility.FromJsonOverwrite(saved,PlatformGlue.Save.Data);
                Time.captureDeltaTime = capture; PauseController.ResetAll();
            }
        }
    }
}
