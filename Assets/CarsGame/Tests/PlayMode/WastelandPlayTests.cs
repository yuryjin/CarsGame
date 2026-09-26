using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Track;

namespace RacingGame.Tests
{
    public sealed class WastelandPlayTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator BridgeBoostWallsRamAndCollapseWorkInOwnEvent()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var path = TrackPath.Instance;
            var ev = Resources.Load<RaceEventDefinition>("Events/Event_e20");
            Assert.IsNotNull(ev);
            float originalLength = path.Length;
            flow.StartRace(ev, null);
            var map = Object.FindAnyObjectByType<WastelandTrack>();
            Assert.IsNotNull(map);
            Assert.IsNull(Object.FindAnyObjectByType<StarRingTrack>(), "the space map is separate");
            var layout = map.Layout;
            Assert.AreEqual(layout.Length, path.Length, 0.5f);
            Assert.AreEqual(ev.ramPush, CarController.RamPushScale);
            while (race.State == RaceState.Countdown) yield return null;

            var car = race.Player.Car;
            var input = car.gameObject.AddComponent<ScriptedDriverInput>();
            car.SetInputSource(input);
            race.Player.Health.SetInvulnerable(120f);
            void Place(CarController target, float d, float lane, float y, Vector3 up, float speed)
            {
                Vector3 point = path.Position(d) + path.Right(d) * lane + Vector3.up * y;
                target.Teleport(point, Quaternion.LookRotation(path.Tangent(d), up));
                target.Body.linearVelocity = path.Tangent(d) * speed;
                Physics.SyncTransforms();
            }

            input.Set(1f, 0f);
            Place(car, layout.BoostPad - 15f, 0f, 0.5f, Vector3.up, 20f);
            bool boosted = false;
            for (int i = 0; i < 75 && !boosted; i++) { yield return new WaitForFixedUpdate(); boosted = car.IsBoosting; }
            Assert.IsTrue(boosted, "central boost pad");

            Place(car, layout.GapStart - 40f, 0f, 0.5f, Vector3.up, 25f);
            bool landed = false;
            for (int i = 0; i < 250 && !landed; i++)
            {
                yield return new WaitForFixedUpdate();
                landed = race.Player.Progress.Distance > layout.GapEnd + 8f && car.IsGrounded;
            }
            Assert.IsTrue(landed, "jump across the full bridge gap");

            float tunnel = layout.TunnelStart + 2f * WastelandLayout.TunnelFlare + 10f;
            Place(car, tunnel, 0f, WastelandTrack.TubeHeight - 0.5f, Vector3.down, 25f);
            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(car.WallRiding, "ceiling ride");
            Place(car, tunnel, WastelandTrack.TubeFloor + WastelandTrack.TubeFillet - 1f, 5.5f, -path.Right(tunnel), 25f);
            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate();
            Assert.IsTrue(car.WallRiding, "side wall ride");

            var victim = race.Participants[1].Car;
            victim.SetInputSource(victim.gameObject.AddComponent<ScriptedDriverInput>());
            input.Set(0f, 0f);
            Place(victim, layout.StartLine + 60f, 0f, 0.5f, Vector3.up, 4f);
            Place(car, layout.StartLine + 54.5f, 0f, 0.5f, Vector3.up, 18f);
            bool shoved = false;
            for (int i = 0; i < 40 && !shoved; i++) { yield return new WaitForFixedUpdate(); shoved = victim.IsStaggered; }
            Assert.IsTrue(shoved, "ramming pushes a rival harder");

            // Stage 2 (the leader finished lap 2): the edges break off after the warning and a car left there falls.
            map.SetStage(2);
            Assert.AreEqual(WastelandLayout.NarrowHalfWidth, path.halfWidth);
            float collapse = Time.time + WastelandTrack.CollapseDelay + WastelandTrack.CollapseSpread + 0.3f;
            while (Time.time < collapse) yield return null;
            Assert.IsTrue(map.EdgesGone);
            input.Set(0f, 0f);
            Place(car, layout.StartLine + 25f, 8.5f, 0.5f, Vector3.up, 2f);
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();
            Assert.Less(car.transform.position.y, -1f, "a car on a broken edge falls");

            flow.EnterMenu();
            Assert.IsNull(Object.FindAnyObjectByType<WastelandTrack>());
            Assert.IsFalse(path.HasLayout);
            Assert.AreEqual(originalLength, path.Length, 0.01f);
            Assert.AreEqual(1f, CarController.RamPushScale);
        }
    }
}
