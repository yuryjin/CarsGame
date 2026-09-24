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
                Assert.AreEqual(10, input.Sample(5).SelectPrimary, "Scrolling backward wraps to key 0.");
                weapons.machineGunUnlocked = false;
                InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, 120f) });
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
                Assert.AreEqual(3, input.Sample(6).SelectPrimary, "Scrolling skips unavailable weapons.");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.Update();
                typeof(PlayerDriverInput).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(input, null);
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
    }
}
