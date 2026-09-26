using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using RacingGame.Core.Track;

namespace RacingGame.Tests
{
    public sealed class StarRingPlayTests
    {
        private TrackPath path;
        private StarRingLayout layout;

        private void Place(CarController car, float d, float lane, float height, Vector3 up, float speed, float verticalOffset = 0f)
        {
            Vector3 tangent = path.Tangent(d);
            Vector3 position = path.Position(d) + path.Right(d) * lane;
            position.y = layout.Height(d);
            car.Teleport(position + up * height + Vector3.up * verticalOffset, Quaternion.LookRotation(tangent, up));
            car.Body.linearVelocity = tangent * speed;
            Physics.SyncTransforms();
        }

        private static IEnumerator Ticks(int count) { for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate(); }

        [UnityTest, Timeout(240000)]
        public IEnumerator JumpWallRideRamAndTurrets()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null;
            yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            var ground = GameObject.Find("Ground_Terrain");
            path = TrackPath.Instance;
            float authoredLength = path.Length;
            var ev = Resources.Load<RaceEventDefinition>("Events/Event_e19");
            Assert.IsNotNull(ev);
            flow.StartRace(ev, null);
            var map = Object.FindAnyObjectByType<StarRingTrack>();
            Assert.IsNotNull(map);
            layout = map.Layout;
            Assert.AreEqual(RaceState.Countdown, race.State);
            Assert.IsFalse(ground.activeSelf);
            Assert.AreEqual(layout.Length, path.Length, 0.5f, "the field races the generated loop");
            Assert.AreEqual(ev.ramPush, CarController.RamPushScale);
            while (race.State == RaceState.Countdown) yield return null;

            // Park the rivals and silence the turrets while the driving physics is measured.
            var car = race.Player.Car;
            var input = car.gameObject.AddComponent<ScriptedDriverInput>();
            car.SetInputSource(input);
            var rivals = new List<CarController>();
            foreach (var participant in race.Participants)
            {
                if (participant.isPlayer) continue;
                participant.Car.SetInputSource(participant.gameObject.AddComponent<ScriptedDriverInput>());
                Place(participant.Car, layout.StartLine - 30f - rivals.Count * 8f, 0f, 0.5f, Vector3.up, 0f);
                rivals.Add(participant.Car);
            }
            foreach (var turret in map.Turrets) turret.enabled = false;
            race.Player.Health.SetInvulnerable(120f);

            // 1. The pad in the middle of the road boosts.
            input.Set(1f, 0f);
            Place(car, layout.BoostPad - 15f, 0f, 0.5f, Vector3.up, 20f);
            bool boosted = false;
            for (int i = 0; i < 75 && !boosted; i++) { yield return new WaitForFixedUpdate(); boosted = car.IsBoosting; }
            Assert.IsTrue(boosted, "centre pad gives nitro");

            // 2. At a racing pace the bridge gap is cleared and the car lands on the lower deck.
            Place(car, layout.GapStart - 40f, 0f, 0.5f, Vector3.up, 25f);
            bool cleared = false;
            for (int i = 0; i < 250 && !cleared; i++)
            {
                yield return new WaitForFixedUpdate();
                cleared = race.Player.Progress.Distance > layout.GapEnd + 8f && car.IsGrounded;
            }
            Assert.IsTrue(cleared, "90 km/h jumps the gap");
            Assert.Greater(car.transform.position.y, StarRingLayout.LandingHeight - 1f, "on the landing deck, not under it");

            // 3. Too slow: the car drops into the gap and comes back with a run-up, never over the gap.
            input.Set(0f, 0f);
            Place(car, layout.GapStart - 6f, 0f, 0.5f, Vector3.up, 9f);
            bool fell = false;
            for (int i = 0; i < 200 && !fell; i++) { yield return new WaitForFixedUpdate(); fell = car.transform.position.y < StarRingLayout.LandingHeight - 3f; }
            Assert.IsTrue(fell, "32 km/h falls short");
            bool back = false;
            for (int i = 0; i < 250 && !back; i++) { yield return new WaitForFixedUpdate(); back = car.transform.position.y > 4f && car.IsGrounded; }
            Assert.IsTrue(back, "fall recovery");
            float respawn = race.Player.Progress.Distance;
            Assert.LessOrEqual(respawn, layout.GapStart - StarRingLayout.JumpRunUp + 1f, "respawn leaves a run-up");
            Assert.Greater(respawn, layout.GapStart - StarRingLayout.JumpRunUp - 40f);

            // 4. Tunnel ceiling: upside down at speed the car sticks, drives and finds the ceiling pad.
            float ceilingAt = layout.TunnelStart + 2f * StarRingLayout.TunnelFlare + 10f;
            input.Set(1f, 0f);
            yield return Ticks(150);   // let the earlier boost run out
            Place(car, ceilingAt, 0f, -(StarRingTrack.TubeHeight - 0.5f), Vector3.down, 25f);
            yield return Ticks(50);
            Assert.IsTrue(car.WallRiding, $"sticks to the ceiling (y={car.transform.position.y - layout.Height(ceilingAt):F1}, up={car.transform.up}, {car.SpeedKmh:F0} km/h)");
            Assert.Greater(car.transform.position.y, layout.Height(ceilingAt) + StarRingTrack.TubeHeight - 2f, "still up there after a second");
            Assert.Less(car.transform.up.y, -0.8f, "upside down");
            Assert.Greater(race.Player.Progress.Distance, ceilingAt + 15f, "and driving along the tunnel");
            bool ceilingBoost = false;
            for (int i = 0; i < 75 && !ceilingBoost; i++) { yield return new WaitForFixedUpdate(); ceilingBoost = car.IsBoosting; }
            Assert.IsTrue(ceilingBoost, "the ceiling pad rewards the ride");
            // Slow down and it lets go.
            input.Set(0f, 0f);
            car.Body.linearVelocity = path.Tangent(race.Player.Progress.Distance) * 5f;
            yield return Ticks(90);
            Assert.IsFalse(car.WallRiding, "too slow to stick");
            Assert.Less(car.transform.position.y, layout.Height(ceilingAt) + 3f, "dropped to the floor");

            // 5. Tunnel wall: on its side along the right wall.
            input.Set(1f, 0f);
            Place(car, ceilingAt, StarRingLayout.HalfWidth - 1.2f, -0.2f, -path.Right(ceilingAt), 25f, 5.5f);
            yield return Ticks(50);
            Assert.IsTrue(car.WallRiding, $"sticks to the wall (up={car.transform.up}, {car.SpeedKmh:F0} km/h)");
            Assert.Greater(path.Spline.LateralOffset(car.transform.position, race.Player.Progress.Distance), 6.5f, "still on the wall");

            // 6. Ramming shoves the car ahead and makes it lose grip for a moment; plain physics does not.
            input.Set(0f, 0f);
            var victim = rivals[0];
            foreach (float scale in new[] { ev.ramPush, 1f })
            {
                CarController.RamPushScale = scale;
                float d = layout.StartLine + 60f;
                Place(victim, d, 0f, 0.5f, Vector3.up, 4f);
                Place(car, d - 5.5f, 0f, 0.5f, Vector3.up, 18f);
                bool staggered = false;
                for (int i = 0; i < 40 && !staggered; i++) { yield return new WaitForFixedUpdate(); staggered = victim.IsStaggered; }
                Assert.AreEqual(scale > 1f, staggered, $"ram push at ×{scale}");
                Assert.IsFalse(car.IsStaggered, "the rammer keeps its grip");
            }
            CarController.RamPushScale = ev.ramPush;

            // 7. Turrets shoot everyone in sight, player and AI alike.
            var gunner = map.Turrets[0];
            int hint = -1;
            float turretAt = path.Project(gunner.transform.position, ref hint);
            Place(car, turretAt, -3f, 0.5f, Vector3.up, 0f);
            Place(victim, turretAt, 3f, 0.5f, Vector3.up, 0f);
            victim.GetComponent<CarHealth>().SetInvulnerable(120f);
            foreach (var turret in map.Turrets) turret.enabled = true;
            var targeted = new HashSet<int>();
            bool laser = false;
            for (int i = 0; i < 400 && targeted.Count < 2; i++)
            {
                yield return new WaitForFixedUpdate();
                if (gunner.Target != null) targeted.Add(gunner.Target.CarId);
                laser |= gunner.Aiming;
                if (i % 100 == 0)
                {
                    bool playerBlocked = Physics.Linecast(gunner.MuzzlePosition, car.Body.worldCenterOfMass, 1 << LayerMask.NameToLayer("Track"));
                    bool victimBlocked = Physics.Linecast(gunner.MuzzlePosition, victim.Body.worldCenterOfMass, 1 << LayerMask.NameToLayer("Track"));
                    Debug.Log($"TURRET_PROBE {i} target={(gunner.Target != null ? gunner.Target.CarId : -1)} seen={string.Join(",", targeted)} shots={gunner.ShotsFired} p={car.CarId}/{car.inputEnabled}/{car.GetComponent<CarHealth>().IsDead}/{Vector3.Distance(gunner.MuzzlePosition,car.transform.position):F1}/{playerBlocked} v={victim.CarId}/{victim.inputEnabled}/{victim.GetComponent<CarHealth>().IsDead}/{Vector3.Distance(gunner.MuzzlePosition,victim.transform.position):F1}/{victimBlocked}");
                }
            }
            Assert.IsTrue(laser, "the lock is telegraphed");
            Assert.IsTrue(targeted.Contains(car.CarId) && targeted.Contains(victim.CarId), "both the player and the AI are targets");
            Assert.GreaterOrEqual(gunner.ShotsFired, SpaceTurret.Burst);

            flow.RetryRace();
            Assert.AreEqual(1, Object.FindObjectsByType<StarRingTrack>(FindObjectsInactive.Exclude).Length, "retry builds one ring");
            flow.EnterMenu();
            Assert.IsNull(Object.FindAnyObjectByType<StarRingTrack>());
            Assert.IsTrue(ground.activeSelf);
            Assert.IsFalse(path.HasLayout);
            Assert.AreEqual(authoredLength, path.Length, 0.01f);
            Assert.AreEqual(1f, CarController.RamPushScale);
            yield return null;
            Assert.AreEqual(0, GravitySurface.ActiveCount);
        }
    }
}
