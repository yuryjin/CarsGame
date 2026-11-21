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
        [TestCase(false)]
        [TestCase(true)]
        public void SectorTimesInterpolateEveryLapAndIgnoreRecoveryCuts(bool reversed)
        {
            var root = new GameObject("SectorTestTrack");
            try
            {
                foreach (var point in new[] { Vector3.zero, Vector3.forward * 30, new Vector3(30, 0, 30), Vector3.right * 30 })
                {
                    var marker = new GameObject("Dash").transform;
                    marker.SetParent(root.transform);
                    marker.position = point;
                }
                var path = root.AddComponent<TrackPath>();
                path.startLineDistance = 0f;
                path.reversed = reversed;
                path.Build();
                var record = new GhostRecord();
                for (int d = -3; d <= 240; d += 3)
                    record.AddPose(path.Position(d), Quaternion.identity, (d + 3) * 0.1, 0f, 0);
                var totals = GhostSectorTimes.Measure(path, GhostRecord.FromBytes(record.ToBytes()), 2);
                Assert.NotNull(totals);
                Assert.That(totals[0], Is.EqualTo(8300).Within(2));
                Assert.That(totals[1], Is.EqualTo(8000).Within(2));
                Assert.That(totals[2], Is.EqualTo(8000).Within(2));
                Assert.IsNull(GhostSectorTimes.Measure(path, record, 1), "Different lap counts cannot be compared.");
                var cut = record.Samples[15]; // from 39 m to 42 m across the first split
                cut.Flags = GhostRecord.TeleportFlag;
                record.Samples[15] = cut;
                Assert.IsNull(GhostSectorTimes.Measure(path, record, 2));
            }
            finally { Object.DestroyImmediate(root); }
        }

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
