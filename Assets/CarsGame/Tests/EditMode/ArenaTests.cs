using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public class ArenaTests
    {
        [UnityTest]
        public IEnumerator ArenaSupportsSpawningPursuitAndUnoccupiedRecovery()
        {
            yield return new EnterPlayMode();
            var arena = ArenaLayout.Create(null);
            var cars = new GameObject("ArenaTestCars");
            try
            {
                var positions = new Vector3[8];
                for (int i = 0; i < positions.Length; i++)
                {
                    arena.GetSpawnPose(i, positions.Length, out positions[i], out var rotation);
                    Vector3 inward = arena.transform.position - positions[i]; inward.y = 0f;
                    Assert.Greater(Vector3.Dot(rotation * Vector3.forward, inward.normalized), 0.99f);
                    for (int j = 0; j < i; j++) Assert.Greater(Vector3.Distance(positions[i], positions[j]), 5f);
                }
                Physics.SyncTransforms();
                Assert.IsTrue(Physics.Raycast(positions[0], Vector3.down, out var ground, 2f));
                Assert.AreEqual(LayerMask.NameToLayer("Track"), ground.collider.gameObject.layer);

                // Keep this pursuit check outside the bridge approach, with clear sight of the first spawn.
                var car = CreateCar(cars.transform, arena.transform.position + new Vector3(0f, 0.5f, 80f));
                var rival = CreateCar(cars.transform, positions[0]);
                var driver = car.gameObject.AddComponent<ArenaDriver>();
                driver.Initialize(arena, null);
                var command = driver.Sample(1);
                Assert.Greater(command.Throttle, 0f);
                Assert.AreEqual(0f, command.Steer, 0.01f);

                Assert.IsTrue(arena.TryGetRecoveryPose(car, out var recovery, out _));
                Assert.Greater(Vector3.Distance(recovery, rival.transform.position), 5f, "Recovery must avoid the occupied first slot.");
                car.Teleport(recovery, Quaternion.identity);
                Assert.IsTrue(arena.TryGetRecoveryPose(car, out var sameSpot, out _));
                Assert.Less(Vector3.Distance(recovery, sameSpot), 0.01f, "The car's own collider must not block recovery.");

                car.Teleport(arena.transform.position + Vector3.up * 0.5f, Quaternion.identity);
                rival.Teleport(car.transform.position + Vector3.right * 20f, Quaternion.identity);
                Physics.SyncTransforms();
                Assert.Greater(driver.Sample(2).Steer, 0f, "Turn towards the rival on the right.");
                rival.gameObject.SetActive(false);
                Assert.IsTrue(driver.Sample(3).Brake, "Stop at the centre when there are no live targets.");

                car.Teleport(arena.transform.position - Vector3.up * 3f, Quaternion.identity);
                Assert.IsTrue(driver.Sample(4).ResetEdge, "A fallen car must recover onto the arena.");
                car.Teleport(arena.transform.position + Vector3.up * 0.5f, Quaternion.Euler(0f, 0f, 180f));
                bool reset = false;
                for (int tick = 5; tick < 165; tick++) reset |= driver.Sample(tick).ResetEdge;
                Assert.IsTrue(reset, "An overturned car must recover even at the centre.");
            }
            finally
            {
                Object.DestroyImmediate(cars);
                Object.DestroyImmediate(arena.gameObject);
            }
        }

        private static CarController CreateCar(Transform parent, Vector3 position)
        {
            var go = new GameObject("ArenaTestCar");
            go.SetActive(false);
            go.transform.SetParent(parent);
            go.transform.position = position;
            var box = go.AddComponent<BoxCollider>();
            box.center = Vector3.up * 0.45f;
            box.size = new Vector3(1.85f, 0.7f, 4.2f);
            var car = go.AddComponent<CarController>();
            car.playerControlled = false;
            go.SetActive(true);
            return car;
        }

        [UnityTearDown]
        public IEnumerator ExitPlayModeAfterTest()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
