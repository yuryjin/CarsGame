using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Economy;
using RacingGame.Core.Ghost;
using Object = UnityEngine.Object;

namespace RacingGame.Tests
{
    public sealed class GhostBodyVisualsPlayTests
    {
        private readonly List<GameObject> ghosts = new List<GameObject>();
        private GameFlow flow;
        [TearDown] public void Cleanup()
        {
            foreach (var ghost in ghosts) if (ghost != null) Object.DestroyImmediate(ghost);
            ghosts.Clear();
            if (flow != null) flow.EnterMenu();
            PauseController.ResetAll();
        }

        private static List<MeshFilter> VisibleMeshes(GameObject root)
        {
            var list = new List<MeshFilter>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer != null && renderer.enabled && filter.sharedMesh != null) list.Add(filter);
            }
            return list;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator EveryRecordedBodyRetainsItsVisibleGeometryWithoutJoiningTheRace()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>();
            var prefab = Object.FindAnyObjectByType<RaceManager>().spawner.raceCarPrefab;
            var failures = new List<string>();
            int index = 0;
            foreach (var body in CarBodies.All)
            {
                // A real spawned car is the visual reference, not a second copy of GhostCar's template algorithm.
                var source = Object.Instantiate(prefab, new Vector3(0, 1000, 0), Quaternion.identity);
                try
                {
                    CarBody.Apply(source, body);
                    var original = VisibleMeshes(source);
                    var record = new GhostRecord { BodyId = body.Id, PlayerName = body.Id };
                    Vector3 position = new Vector3(index % 5 * 6f, 500f, index / 5 * 7f);
                    record.AddPose(position, Quaternion.identity, 0d, 0f, 0);
                    record.AddPose(position, Quaternion.identity, 1d, 0f, 0);
                    record = GhostRecord.FromBytes(record.ToBytes());
                    int cars = RaceDirector.Instance.Cars.Count;
                    var ghost = GhostCar.Spawn(prefab, record, new Color(.35f, .85f, 1f, .55f));
                    ghosts.Add(ghost.gameObject);
                    Assert.AreEqual(cars, RaceDirector.Instance.Cars.Count, body.Id);
                    Assert.IsEmpty(ghost.GetComponentsInChildren<Collider>(true), body.Id);
                    Assert.IsEmpty(ghost.GetComponentsInChildren<Rigidbody>(true), body.Id);
                    Assert.AreEqual(1, ghost.GetComponentsInChildren<MonoBehaviour>(true).Length, body.Id);
                    var replay = VisibleMeshes(ghost.gameObject);
                    Assert.AreEqual(1, replay.Count, body.Id + ": one visual draw per ghost");
                    Assert.AreEqual(1, replay[0].sharedMesh.subMeshCount);
                    var expected = LocalTriangleVertices(source.transform, original);
                    var actual = LocalTriangleVertices(ghost.transform, replay);
                    Assert.AreEqual(expected.Count, actual.Count, body.Id + ": no surface may be lost or added");
                    float maxError = 0f;
                    for (int i = 0; i < expected.Count; i++)
                        maxError = Mathf.Max(maxError, Vector3.Distance(expected[i], actual[i]));
                    var duplicate = GhostCar.Spawn(prefab, record, Color.cyan);
                    try
                    {
                        Assert.AreSame(replay[0].sharedMesh, duplicate.GetComponent<MeshFilter>().sharedMesh,
                            body.Id + ": repeated opponents reuse the cached mesh");
                        Assert.AreEqual(cars, RaceDirector.Instance.Cars.Count);
                    }
                    finally { Object.DestroyImmediate(duplicate.gameObject); }
                    int draws = replay[0].sharedMesh.subMeshCount, triangles = actual.Count / 3;
                    if (maxError > .005f) failures.Add(body.Id + ": " + maxError.ToString("F4") + " m");
                    Debug.Log($"[GhostBody] {body.Id} parts={replay.Count} submeshDraws={draws} triangles={triangles} maxVertexError={maxError:F5}");
                }
                finally { Object.DestroyImmediate(source); }
                index++;
                yield return null;
            }
            string output = Environment.GetEnvironmentVariable("CG_GHOST_CONTACT_SHEET");
            if (!string.IsNullOrEmpty(output)) Capture(output);
            Assert.IsEmpty(failures, "Ghost geometry differs from the actual selected body: " + string.Join(", ", failures));
            int warmMeshes = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            int warmCars = RaceDirector.Instance.Cars.Count;
            var classicMesh = ghosts.Find(g => g.GetComponent<GhostCar>().Record.BodyId == CarBodies.DefaultId)
                .GetComponent<MeshFilter>().sharedMesh;
            for (int cycle = 0; cycle < 5; cycle++)
            {
                foreach (var body in CarBodies.All)
                {
                    var record = new GhostRecord { BodyId = body.Id };
                    record.AddPose(Vector3.zero, Quaternion.identity, 0d, 0f, 0);
                    var repeat = GhostCar.Spawn(prefab, record, Color.cyan);
                    Object.DestroyImmediate(repeat.gameObject);
                }
                var future = new GhostRecord { BodyId = "future-body-" + cycle };
                future.AddPose(Vector3.zero, Quaternion.identity, 0d, 0f, 0);
                var unknown = GhostCar.Spawn(prefab, future, Color.cyan);
                Assert.AreSame(classicMesh, unknown.GetComponent<MeshFilter>().sharedMesh,
                    "Unknown remote body IDs reuse the classic fallback instead of growing the cache");
                Object.DestroyImmediate(unknown.gameObject);
                yield return null;
                Assert.AreEqual(warmMeshes, Resources.FindObjectsOfTypeAll<Mesh>().Length, "Repeated races cannot allocate new cached geometry");
                Assert.AreEqual(warmCars, RaceDirector.Instance.Cars.Count);
            }
        }

        private static List<Vector3> LocalTriangleVertices(Transform root, List<MeshFilter> filters)
        {
            var list = new List<Vector3>();
            foreach (var filter in filters)
            {
                var matrix = root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var vertices = filter.sharedMesh.vertices;
                for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                    foreach (int vertex in filter.sharedMesh.GetTriangles(submesh))
                        list.Add(matrix.MultiplyPoint3x4(vertices[vertex]));
            }
            return list;
        }

        private static void Capture(string output)
        {
            var cameraObject = new GameObject("GhostContactSheetCamera", typeof(Camera));
            var texture = new RenderTexture(1280, 960, 24);
            var pixels = new Texture2D(1280, 960, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                var camera = cameraObject.GetComponent<Camera>();
                camera.transform.position = new Vector3(12f, 525f, -12f);
                camera.transform.LookAt(new Vector3(12f, 500f, 10.5f));
                camera.orthographic = true; camera.orthographicSize = 14f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.025f, .045f, .075f);
                camera.cullingMask = 1 << LayerMask.NameToLayer("Ignore Raycast");
                camera.nearClipPlane = .1f; camera.farClipPlane = 100f;
                camera.targetTexture = texture; camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes(output, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(cameraObject); texture.Release();
                Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels);
            }
        }
    }
}
