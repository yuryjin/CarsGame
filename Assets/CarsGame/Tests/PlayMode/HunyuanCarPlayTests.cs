using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    /// <summary>The Hunyuan3D sample car (Art/Cars/HunyuanSample) drives on the real track like the stock car.</summary>
    public sealed class HunyuanCarPlayTests
    {
        private float savedCaptureDelta;
        [SetUp] public void Save() { savedCaptureDelta = Time.captureDeltaTime; }
        [TearDown] public void Restore() { Time.captureDeltaTime = savedCaptureDelta; LogAssert.ignoreFailingMessages = false; }

        [UnityTest, Timeout(300000)]
        public IEnumerator SampleCarDrivesOnTheTrack() { return Drive("Assets/CarsGame/Art/Cars/HunyuanSample/HunyuanSampleCar.prefab"); }

        [UnityTest, Timeout(300000)]
        public IEnumerator StockCarDrivesOnTheTrack() { return Drive("Assets/Prefabs/RaceCar.prefab"); }

        private IEnumerator Drive(string path)
        {
#if UNITY_EDITOR
            LogAssert.ignoreFailingMessages = true;
            Time.captureDeltaTime = SimulationDriver.FixedStep;
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null; yield return null;
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, "baked prefab");
            var track = TrackPath.Instance;
            track.Pose(track.startLineDistance + 25f, 0f, out var pos, out var rot);
            var go = Object.Instantiate(prefab, pos + Vector3.up * 0.4f, rot);
            var car = go.GetComponent<CarController>();
            var input = go.AddComponent<ScriptedDriverInput>();
            car.SetInputSource(input);
            yield return new WaitForFixedUpdate();
            input.Set(1f, 0f);
            Vector3 start = go.transform.position;
            var trail = new System.Text.StringBuilder();
            for (int i = 0; i < 150; i++)   // 3 s: the straight ahead of the grid ends at ~30 m
            {
                yield return null;
                if (i % 20 == 0) trail.Append($" t{i / 50f:0.0}:d{Vector3.Distance(start, go.transform.position):0.0},y{go.transform.position.y:0.00},v{car.Body.linearVelocity.magnitude * 3.6f:0},g{(car.IsGrounded ? 1 : 0)}");
            }
            float travelled = Vector3.Distance(start, go.transform.position);
            float speed = car.Body.linearVelocity.magnitude;
            Debug.Log($"[HunyuanCarPlayTests] {System.IO.Path.GetFileName(path)}{trail} | travelled={travelled:0.0} m speed={speed * 3.6f:0} km/h grounded={car.IsGrounded} surface={car.Surface} up={Vector3.Dot(go.transform.up, Vector3.up):0.00}");
            Assert.Greater(travelled, 20f, "accelerates like a car");
            Assert.Greater(Vector3.Dot(go.transform.up, Vector3.up), 0.9f, "stays upright");
            Assert.IsTrue(car.IsGrounded, "wheels on the ground");
            Object.Destroy(go);
            Time.captureDeltaTime = 0f;
#else
            Assert.Ignore("editor only");
            yield break;
#endif
        }
    }
}
