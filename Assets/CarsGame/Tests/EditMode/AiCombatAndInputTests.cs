using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;
using RacingGame.Core.Combat;
using RacingGame.Core.Track;

namespace RacingGame.Tests
{
    public class AiCombatAndInputTests
    {
        private GameObject directorGo;
        private RaceDirector director;
        private GameObject carGo;
        private CarController car;
        private CarStatus status;
        private CarWeapons weapons;
        private CarCannon cannon;
        private AiGunner gunner;
        private GameObject rivalGo;
        private CarController rival;
        private GameObject trackGo;

        [SetUp]
        public void SetUp()
        {
            director = SimulationDriver.Instance.GetComponent<RaceDirector>();
            directorGo = director.gameObject;
            director.raceSeed = 12345;

            carGo = new GameObject("AiCar");
            _ = carGo.AddComponent<Rigidbody>();
            car = carGo.AddComponent<CarController>();
            status = carGo.AddComponent<CarStatus>();
            weapons = carGo.AddComponent<CarWeapons>();
            cannon = carGo.AddComponent<CarCannon>();
            gunner = carGo.AddComponent<AiGunner>();

            rivalGo = new GameObject("RivalCar");
            _ = rivalGo.AddComponent<Rigidbody>();
            rival = rivalGo.AddComponent<CarController>();
            rivalGo.AddComponent<CarStatus>();
            rivalGo.AddComponent<CarHealth>();

            director.RegisterCar(car);
            director.RegisterCar(rival);
        }

        [TearDown]
        public void TearDown()
        {
            if (carGo != null) Object.DestroyImmediate(carGo);
            if (rivalGo != null) Object.DestroyImmediate(rivalGo);
            if (trackGo != null) Object.DestroyImmediate(trackGo);
            if (directorGo != null) Object.DestroyImmediate(directorGo);
        }

        [Test]
        public void PlayerDriverInput_WheelAndScroll_UsePrimaryCommandAndCancelOnPause()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            var input = carGo.AddComponent<PlayerDriverInput>();
            weapons.machineGunUnlocked = true;
            typeof(PlayerDriverInput).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                Assert.IsTrue(input.WeaponWheelOpen);
                Assert.AreEqual(0, input.Sample(1).SelectPrimary, "Opening the wheel must not select anything.");

                InputSystem.QueueStateEvent(mouse, new MouseState { delta = WeaponWheelGraphic.SlotDirection(2) * 100f, buttons = 1 });
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                Assert.AreEqual(2, input.WheelSelection);
                Assert.IsFalse(input.Sample(2).Fire, "Pointing at the wheel must not fire.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                Assert.AreEqual(2, input.Sample(3).SelectPrimary, "Releasing Q confirms the highlighted weapon.");
                Assert.AreEqual(0, input.Sample(4).SelectPrimary, "Selection is consumed once.");

                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, -120f) });
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                Assert.AreEqual(50, input.Sample(5).SelectPrimary, "Scrolling backward wraps to the final arsenal page.");
                weapons.machineGunUnlocked = false;
                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, 120f) });
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                Assert.AreEqual(3, input.Sample(6).SelectPrimary, "Scrolling skips unavailable weapons.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, 120f) });
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                Assert.AreEqual(1, input.SelectionPage);
                Assert.AreEqual(0, input.WheelSelection, "Changing page never confirms an unseen weapon.");
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = WeaponWheelGraphic.SlotDirection(2) * 100f });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                Assert.AreEqual(12, input.WheelSelection);
                PauseController.Set(RacingGame.Core.Platform.PauseReason.User, true);
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                Assert.IsFalse(input.WeaponWheelOpen);
                Assert.AreEqual(0, input.Sample(9).SelectPrimary);
            }
            finally
            {
                PauseController.ResetAll();
                Object.DestroyImmediate(input);
                InputSystem.RemoveDevice(mouse);
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void PlayerDriverInput_ManualAim_PointsCannonAtCursor()
        {
            AimMouseAtTarget(autoAim: false, (input, cannon) =>
            {
                input.Sample(1);
                Assert.IsTrue(cannon.HasAimCommit);
                Assert.That(cannon.AimCommitPoint.x, Is.InRange(4f, 6f));
                Assert.AreEqual(input.manualAimYawLimit, cannon.CurrentYawLimit, 0.01f, "mouse aim widens the turret sector");
            });
        }

        [Test]
        public void PlayerDriverInput_AutoAim_IgnoresMouseAndKeepsBodyArc()
        {
            AimMouseAtTarget(autoAim: true, (input, cannon) =>
            {
                input.Sample(1);
                Assert.IsFalse(cannon.HasAimCommit);
                Assert.AreEqual(cannon.turretYawLimit, cannon.CurrentYawLimit, 0.01f);
            });
        }

        private void AimMouseAtTarget(bool autoAim, System.Action<PlayerDriverInput, CarCannon> check)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            // Camera.main must be the test camera, not a scene one left by other fixtures.
            var otherCameras = System.Array.FindAll(Camera.allCameras, c => c.CompareTag("MainCamera"));
            foreach (var c in otherCameras) c.enabled = false;
            var cameraGo = new GameObject("AimCamera", typeof(Camera));
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var input = carGo.AddComponent<PlayerDriverInput>();
            input.autoAim = autoAim;
            typeof(PlayerDriverInput).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
            try
            {
                car.transform.position = new Vector3(0f, 200f, 0f);
                target.transform.position = new Vector3(5f, 200.5f, 20f);
                target.transform.localScale = Vector3.one * 2f;
                var camera = cameraGo.GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.pixelRect = new Rect(0f, 0f, 800f, 600f);
                camera.transform.position = new Vector3(0f, 204f, -8f);
                camera.transform.LookAt(new Vector3(0f, 200f, 20f));
                Physics.SyncTransforms();
                Vector3 screen = camera.WorldToScreenPoint(target.transform.position);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(screen.x, screen.y), delta = Vector2.one * 4f, buttons = 1 });
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                check(input, cannon);
            }
            finally
            {
                cannon.ClearAimCommit();
                foreach (var c in otherCameras) if (c != null) c.enabled = true;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraGo);
                InputSystem.RemoveDevice(mouse);
            }
        }

        [Test]
        public void AiGunner_WantsFire_BlockedWhenJammedOrOverheated()
        {
            // Position rival in front of car within cone
            car.transform.position = Vector3.zero;
            car.transform.forward = Vector3.forward;
            rival.transform.position = new Vector3(0f, 0f, 15f);

            gunner.startGraceSeconds = 0f;
            gunner.fireCooldown = 0.5f;

            // When jammed, WantsFire must return false
            status.ApplyStatus(StatusKind.Jammed, 50);
            Assert.IsTrue(status.IsJammed);
            Assert.IsFalse(gunner.WantsFire(100));

            // Clear debuff
            status.ClearDebuffs();
            Assert.IsFalse(status.IsJammed);

            // Overheat weapons
            weapons.AddHeat(100f);
            Assert.IsTrue(weapons.IsOverheated);
            Assert.IsFalse(gunner.WantsFire(100));
        }

        [Test]
        public void AiGunner_WantsFire_MachineGunGeneratesBurst()
        {
            car.transform.position = Vector3.zero;
            car.transform.forward = Vector3.forward;
            rival.transform.position = new Vector3(0f, 0f, 15f);

            weapons.SetPrimaryType(PrimaryWeaponType.MachineGun);
            gunner.startGraceSeconds = 0f;
            gunner.fireCooldown = 1f;

            // Tick 100: acquires target and starts burst
            bool fireFirst = gunner.WantsFire(100);
            Assert.IsTrue(fireFirst);

            // Subsequent ticks (even non-multiples of 10) fire burst ticks
            bool fireSecond = gunner.WantsFire(101);
            Assert.IsTrue(fireSecond);
        }

        [Test]
        public void AiGunner_WantsSecondary_DeploysWhenRivalIsPursuingBehind()
        {
            car.transform.position = Vector3.zero;
            car.transform.forward = Vector3.forward;

            weapons.secondaryType = SecondaryWeaponType.Mines;
            weapons.secondaryAmmo = 3;
            weapons.secondaryCooldown = 2f;
            gunner.startGraceSeconds = 0f;

            // Rival placed 18m directly behind
            rival.transform.position = new Vector3(0f, 0f, -18f);

            bool wantsSecondary = gunner.WantsSecondary(100);
            Assert.IsTrue(wantsSecondary);
            Assert.AreEqual(1, gunner.SecondaryRequested);

            // Rival placed too far (35m behind)
            rival.transform.position = new Vector3(0f, 0f, -35f);
            bool wantsFar = gunner.WantsSecondary(110);
            Assert.IsFalse(wantsFar);

            // Rival placed in front (ahead)
            rival.transform.position = new Vector3(0f, 0f, 18f);
            bool wantsAhead = gunner.WantsSecondary(120);
            Assert.IsFalse(wantsAhead);
        }

        [Test]
        public void PlayerDriverInput_AutoSecondary_TriggersWhenPursued()
        {
            var input = carGo.AddComponent<PlayerDriverInput>();
            input.autoSecondary = true;
            weapons.secondaryType = SecondaryWeaponType.Mines;
            weapons.secondaryAmmo = 3;

            // Rival 20m behind
            rival.transform.position = new Vector3(0f, 0f, -20f);

            var cmd = input.Sample(100);
            Assert.IsTrue(cmd.Secondary);

            // Disable auto secondary
            input.autoSecondary = false;
            var cmdDisabled = input.Sample(110);
            Assert.IsFalse(cmdDisabled.Secondary);
        }

        [Test]
        public void TargetFinder_FindPursuer_FiltersByDistanceAndLateralOffset()
        {
            car.transform.position = Vector3.zero;
            car.transform.forward = Vector3.forward;

            // Rival 20m behind, 1m lateral -> matches
            rival.transform.position = new Vector3(1f, 0f, -20f);
            var target = TargetFinder.FindPursuer(car, car.transform.position, -car.transform.forward, 15f, 25f, 3f);
            Assert.AreSame(rival, target);

            // Rival 20m behind, 6m lateral (outside 3m cone) -> null
            rival.transform.position = new Vector3(6f, 0f, -20f);
            var targetWide = TargetFinder.FindPursuer(car, car.transform.position, -car.transform.forward, 15f, 25f, 3f);
            Assert.IsNull(targetWide);
        }

        [Test]
        public void AiDriver_AvoidMines_AdjustsTargetLane()
        {
            var trackGo = new GameObject("TrackPath_Test");
            var tp = trackGo.AddComponent<TrackPath>();
            var points = new Vector3[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(50f, 0f, 0f),
                new Vector3(100f, 0f, 0f),
                new Vector3(100f, 0f, 50f),
                new Vector3(100f, 0f, 100f),
                new Vector3(50f, 0f, 100f),
                new Vector3(0f, 0f, 100f),
                new Vector3(0f, 0f, 50f)
            };
            for (int i = 0; i < points.Length; i++)
            {
                var pt = new GameObject("pt" + i);
                pt.transform.SetParent(trackGo.transform);
                pt.transform.position = points[i];
            }
            tp.Build();

            var progress = carGo.AddComponent<RaceProgress>();
            progress.Resync();

            var driver = carGo.AddComponent<AiDriver>();
            driver.baseLaneOffset = 0f;
            driver.Initialize(null, 0f);

            // Spawn mine on the track at distance 15m ahead on the center line (lane 0)
            var mine = director.RequestMine(new MineSpawn
            {
                Owner = rivalGo,
                Position = new Vector3(15f, 0f, 0f),
                Rotation = Quaternion.identity,
                Damage = 25f,
                Tick = 0
            });

            // Sample driver command -> AiDriver will detect mine ahead and adjust targetLane away from 0
            var cmd = driver.Sample(100);
            Assert.AreNotEqual(0f, driver.TargetLane);

            Object.DestroyImmediate(trackGo);
        }

        [Test]
        public void BotAim_CalmTarget_KeepsLeadAndBaseTelegraph()
        {
            var calm = BotAim.Evaluate(0.4f, 0.42f, machineGun: true, hardTier: false);
            Assert.IsFalse(calm.StripLateralLead);
            Assert.AreEqual(0.42f, calm.TelegraphSeconds, 0.001f);
            Assert.AreEqual(2, calm.BurstShots);

            var hard = BotAim.Evaluate(0f, BotAim.BaseTelegraph("hard"), machineGun: true, hardTier: true);
            Assert.AreEqual(0.35f, hard.TelegraphSeconds, 0.001f);
            Assert.AreEqual(3, hard.BurstShots);
            Assert.AreEqual(0.55f, BotAim.BaseTelegraph("easy"), 0.001f);
            Assert.AreEqual(0.42f, BotAim.BaseTelegraph("normal"), 0.001f);
        }

        [Test]
        public void BotAim_LaneChange_StripsLateralLeadAndShortensBurst()
        {
            var change = BotAim.Evaluate(4f, 0.42f, machineGun: true, hardTier: true);
            Assert.IsTrue(change.StripLateralLead);
            Assert.AreEqual(0.42f * BotAim.LaneChangeTelegraphScale, change.TelegraphSeconds, 0.001f);
            Assert.AreEqual(2, change.BurstShots);
            Assert.AreEqual(0f, BotAim.Evaluate(4f, 0f, false, false).TelegraphSeconds, 0.001f);
        }

        [Test]
        public void CarCannon_SolveAimPoint_DropsOnlySidewaysLead()
        {
            car.transform.position = Vector3.zero;
            car.transform.rotation = Quaternion.identity;
            rival.transform.position = new Vector3(0f, 0f, 20f);
            rival.Body.linearVelocity = new Vector3(10f, 0f, 30f);

            Vector3 full = cannon.SolveAimPoint(rival, false);
            Vector3 stripped = cannon.SolveAimPoint(rival, true);
            Assert.Greater(Mathf.Abs(full.x), Mathf.Abs(stripped.x) + 0.5f);
            Assert.AreEqual(full.z, stripped.z, 0.05f);
        }

        [Test]
        public void CarCannon_CommitAim_HoldsBarrelWhileLiveTurretFollows()
        {
            car.transform.position = new Vector3(0f, 200f, 0f);
            car.transform.rotation = Quaternion.identity;
            rival.transform.position = new Vector3(0f, 200f, 15f);
            // The procedural turret build can fail in EditMode (shader). Give the cannon a plain mount.
            var pivot = new GameObject("Turret").transform;
            pivot.SetParent(car.transform, false);
            var barrel = new GameObject("Barrel").transform;
            barrel.SetParent(pivot, false);
            var muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(barrel, false);
            muzzle.localPosition = new Vector3(0f, 0f, 1.5f);
            cannon.turret = pivot;
            cannon.barrel = barrel;
            cannon.muzzle = muzzle;
            cannon.turretTurnSpeed = 20000f;

            for (int t = 0; t < 10; t++) cannon.SimulationStep(t);
            Assert.IsFalse(cannon.HasAimCommit);
            Assert.Less(Mathf.Abs(cannon.TurretYaw), 1f);

            rival.transform.position = new Vector3(2f, 200f, 15f);
            for (int t = 10; t < 20; t++) cannon.SimulationStep(t);
            Assert.Greater(cannon.TurretYaw, 4f);

            rival.transform.position = new Vector3(0f, 200f, 15f);
            cannon.CommitAim(cannon.SolveAimPoint(rival, false));
            rival.transform.position = new Vector3(2f, 200f, 15f);
            for (int t = 20; t < 30; t++) cannon.SimulationStep(t);
            Assert.Less(Mathf.Abs(cannon.TurretYaw), 2f);
        }

        [Test]
        public void AiGunner_Telegraph_FreezesAimPointWhileWarningStays()
        {
            car.transform.position = new Vector3(0f, 200f, 0f);
            car.transform.rotation = Quaternion.identity;
            rival.transform.position = new Vector3(0f, 200f, 15f);
            gunner.startGraceSeconds = 0f;
            gunner.telegraphSeconds = 0.42f;
            gunner.fireCooldown = 1f;
            cannon.RefillAmmo();

            Assert.IsFalse(gunner.WantsFire(100));
            Assert.IsTrue(cannon.HasAimCommit, $"ready={cannon.IsReady} ammo={cannon.Ammo} locked={cannon.locked} shots={gunner.ShotsRequested}");
            Assert.AreSame(rival, gunner.AimTarget);
            Assert.IsTrue(cannon.HasAimCommit);
            Vector3 committed = cannon.AimCommitPoint;

            rival.transform.position = new Vector3(4f, 200f, 15f);
            Assert.IsFalse(gunner.WantsFire(101));
            Assert.AreSame(rival, gunner.AimTarget);
            Assert.AreEqual(committed, cannon.AimCommitPoint);
        }

        [Test]
        public void AiGunner_LaneChange_CommitsAimWithoutSidewaysLead()
        {
            car.transform.position = new Vector3(0f, 200f, 0f);
            car.transform.rotation = Quaternion.identity;
            rival.transform.position = new Vector3(0f, 200f, 15f);
            rival.Body.linearVelocity = new Vector3(8f, 0f, 20f);
            gunner.startGraceSeconds = 0f;
            gunner.telegraphSeconds = 0.42f;
            cannon.RefillAmmo();
            cannon.locked = true;
            gunner.WantsFire(100);

            rival.transform.position = new Vector3(1f, 200f, 15f);
            cannon.locked = false;
            Assert.IsFalse(gunner.WantsFire(110));
            Assert.AreSame(rival, gunner.AimTarget);
            Vector3 expected = cannon.SolveAimPoint(rival, true);
            Assert.AreEqual(expected.x, cannon.AimCommitPoint.x, 0.05f);
            Assert.AreEqual(expected.z, cannon.AimCommitPoint.z, 0.05f);
            Assert.Less(Mathf.Abs(cannon.AimCommitPoint.x), Mathf.Abs(cannon.SolveAimPoint(rival, false).x) - 0.5f);
        }
    }
}
