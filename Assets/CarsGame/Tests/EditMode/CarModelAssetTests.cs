using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Economy;

namespace RacingGame.Tests
{
    public class CarModelAssetTests
    {
        [TestCase(BodyClass.Sport)]
        [TestCase(BodyClass.Buggy)]
        [TestCase(BodyClass.Armored)]
        [TestCase(BodyClass.Tank)]
        public void ImportedHullFitsCarSpaceAndSupportsPaintAndReplay(BodyClass cls)
        {
            var prefab = Resources.Load<GameObject>("Art/TripoCars/" + cls);
            Assert.NotNull(prefab);
            Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>());
            var parts = prefab.GetComponentsInChildren<MeshFilter>();
            Assert.AreEqual(2, parts.Length);
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            int triangles = 0, paint = 0;
            foreach (var part in parts)
            {
                var mesh = part.sharedMesh;
                Assert.IsTrue(mesh.isReadable);
                triangles += (int)mesh.GetIndexCount(0) / 3;
                foreach (var v in mesh.vertices)
                    bounds.Encapsulate(prefab.transform.InverseTransformPoint(part.transform.TransformPoint(v)));
                var material = part.GetComponent<MeshRenderer>().sharedMaterial;
                if (material.name.Contains("CarPaint")) paint++;
                else
                {
                    Assert.AreEqual("M_Props", material.name);
                    Assert.AreEqual(mesh.vertexCount, mesh.colors32.Length);
                }
            }
            Assert.AreEqual(1, paint);
            Assert.That(triangles, Is.InRange(3000, 6000));
            Assert.That(bounds.size.x, Is.EqualTo(2f).Within(0.15f));
            Assert.That(bounds.size.z, Is.EqualTo(4f).Within(0.15f));
            Assert.That(bounds.max.y, Is.EqualTo(1f).Within(0.1f));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.06f));
        }
    }
}
