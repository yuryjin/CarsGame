using System.Collections;
using UnityEngine.TestTools;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using RacingGame.Core.Combat;
using RacingGame.Core.Sim;

namespace RacingGame.Tests
{
    public class KillFeedTests
    {
        [Test]
        public void PushRotatesSlotsAndDropsOldest()
        {
            var buffer = new KillFeedBuffer();
            buffer.RegisterRacerName(1, "You");
            buffer.RegisterRacerName(2, "Rex");
            buffer.RegisterRacerName(3, "Nova");
            buffer.RegisterRacerName(4, "Bolt");
            buffer.RegisterRacerName(5, "Vega");

            // 1st Kill: Rex kills Nova
            buffer.Push(2, 3);
            Assert.AreEqual(1, buffer.ActiveCount);
            Assert.AreEqual("Rex × Nova", buffer.GetEntry(0).FormattedText);
            Assert.IsTrue(buffer.GetEntry(0).IsActive);

            // 2nd Kill: Bolt kills Vega
            buffer.Push(4, 5);
            Assert.AreEqual(2, buffer.ActiveCount);
            Assert.AreEqual("Bolt × Vega", buffer.GetEntry(0).FormattedText);
            Assert.AreEqual("Rex × Nova", buffer.GetEntry(1).FormattedText);

            // 3rd Kill: You kill Rex
            buffer.Push(1, 2);
            Assert.AreEqual(3, buffer.ActiveCount);
            Assert.AreEqual("You × Rex", buffer.GetEntry(0).FormattedText);
            Assert.AreEqual("Bolt × Vega", buffer.GetEntry(1).FormattedText);
            Assert.AreEqual("Rex × Nova", buffer.GetEntry(2).FormattedText);

            // 4th Kill: Nova kills Bolt (Rex × Nova should be pushed out)
            buffer.Push(3, 4);
            Assert.AreEqual(3, buffer.ActiveCount);
            Assert.AreEqual("Nova × Bolt", buffer.GetEntry(0).FormattedText);
            Assert.AreEqual("You × Rex", buffer.GetEntry(1).FormattedText);
            Assert.AreEqual("Bolt × Vega", buffer.GetEntry(2).FormattedText);
        }

        [Test]
        public void DurationDecayAndFadeAlpha()
        {
            var buffer = new KillFeedBuffer();
            buffer.RegisterRacerName(1, "Rex");
            buffer.RegisterRacerName(2, "Nova");

            buffer.Push(1, 2, 4.0f);
            Assert.AreEqual(1, buffer.ActiveCount);
            Assert.AreEqual(4.0f, buffer.GetEntry(0).TimeRemaining, 0.001f);
            Assert.AreEqual(1.0f, buffer.GetEntry(0).Alpha, 0.001f);

            // Advance 2 seconds -> half duration, full alpha
            buffer.Update(2.0f);
            Assert.AreEqual(2.0f, buffer.GetEntry(0).TimeRemaining, 0.001f);
            Assert.AreEqual(1.0f, buffer.GetEntry(0).Alpha, 0.001f);
            Assert.IsTrue(buffer.GetEntry(0).IsActive);

            // Advance 1.5 seconds -> 0.5s remaining, alpha = 0.5 (fading in last 1.0s)
            buffer.Update(1.5f);
            Assert.AreEqual(0.5f, buffer.GetEntry(0).TimeRemaining, 0.001f);
            Assert.AreEqual(0.5f, buffer.GetEntry(0).Alpha, 0.001f);
            Assert.IsTrue(buffer.GetEntry(0).IsActive);

            // Advance 0.6 seconds -> expired
            buffer.Update(0.6f);
            Assert.AreEqual(0.0f, buffer.GetEntry(0).TimeRemaining, 0.001f);
            Assert.AreEqual(0.0f, buffer.GetEntry(0).Alpha, 0.001f);
            Assert.IsFalse(buffer.GetEntry(0).IsActive);
            Assert.AreEqual(0, buffer.ActiveCount);
        }

        [Test]
        public void MultipleSlotsDecayIndependently()
        {
            var buffer = new KillFeedBuffer();
            buffer.RegisterRacerName(1, "Rex");
            buffer.RegisterRacerName(2, "Nova");
            buffer.RegisterRacerName(3, "Bolt");

            buffer.Push(1, 2, 4.0f); // t = 0
            buffer.Update(2.0f);     // t = 2 (entry in slot 0 has 2.0s left)

            buffer.Push(2, 3, 4.0f); // t = 2 (new entry in slot 0 has 4.0s left, slot 1 has 2.0s left)
            Assert.AreEqual(2, buffer.ActiveCount);
            Assert.AreEqual(4.0f, buffer.GetEntry(0).TimeRemaining, 0.001f);
            Assert.AreEqual(2.0f, buffer.GetEntry(1).TimeRemaining, 0.001f);

            buffer.Update(2.5f);     // t = 4.5: slot 1 (originally 2.0s) expired, slot 0 (originally 4.0s) has 1.5s left
            Assert.AreEqual(1, buffer.ActiveCount);
            Assert.IsTrue(buffer.GetEntry(0).IsActive);
            Assert.AreEqual(1.5f, buffer.GetEntry(0).TimeRemaining, 0.001f);
            Assert.IsFalse(buffer.GetEntry(1).IsActive);
        }

        [Test]
        public void NameRegistrationAndFallbackFormatting()
        {
            var buffer = new KillFeedBuffer();
            buffer.RegisterRacerName(1, "You");
            buffer.RegisterRacerName(2, "Rex");

            Assert.AreEqual("Rex × You", buffer.GetKillText(2, 1));
            Assert.AreEqual("Car 99", buffer.GetRacerName(99));
            Assert.AreEqual("Car 99 × Rex", buffer.GetKillText(99, 2));

            // Update registered name -> string cache is updated
            buffer.RegisterRacerName(2, "Nova");
            Assert.AreEqual("Nova × You", buffer.GetKillText(2, 1));
        }

        [Test]
        public void ClearResetsAllSlots()
        {
            var buffer = new KillFeedBuffer();
            buffer.RegisterRacerName(1, "A");
            buffer.RegisterRacerName(2, "B");
            buffer.RegisterRacerName(3, "C");

            buffer.Push(1, 2);
            buffer.Push(2, 3);
            buffer.Push(3, 1);
            Assert.AreEqual(3, buffer.ActiveCount);

            buffer.Clear();
            Assert.AreEqual(0, buffer.ActiveCount);
            Assert.IsFalse(buffer.GetEntry(0).IsActive);
            Assert.IsFalse(buffer.GetEntry(1).IsActive);
            Assert.IsFalse(buffer.GetEntry(2).IsActive);
        }

        [Test]
        public void PushAndUpdateDoNotAllocateGC()
        {
            var buffer = new KillFeedBuffer();
            for (int i = 1; i <= 8; i++) buffer.RegisterRacerName(i, $"Car{i}");
            buffer.PrecacheAll();

            // Warmup JIT
            for (int i = 0; i < 50; i++)
            {
                buffer.Push(1 + (i % 7), 1 + ((i + 1) % 7));
                buffer.Update(0.1f);
            }

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                buffer.Push(1 + (i % 7), 1 + ((i + 1) % 7));
                buffer.Update(0.05f);
            }
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, $"Allocated {allocated} bytes during kill feed push/update");
        }

        [UnityTest]
        public IEnumerator HudKillEventPathDoesNotRebuildNameCacheAfterWarmup()
        {
            yield return new EnterPlayMode();
            var root = new GameObject("HudKillFeedAllocationTest");
            root.SetActive(false);
            try
            {
                var director = RaceDirector.Instance; // the same scene director that the HUD subscribes to
                var race = root.AddComponent<RaceManager>();

                var racerA = CreateParticipant(root.transform, "Rex");
                var racerB = CreateParticipant(root.transform, "Nova");
                var participantsField = typeof(RaceManager).GetField("participants", BindingFlags.Instance | BindingFlags.NonPublic);
                var participants = (List<RaceParticipant>)participantsField.GetValue(race);
                participants.Add(racerA);
                participants.Add(racerB);

                var hud = root.AddComponent<HudScreen>();
                hud.race = race;
                root.SetActive(true);
                // Re-run the idempotent subscription path after activation so the test remains
                // independent of Unity's component Awake/OnEnable ordering in batch mode.
                typeof(HudScreen).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
                int killerId = racerA.Car.CarId;
                int victimId = racerB.Car.CarId;
                Assert.AreNotEqual(killerId, victimId);

                // The first event initializes localized names and all killer/victim text pairs.
                director.RegisterKill(new KillEvent(killerId, victimId));

                // Warm event dispatch and TMP/UI state before measuring the actual HUD path.
                for (int i = 0; i < 20; i++) director.RegisterKill(new KillEvent(killerId, victimId));

                long before = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 100; i++) director.RegisterKill(new KillEvent(killerId, victimId));
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.AreEqual(0, allocated, $"Allocated {allocated} bytes while dispatching warmed HUD kill events");
                Assert.AreEqual(3, hud.KillFeed.ActiveCount);
                Assert.AreEqual(
                    KillFeedBuffer.FormatKill(hud.KillFeed.GetRacerName(killerId), hud.KillFeed.GetRacerName(victimId)),
                    hud.KillFeed.GetEntry(0).FormattedText);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreEditMode()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        private static RaceParticipant CreateParticipant(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var participant = go.AddComponent<RaceParticipant>();
            participant.displayName = name;
            return participant;
        }

        [Test]
        public void RaceDirectorRegistersKillEvent()
        {
            var go = new GameObject("TestDriver");
            var driver = go.AddComponent<SimulationDriver>();
            var director = go.AddComponent<RaceDirector>();

            KillEvent received = default;
            bool fired = false;
            director.OnKillRegistered += evt =>
            {
                fired = true;
                received = evt;
            };

            director.RegisterKill(2, 3);
            Assert.IsTrue(fired);
            Assert.AreEqual(2, received.KillerId);
            Assert.AreEqual(3, received.VictimId);

            Object.DestroyImmediate(go);
        }
    }
}
