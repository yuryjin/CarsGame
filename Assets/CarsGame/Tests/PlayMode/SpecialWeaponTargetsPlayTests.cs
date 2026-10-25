using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RacingGame.Tests
{
    public sealed class SpecialWeaponTargetsPlayTests
    {
        private float capture;
        private GameFlow flow;
        private RaceManager race;
        private RaceEventDefinition practice;
        private GameObject targets;
        private CarController car;
        private readonly Vector3 origin = new Vector3(10000f, 30f, 10000f);

        [SetUp] public void Setup() { capture = Time.captureDeltaTime; Time.captureDeltaTime = SimulationDriver.FixedStep; }
        [TearDown] public void Cleanup()
        {
            if (targets != null) Object.DestroyImmediate(targets);
            if (flow != null) flow.EnterMenu();
            if (practice != null) Object.DestroyImmediate(practice);
            Time.captureDeltaTime = capture;
            PauseController.ResetAll();
        }

        private IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            float until = Time.realtimeSinceStartup + 60f;
            while (!PlatformGlue.IsReady && Time.realtimeSinceStartup < until) yield return null;
            Assert.IsTrue(PlatformGlue.IsReady);
            yield return null; yield return null;
            flow = Object.FindAnyObjectByType<GameFlow>(); race = Object.FindAnyObjectByType<RaceManager>();
            practice = Object.Instantiate(System.Array.Find(flow.events, e => e.eventId == "e1"));
            practice.aiCountOverride = 0; practice.pickupCount = 0; practice.mineFieldCount = 0;
            flow.StartRace(practice, flow.tiers[0]);
            while (race.State == RaceState.Countdown) yield return null;
            car = race.Player.Car;
            car.inputEnabled = false;
            car.Teleport(origin, Quaternion.identity);
            car.Body.constraints = RigidbodyConstraints.FreezeAll;
            targets = new GameObject("ControlledWeaponTargets");
            Physics.SyncTransforms();
        }

        private SpaceTurret Target(Vector3 position)
        {
            var material = Resources.Load<Material>("FX_Flash");
            return SpaceTurret.Create(targets.transform, position, Vector3.back, 0, race, material, material, material);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DirectSpecialWeaponsCanDestroyReachableTrackTurrets()
        {
            yield return Boot();
            var failures = new List<string>();
            for (int index = 0; index < PrimaryWeaponCatalog.Count; index++)
            {
                var type = (PrimaryWeaponType)index;
                var shot = PrimaryWeaponCatalog.Get(type);
                if (shot.Mode == WeaponMode.Projectile || shot.Mode == WeaponMode.Trap) continue;
                var turret = Target(origin + Vector3.forward * 4f);
                Physics.SyncTransforms();
                Assert.AreSame(turret, TargetFinder.FindTurret(origin + Vector3.up, Vector3.forward, shot.Arc, 0.1f, shot.Range));
                var attack = new SpecialWeaponAttack(car.gameObject);
                attack.Configure(type);
                int points = race.Player.TurretPoints;
                // Exercise target acquisition and each attack's damage path with its real profile.
                int shots = Mathf.CeilToInt(SpaceTurret.MaxHealth / shot.Damage);
                for (int n = 0; n < shots; n++)
                {
                    int tick = 10000 + n * 100;
                    attack.Fire(shot, shot.Damage, tick);
                    attack.Step(tick + 20);
                }
                if (turret.IsAlive || race.Player.TurretPoints != points + SpaceTurret.DestroyPoints) failures.Add(type.ToString());
                attack.Cancel(); attack.Configure(PrimaryWeaponType.Cannon);
                Object.DestroyImmediate(turret.gameObject);
            }
            Assert.IsEmpty(failures, "Reachable track turrets ignored by: " + string.Join(", ", failures));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator BeamReticleUsesWeaponReachAndAcquiresTrackTurrets()
        {
            yield return Boot();
            Target(origin + Vector3.forward * 4f);
            Physics.SyncTransforms();
            car.Weapons.SetPrimaryType(PrimaryWeaponType.Welder);
            var camera = Camera.main;
            camera.GetComponent<CarCameraFollow>().enabled = false;
            camera.transform.SetPositionAndRotation(origin + new Vector3(0f, 4f, -12f), Quaternion.LookRotation(new Vector3(0f, -3f, 18f)));
            for (int i = 0; i < 8; i++) yield return null;
            var reticle = GameObject.Find("Reticle")?.GetComponent<RectTransform>();
            Assert.NotNull(reticle, "A beam has a visible aiming marker.");
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, reticle.position);
            Assert.IsTrue(new Rect(0f, 0f, Screen.width, Screen.height).Contains(screen), "A short-range beam must not use projectile gravity/drop.");
            Assert.IsTrue(race.GetComponent<HudScreen>().ReticleLocked, "A reachable turret is an attackable target.");
            foreach (var type in new[] { PrimaryWeaponType.TeslaCoil, PrimaryWeaponType.MineDispenser })
            {
                car.Weapons.SetPrimaryType(type);
                yield return null; yield return null;
                Assert.IsFalse(reticle.gameObject.activeSelf, type + " must not display a forward projectile crosshair.");
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SpecialTurretHitsRespectReachCoverAndMeleeCancellation()
        {
            yield return Boot();
            foreach (var type in new[] { PrimaryWeaponType.Welder, PrimaryWeaponType.Sledgehammer })
            {
                var shot = PrimaryWeaponCatalog.Get(type);
                var turret = Target(origin + Vector3.forward * (shot.Range + 2f));
                var attack = new SpecialWeaponAttack(car.gameObject);
                attack.Configure(type);
                void Fire(int tick) { attack.Fire(shot, SpaceTurret.MaxHealth, tick); attack.Step(tick + 20); }
                Physics.SyncTransforms(); Fire(10000);
                Assert.IsTrue(turret.IsAlive, type + " cannot hit beyond its reach");
                turret.transform.position = origin + Vector3.back * 4f;
                Physics.SyncTransforms(); Fire(10100);
                Assert.IsTrue(turret.IsAlive, type + " cannot hit outside its arc");
                turret.transform.position = origin + Vector3.forward * 4f;
                var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cover.transform.SetParent(targets.transform);
                cover.layer = LayerMask.NameToLayer("Track");
                cover.transform.position = origin + new Vector3(0f, 1f, 2f);
                cover.transform.localScale = new Vector3(5f, 5f, 0.5f);
                Physics.SyncTransforms(); Fire(10200);
                Assert.IsTrue(turret.IsAlive, type + " cannot hit through cover");
                Object.DestroyImmediate(cover); Physics.SyncTransforms();
                if (shot.Mode == WeaponMode.Melee)
                {
                    attack.Fire(shot, SpaceTurret.MaxHealth, 10300);
                    attack.Cancel(); attack.Step(10320);
                    Assert.IsTrue(turret.IsAlive, "Cancelled melee swing cannot land later");
                }
                Fire(10400);
                Assert.IsFalse(turret.IsAlive, type + " can hit once the target is exposed");
                int points = race.Player.TurretPoints;
                Fire(10500);
                Assert.AreEqual(points, race.Player.TurretPoints, "A wreck cannot award duplicate points");
                attack.Configure(PrimaryWeaponType.Cannon);
                Object.DestroyImmediate(turret.gameObject);
            }

            var first = Target(origin + Vector3.forward * 4f);
            var second = Target(origin + Vector3.back * 4f);
            Physics.SyncTransforms();
            var pulse = new SpecialWeaponAttack(car.gameObject);
            pulse.Configure(PrimaryWeaponType.TeslaCoil);
            int before = race.Player.TurretPoints;
            pulse.Fire(PrimaryWeaponCatalog.Get(PrimaryWeaponType.TeslaCoil), SpaceTurret.MaxHealth, 11000);
            Assert.IsFalse(first.IsAlive); Assert.IsFalse(second.IsAlive);
            Assert.AreEqual(before + 2 * SpaceTurret.DestroyPoints, race.Player.TurretPoints, "Removing a turret must not skip the next pulse target");
            pulse.Configure(PrimaryWeaponType.Cannon);
        }
    }
}
