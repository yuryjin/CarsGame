using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace RacingGame.Tests
{
    /// <summary>The imported prop models stay small: triangle budgets, one mesh each, vertex colours, no UVs.</summary>
    public class PropModelLibraryTests
    {
        private static int Triangles(Mesh mesh)
        {
            long n = 0;
            for (int s = 0; s < mesh.subMeshCount; s++) n += mesh.GetIndexCount(s);
            return (int)(n / 3);
        }

        [Test]
        public void Library_ModelsStayInsideTheSizeBudget()
        {
            var lib = Resources.Load<PropModelLibrary>(PropModelLibrary.ResourcePath);
            Assert.IsNotNull(lib, "Racing/Art/Bake Prop Models has not been run");
            Assert.IsNotNull(lib.material);
            Assert.AreEqual("CarsGame/StylizedLit", lib.material.shader.name);
            Assert.IsFalse(lib.material.enableInstancing);
            int total = 0;
            foreach (var e in lib.entries)
            {
                Assert.IsNotNull(e.mesh, e.id);
                int tris = Triangles(e.mesh);
                total += tris;
                bool pickup = e.id.StartsWith("Pickup_");
                if (pickup) Assert.IsTrue(Enum.TryParse<PickupKind>(e.id.Substring(7), out _), e.id);
                else Assert.IsTrue(e.id.StartsWith("Weapon_") && Enum.TryParse<PrimaryWeaponType>(e.id.Substring(7), out _), e.id);
                Assert.LessOrEqual(tris, pickup ? PropModelLibrary.PickupTrisMax : PropModelLibrary.WeaponTrisMax, e.id);
                Assert.AreEqual(1, e.mesh.subMeshCount, e.id);
                Assert.IsTrue(e.mesh.HasVertexAttribute(VertexAttribute.Color), e.id + " has no vertex colours");
                Assert.IsFalse(e.mesh.HasVertexAttribute(VertexAttribute.TexCoord0), e.id + " carries UVs");
                Assert.IsFalse(e.mesh.HasVertexAttribute(VertexAttribute.Tangent), e.id + " carries tangents");
                Assert.Less(e.mesh.bounds.size.magnitude, 3.5f, e.id + " is larger than the primitive it replaces");
            }
            Assert.LessOrEqual(total, PropModelLibrary.BatchTrisMax);
        }

        [Test]
        public void Library_LookupByKindAndType()
        {
            var lib = Resources.Load<PropModelLibrary>(PropModelLibrary.ResourcePath);
            Assert.IsNotNull(lib);
            foreach (var e in lib.entries)
            {
                bool found = e.id.StartsWith("Pickup_")
                    ? PropModelLibrary.TryGetPickup((PickupKind)Enum.Parse(typeof(PickupKind), e.id.Substring(7)), out var m1, out var mat1) && m1 == e.mesh && mat1 == lib.material
                    : PropModelLibrary.TryGetWeapon((PrimaryWeaponType)Enum.Parse(typeof(PrimaryWeaponType), e.id.Substring(7)), out var m2, out var mat2) && m2 == e.mesh && mat2 == lib.material;
                Assert.IsTrue(found, e.id);
            }
            Assert.IsFalse(PropModelLibrary.TryGetWeapon(PrimaryWeaponType.Boxer, out _, out _), "Boxer has moving parts and keeps its primitives");
        }
    }
}
