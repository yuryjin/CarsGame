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

        /// <summary>Car at a lane and a height above the road (world up), its own up along <paramref name="up"/>.</summary>
        private void Place(CarController car, float d, float lane, float lift, Vector3 up, float speed)
        {
            Vector3 tangent = path.Tangent(d);
            Vector3 position = path.Position(d) + path.Right(d) * lane;
            position.y = layout.Height(d) + lift;
            car.Teleport(position, Quaternion.LookRotation(tangent, up));
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
            Assert.AreEqual(1, map.Stage);
            Assert.IsTrue(map.transform.Find("Rails").gameObject.activeSelf);
            Assert.IsTrue(layout.InTunnel((layout.RuinTunnelStart + layout.RuinTunnelEnd) * 0.5f), "the second tunnel is on the return leg");
            Physics.SyncTransforms();
            float ruinMid = (layout.RuinTunnelStart + layout.RuinTunnelEnd) * 0.5f;
            Assert.IsTrue(Physics.Raycast(path.Position(ruinMid) + Vector3.up * 4f, Vector3.up, out var roof, 12f,
                1 << LayerMask.NameToLayer("Track")));
            Assert.IsNotNull(roof.collider.GetComponent<GravitySurface>(), "the second tunnel roof supports wall riding");
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
            Place(car, ceilingAt, 0f, StarRingTrack.TubeHeight - 0.5f, Vector3.down, 25f);
            yield return Ticks(50);
            Assert.IsTrue(car.WallRiding, $"sticks to the ceiling (y={car.transform.position.y - layout.Height(ceilingAt):F1}, up={car.transform.up}, {car.SpeedKmh:F0} km/h)");
            Assert.Greater(car.transform.position.y, layout.Height(ceilingAt) + StarRingTrack.TubeHeight - 2f, "still up there after a second");
            Assert.Less(car.transform.up.y, -0.8f, "upside down");
            Assert.Greater(race.Player.Progress.Distance, ceilingAt + 15f, "and driving along the tunnel");
            bool ceilingBoost = false;
            for (int i = 0; i < 75 && !ceilingBoost; i++) { yield return new WaitForFixedUpdate(); ceilingBoost = car.IsBoosting; }
            Assert.IsTrue(ceilingBoost, "the ceiling pad rewards the ride");
            Assert.Greater(race.Player.CeilingBoosts, 0, "the skill goal receives the ceiling pad trigger");
            // Slow down and it lets go.
            input.Set(0f, 0f);
            car.Body.linearVelocity = path.Tangent(race.Player.Progress.Distance) * 5f;
            yield return Ticks(90);
            Assert.IsFalse(car.WallRiding, "too slow to stick");
            Assert.Less(car.transform.position.y, layout.Height(ceilingAt) + 3f, "dropped to the floor");

            // 5. Tunnel wall: on its side along the right wall (the wall's normal points left).
            input.Set(1f, 0f);
            Place(car, ceilingAt, StarRingLayout.HalfWidth - 0.8f, 5.5f, -path.Right(ceilingAt), 25f);
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
            Assert.AreEqual(RaceState.Racing, race.State, $"still racing before the turret check (laps {race.Player.Progress.LapsCompleted})");
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
            }
            Assert.IsTrue(laser, "the lock is telegraphed");
            Assert.IsTrue(targeted.Contains(car.CarId) && targeted.Contains(victim.CarId),
                $"both the player ({car.CarId}, can={gunner.CanTarget(car)}, {Vector3.Distance(gunner.MuzzlePosition, car.Body.worldCenterOfMass):F0} m) " +
                $"and the AI ({victim.CarId}, can={gunner.CanTarget(victim)}, {Vector3.Distance(gunner.MuzzlePosition, victim.Body.worldCenterOfMass):F0} m) are targets; " +
                $"seen [{string.Join(",", targeted)}], shots {gunner.ShotsFired}, now {(gunner.Target != null ? gunner.Target.CarId : 0)}; " +
                $"race {race.State} laps {race.Player.Progress.LapsCompleted} input {car.inputEnabled}/{victim.inputEnabled} enabled {car.isActiveAndEnabled}/{victim.isActiveAndEnabled}");
            Assert.GreaterOrEqual(gunner.ShotsFired, SpaceTurret.Burst);

            // A shot can destroy a turret once, crediting the shooter and the race reward.
            Assert.IsTrue(gunner.GetComponentsInChildren<Collider>().Length > 0, "turret has a projectile target");
            float approach = turretAt - 25f;
            Vector3 muzzleOnRoad = path.Position(approach) - path.Right(approach) * 6f + Vector3.up * 1.5f;
            Vector3 sight = gunner.AimPoint - muzzleOnRoad;
            float sightAngle = Vector3.Angle(path.Tangent(approach), sight);
            bool sightBlocked = Physics.Linecast(muzzleOnRoad, gunner.AimPoint, TargetFinder.ObstacleMask);
            Assert.AreEqual(gunner, TargetFinder.FindTurret(muzzleOnRoad, path.Tangent(approach), 30f, 4f, 45f),
                $"the car can acquire the turret from the road (distance {sight.magnitude:F1}, angle {sightAngle:F1}, blocked {sightBlocked}, active {SpaceTurret.All.Count})");
            var shell = RaceDirector.Current.RequestProjectile(new ProjectileSpawn
            {
                Owner = car.gameObject,
                OwnerId = car.CarId,
                Position = gunner.AimPoint - gunner.transform.forward * 4f,
                Velocity = gunner.transform.forward * 70f,
                Damage = SpaceTurret.MaxHealth,
                Tick = SimulationDriver.Instance.Tick,
                Kind = ProjectileKind.Shell,
                Weapon = PrimaryWeaponType.Cannon
            });
            Assert.IsNotNull(shell);
            for (int i = 0; i < 15 && gunner.IsAlive; i++) yield return new WaitForFixedUpdate();
            Assert.IsFalse(gunner.IsAlive, "a real shell can destroy the turret");
            Assert.AreEqual(SpaceTurret.DestroyPoints, race.Player.TurretPoints);
            Assert.IsFalse(gunner.TakeHit(car.gameObject, SpaceTurret.MaxHealth), "wreck gives no duplicate points");
            Assert.AreEqual(SpaceTurret.DestroyPoints, race.Player.TurretPoints);

            // Any racer can remove the rails, then the shoulder collider, for the entire field.
            var rivalProgress = rivals[0].GetComponent<RaceProgress>();
            float distance = path.startLineDistance + 1f;
            rivalProgress.transform.position = path.Position(distance);
            rivalProgress.BeginRace();
            int steps = Mathf.CeilToInt(path.Length / 5f);
            for (int lap = 1; lap <= 2; lap++)
            {
                for (int i = 0; i < steps; i++)
                {
                    distance += path.Length / steps;
                    rivalProgress.transform.position = path.Position(distance);
                    rivalProgress.SimulationStep(lap * steps + i);
                }
                Assert.AreEqual(lap + 1, map.Stage);
                var hud = Object.FindAnyObjectByType<HudScreen>();
                Assert.AreEqual(Loc.Get(lap == 1 ? "hud.wasteland.rails_off" : "hud.wasteland.narrowing"), hud.LastHint,
                    "a rival triggers a visible warning even before the player's next lap");
                Assert.IsTrue(map.transform.Find("Shoulders").gameObject.activeSelf, "warning precedes detachment");
                float wait = lap == 1 ? StarRingTrack.RailSinkSeconds : StarRingTrack.CollapseDelay + StarRingTrack.CollapseSeconds;
                float end = Time.time + wait + 0.1f;
                while (Time.time < end) yield return null;
                Assert.IsFalse(map.transform.Find("Rails").gameObject.activeSelf);
                Assert.AreEqual(lap == 1, map.transform.Find("Shoulders").gameObject.activeSelf);
                Assert.AreEqual(lap == 1, map.transform.Find("Shoulders/Warnings").gameObject.activeSelf);
            }
            Assert.AreEqual(StarRingTrack.NarrowHalfWidth, path.halfWidth);
            Physics.SyncTransforms();
            Assert.IsFalse(Physics.Raycast(path.Position(110f) + path.Right(110f) * 8f + Vector3.up * 3f,
                Vector3.down, 8f, 1 << LayerMask.NameToLayer("Track")), "the outer road has no collider");

            flow.RetryRace();
            Assert.AreEqual(1, Object.FindObjectsByType<StarRingTrack>(FindObjectsInactive.Exclude).Length, "retry builds one ring");
            Assert.AreEqual(1, Object.FindAnyObjectByType<StarRingTrack>().Stage, "retry restores full width and rails");
            Assert.AreEqual(0, race.Player.TurretPoints, "retry clears turret points");
            flow.EnterMenu();
            Assert.IsNull(Object.FindAnyObjectByType<StarRingTrack>());
            Assert.IsTrue(ground.activeSelf);
            Assert.IsFalse(path.HasLayout);
            Assert.AreEqual(authoredLength, path.Length, 0.01f);
            Assert.AreEqual(1f, CarController.RamPushScale);
            yield return null;
            Assert.AreEqual(0, GravitySurface.ActiveCount);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CollapseWarningPausesAndFallenCarRecoversOnTheNarrowDeck()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float deadline = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            var flow = Object.FindAnyObjectByType<GameFlow>();
            var race = Object.FindAnyObjectByType<RaceManager>();
            try
            {
                flow.StartRace(Resources.Load<RaceEventDefinition>("Events/Event_e19"), null);
                var map = Object.FindAnyObjectByType<StarRingTrack>();
                path = TrackPath.Instance;
                layout = map.Layout;
                while (race.State == RaceState.Countdown) yield return null;
                foreach (var turret in map.Turrets) turret.enabled = false;
                foreach (var participant in race.Participants)
                {
                    participant.Car.SetInputSource(participant.gameObject.AddComponent<ScriptedDriverInput>());
                    Place(participant.Car, layout.StartLine - 40f - participant.Car.CarId * 8f, 0f, 0.5f, Vector3.up, 0f);
                }
                var car = race.Player.Car;
                float d = layout.StartLine + 25f;
                Place(car, d, 7.5f, 0.5f, Vector3.up, 0f);
                yield return Ticks(20);
                Capture("1-rails", car);
                map.SetStage(2, true);
                yield return new WaitForSeconds(StarRingTrack.RailSinkSeconds + 0.1f);
                Capture("2-open", car);
                map.SetStage(3, true);
                yield return new WaitForSeconds(0.4f);
                Capture("3-warning", car);
                PauseController.Set(RacingGame.Core.Platform.PauseReason.User, true);
                yield return new WaitForSecondsRealtime(StarRingTrack.CollapseDelay + 0.2f);
                Assert.IsFalse(map.EdgesGone, "a pause cannot consume the player's escape window");
                Assert.Greater(car.transform.position.y, layout.Height(d) - 0.2f);
                PauseController.Set(RacingGame.Core.Platform.PauseReason.User, false);
                float end = Time.time + StarRingTrack.CollapseDelay - 0.8f;
                while (Time.time < end) yield return null;
                Assert.IsFalse(map.EdgesGone, "the remaining warning resumes after unpause");
                end = Time.time + 2f;
                bool fell = false;
                while (Time.time < end)
                {
                    fell |= car.transform.position.y < layout.Height(d) - 2f;
                    yield return null;
                }
                Assert.IsTrue(map.EdgesGone);
                Assert.IsTrue(fell, "a stationary car left on the detached panel falls");
                end = Time.time + 8f;
                while (!car.IsGrounded && Time.time < end) yield return null;
                Assert.IsTrue(car.IsGrounded, "fall recovery returns to the surviving road");
                Assert.LessOrEqual(Mathf.Abs(path.Spline.LateralOffset(car.transform.position, race.Player.Progress.Distance)),
                    StarRingTrack.NarrowHalfWidth - 1f, "recovery leaves room at the new edge");
                Capture("4-narrow", car);
                flow.RetryRace();
                map = Object.FindAnyObjectByType<StarRingTrack>();
                Assert.AreEqual(1, map.Stage);
                Assert.IsFalse(map.EdgesGone);
                Assert.AreEqual(StarRingLayout.HalfWidth, path.halfWidth);
                Assert.IsTrue(map.transform.Find("Rails").gameObject.activeSelf);
                Assert.IsTrue(map.transform.Find("Shoulders").gameObject.activeSelf);
            }
            finally { PauseController.ResetAll(); flow.EnterMenu(); }
        }

        // Optional artifacts from the real GPU test; no screenshots are taken in ordinary test runs.
        private void Capture(string name, CarController car)
        {
            string directory = System.Environment.GetEnvironmentVariable("CG_STAR_SHOTS");
            if (string.IsNullOrEmpty(directory)) return;
            System.IO.Directory.CreateDirectory(directory);
            var camera = Camera.main;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var target = new RenderTexture(960, 540, 24);
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            try
            {
                float d = layout.StartLine + 25f;
                Vector3 centre = path.Position(d), forward = path.Tangent(d);
                camera.transform.position = centre - forward * 12f + Vector3.up * 7f;
                camera.transform.LookAt(centre + forward * 15f);
                camera.targetTexture = target;
                camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), image.EncodeToPNG());
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "renderer.txt"), SystemInfo.graphicsDeviceName);
            }
            finally
            {
                camera.targetTexture = previousTarget; RenderTexture.active = previous;
                camera.transform.SetPositionAndRotation(position, rotation);
                target.Release(); Object.Destroy(target); Object.Destroy(image);
            }
        }
    }
}
