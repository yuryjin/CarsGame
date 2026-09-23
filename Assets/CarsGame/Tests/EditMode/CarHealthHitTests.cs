using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Combat;

namespace RacingGame.Tests
{
    public class CarHealthHitTests
    {
        private GameObject testGo;
        private GameObject attackerGo;
        private CarHealth health;
        private CarStatus status;

        [SetUp]
        public void SetUp()
        {
            testGo = new GameObject("TestCar_Health");
            _ = testGo.AddComponent<Rigidbody>();
            health = testGo.AddComponent<CarHealth>();
            status = testGo.AddComponent<CarStatus>();
            health.maxHealth = 100f;
            health.SetMaxHealth(100f, true);
            health.respawnOnDeath = false;
            CarHealth.GlobalDamageMultiplier = 1f;

            attackerGo = new GameObject("AttackerCar");
        }

        [TearDown]
        public void TearDown()
        {
            if (testGo != null) Object.DestroyImmediate(testGo);
            if (attackerGo != null) Object.DestroyImmediate(attackerGo);
            CarHealth.GlobalDamageMultiplier = 1f;
        }

        [Test]
        public void ApplyHit_StandardDamage_ReducesHealthAndSetsAttacker()
        {
            var hit = new HitInfo
            {
                Attacker = attackerGo,
                Damage = 30f,
                HitType = HitType.Projectile
            };

            var result = health.ApplyHit(in hit);

            Assert.IsTrue(result.DamageApplied);
            Assert.IsFalse(result.ShieldAbsorbed);
            Assert.IsFalse(result.Fatal);
            Assert.AreEqual(30f, result.DamageDealt, 1e-4f);
            Assert.AreEqual(70f, health.Health, 1e-4f);
            Assert.AreEqual(attackerGo, health.LastAttacker);
        }

        [Test]
        public void ApplyHit_WhenInvulnerable_RejectsHit()
        {
            health.SetInvulnerable(2f);

            var hit = new HitInfo
            {
                Attacker = attackerGo,
                Damage = 40f,
                HitType = HitType.Direct
            };

            var result = health.ApplyHit(in hit);

            Assert.IsFalse(result.DamageApplied);
            Assert.AreEqual(100f, health.Health, 1e-4f);
        }

        [Test]
        public void ApplyHit_FatalDamage_TriggersDeathAndSetsFatalFlag()
        {
            bool diedFired = false;
            GameObject diedAttacker = null;
            health.OnDied += (att) =>
            {
                diedFired = true;
                diedAttacker = att;
            };

            var hit = new HitInfo
            {
                Attacker = attackerGo,
                Damage = 120f,
                HitType = HitType.Projectile
            };

            var result = health.ApplyHit(in hit);

            Assert.IsTrue(result.DamageApplied);
            Assert.IsTrue(result.Fatal);
            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0f, health.Health, 1e-4f);
            Assert.IsTrue(diedFired);
            Assert.AreEqual(attackerGo, diedAttacker);

            // Subsequent hits while dead are rejected
            var hitAfterDeath = new HitInfo { Damage = 10f };
            var resultAfterDeath = health.ApplyHit(in hitAfterDeath);
            Assert.IsFalse(resultAfterDeath.DamageApplied);
        }

        [Test]
        public void ApplyHit_WithActiveShield_AbsorbsHitCompletely()
        {
            status.ApplyStatus(StatusKind.Shield, 100);
            Assert.IsTrue(status.HasShield);

            var hit = new HitInfo
            {
                Attacker = attackerGo,
                Damage = 50f,
                HitType = HitType.Projectile
            };

            var result = health.ApplyHit(in hit);

            Assert.IsTrue(result.DamageApplied);
            Assert.IsTrue(result.ShieldAbsorbed);
            Assert.AreEqual(0f, result.DamageDealt, 1e-4f);
            Assert.IsFalse(result.Fatal);
            Assert.AreEqual(100f, health.Health, 1e-4f);
            Assert.IsFalse(status.HasShield); // Shield consumed

            // Next hit penetrates
            var nextHit = new HitInfo { Damage = 25f, Attacker = attackerGo };
            var nextResult = health.ApplyHit(in nextHit);
            Assert.IsTrue(nextResult.DamageApplied);
            Assert.IsFalse(nextResult.ShieldAbsorbed);
            Assert.AreEqual(25f, nextResult.DamageDealt, 1e-4f);
            Assert.AreEqual(75f, health.Health, 1e-4f);
        }

        [Test]
        public void ApplyHit_WithStatusPayload_AppliesStatusWhenNotShielded()
        {
            var hit = new HitInfo
            {
                Attacker = attackerGo,
                Damage = 15f,
                Status = StatusKind.Frozen,
                StatusDuration = 2.0f,
                HitType = HitType.Projectile
            };

            var result = health.ApplyHit(in hit);

            Assert.IsTrue(result.DamageApplied);
            Assert.IsTrue(result.StatusApplied);
            Assert.IsTrue(status.IsFrozen);
            Assert.AreEqual(85f, health.Health, 1e-4f);
        }

        [Test]
        public void ApplyHit_WithStatusPayload_ShieldBlocksBothDamageAndStatus()
        {
            status.ApplyStatus(StatusKind.Shield, 100);

            var hit = new HitInfo
            {
                Attacker = attackerGo,
                Damage = 25f,
                Status = StatusKind.Frozen,
                StatusDuration = 2.0f,
                HitType = HitType.Projectile
            };

            var result = health.ApplyHit(in hit);

            Assert.IsTrue(result.DamageApplied);
            Assert.IsTrue(result.ShieldAbsorbed);
            Assert.IsFalse(result.StatusApplied);
            Assert.IsFalse(status.IsFrozen);
            Assert.AreEqual(100f, health.Health, 1e-4f);
        }

        [Test]
        public void TakeDamage_BackwardCompatibility_WorksAsExpected()
        {
            status.ApplyStatus(StatusKind.Shield, 100);

            // Shielded -> returns false because HP did not decrease
            bool shieldedDamage = health.TakeDamage(30f, attackerGo);
            Assert.IsFalse(shieldedDamage);
            Assert.AreEqual(100f, health.Health, 1e-4f);

            // Shield consumed -> next TakeDamage succeeds and decreases HP
            bool unshieldedDamage = health.TakeDamage(30f, attackerGo);
            Assert.IsTrue(unshieldedDamage);
            Assert.AreEqual(70f, health.Health, 1e-4f);
        }

        [Test]
        public void ZeroAllocations_ApplyHit_InHotPath()
        {
            var hit = new HitInfo
            {
                Attacker = attackerGo,
                Damage = 1f,
                HitType = HitType.Projectile
            };

            // Warm up
            for (int i = 0; i < 10; i++)
            {
                health.ApplyHit(in hit);
            }

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++)
            {
                health.ApplyHit(in hit);
            }
            long after = System.GC.GetAllocatedBytesForCurrentThread();

            Assert.AreEqual(before, after, "ApplyHit must produce 0 GC allocations.");
        }
    }
}
