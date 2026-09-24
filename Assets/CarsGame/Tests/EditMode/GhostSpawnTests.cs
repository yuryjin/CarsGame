using System.Collections;
using NUnit.Framework;
using RacingGame.Core.Ghost;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public class GhostSpawnTests
    {
        [UnityTest]
        public IEnumerator GhostHasVisualsWithoutJoiningPhysicsOrTheRace()
        {
            yield return new EnterPlayMode();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RaceCar.prefab");
            Assert.NotNull(prefab);
            var record = new GhostRecord { PlayerName = "Test" };
            record.Add(new Vector3(12f, 1f, 20f), 45f, 0f, 0);
            int before = RaceDirector.Instance.Cars.Count;
            var ghost = GhostCar.Spawn(prefab, record, Color.cyan);
            try
            {
                yield return null;
                Assert.AreEqual(before, RaceDirector.Instance.Cars.Count);
                Assert.IsEmpty(ghost.GetComponentsInChildren<Collider>());
                Assert.IsEmpty(ghost.GetComponentsInChildren<Rigidbody>());
                Assert.AreEqual(1, ghost.GetComponentsInChildren<MonoBehaviour>().Length);
                Assert.Greater(ghost.GetComponentsInChildren<MeshRenderer>().Length, 0);
                Assert.Less(Vector3.Distance(new Vector3(12f, 1f, 20f), ghost.transform.position), 0.1f);
            }
            finally { Object.DestroyImmediate(ghost.gameObject); }
        }

        [UnityTearDown]
        public IEnumerator ExitPlayModeAfterTest()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
