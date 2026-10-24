// Optional visual probe. Copy into an isolated project's PlayMode tests; never ship.
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Ghost;
using RacingGame.Core.Platform;
using Object = UnityEngine.Object;
namespace RacingGame.Tests
{
    public sealed class GhostPreview
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator Capture()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            flow.StartRace(Array.Find(flow.events, e => e.eventId == "e1"), flow.tiers[0]);
            PauseController.Set(PauseReason.User, true);
            var player = race.Player.transform;
            var position = player.position + player.right * 4.5f;
            var record = new GhostRecord { PlayerName = "Visual check" };
            record.Add(position, player.eulerAngles.y, 0f, 0);
            var ghost = GhostCar.Spawn(race.spawner.raceCarPrefab, record, new Color(.6f,.8f,1f,.35f));
            var cameraObject = new GameObject("GhostPreviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.CopyFrom(Camera.main);
            camera.transform.position = player.position + player.rotation * new Vector3(9f,4f,9f);
            camera.transform.LookAt(player.position + player.right * 2.25f + Vector3.up * .7f);
            camera.fieldOfView = 42f;
            var target = new RenderTexture(1280,720,24);
            var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            var previous = RenderTexture.active;
            try
            {
                yield return null;
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                string output = Environment.GetEnvironmentVariable("CG_GHOST_SHOT");
                Assert.IsNotEmpty(output);
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                File.WriteAllBytes(output,image.EncodeToPNG());
                foreach(var renderer in ghost.GetComponentsInChildren<MeshRenderer>())
                    foreach(var material in renderer.sharedMaterials)
                        Debug.Log($"[GhostVisual] {renderer.name}: {material.shader.name}, queue={material.renderQueue}");
            }
            finally
            {
                camera.targetTexture = null; RenderTexture.active = previous;
                target.Release(); Object.Destroy(target); Object.Destroy(image);
                Object.Destroy(cameraObject); Object.Destroy(ghost.gameObject);
                flow.EnterMenu(); PauseController.ResetAll();
            }
        }
    }
}
