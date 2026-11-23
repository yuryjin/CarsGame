using UnityEngine;
using UnityEngine.InputSystem;
using RacingGame.Core.Sim;

namespace RacingGame
{
    /// <summary>
    /// Human input → <see cref="DriveCommand"/>. Reads the project-wide "Driving" action map
    /// every frame (keyboard, gamepad and on-screen touch controls all bind there), caches the
    /// analog levels and latches edges, then hands one command per simulation tick to the car.
    /// </summary>
    public sealed class PlayerDriverInput : MonoBehaviour, ICarInputSource
    {
        [Tooltip("Full throttle unless braking — one-hand touch play.")]
        public bool autoThrottle;
        [Tooltip("Automatically deploy secondary weapon when a rival tails closely behind.")]
        public bool autoSecondary;
        [Tooltip("Turret tracks rivals by itself. Off = the mouse aims. Follows the saved setting when a save is loaded.")]
        public bool autoAim = true;
        [Tooltip("Turret sector (deg off the nose) while the mouse aims; the body's own arc applies if wider.")]
        public float manualAimYawLimit = 90f;
        [Range(0f, 1f)] public float touchSteeringAssist = 0.35f;

        private InputActionMap map;
        private InputAction throttleAction, steerAction, brakeAction, fireAction, secondaryAction, resetAction, rearViewAction;
        private float throttle, steer;
        private bool brake, fire, secondary, mouseAimActive;
        private readonly EdgeLatch resetLatch = new EdgeLatch();
        private int pendingPrimary;   // last slot picked (1..50), 0 = none
        private readonly InputAction[] primaryActions = new InputAction[PrimaryWeaponCatalog.SlotsPerPage];
        private static readonly Key[] NumberKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
        private static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9, Key.Numpad0 };

        private CarWeapons weapons;
        private CarController car;
        private RaceProgress progress;
        private CarCannon cannon;
        // ponytail: 32 ray hits cover normal sightlines; grow this buffer if dense scenery hides targets.
        private readonly RaycastHit[] aimHits = new RaycastHit[32];
        private Vector2 wheelPointer;
        public bool WeaponWheelOpen { get; private set; }
        public int WheelSelection { get; private set; }
        public int SelectionPage { get; private set; }
        public bool RearViewHeld { get; private set; }
        private bool rearNeedsRelease, focusLost;

        public bool UsingActionMap => map != null;
        /// <summary>Current throttle level (0..1 forward) even while the car ignores commands (countdown revving).</summary>
        public float ThrottleLevel => Mathf.Max(0f, throttle);
        /// <summary>Physical throttle before auto-throttle, including while paused. Launch timing
        /// observes held controls without allowing them into the paused simulation.</summary>
        public float RawThrottle
        {
            get
            {
                if (map != null) return Mathf.Max(0f, throttleAction != null ? throttleAction.ReadValue<float>() : 0f);
                float value = 0f;
                var k = Keyboard.current;
                if (k != null)
                {
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) value += 1f;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) value -= 1f;
                }
                var g = Gamepad.current;
                if (g != null)
                {
                    float right = g.rightTrigger.ReadValue(), left = g.leftTrigger.ReadValue();
                    if (right > 0.05f || left > 0.05f) value = right - left;
                }
                return Mathf.Max(0f, value);
            }
        }

        private void OnEnable()
        {
            PauseController.PausedChanged += OnPaused;
            weapons = GetComponent<CarWeapons>();
            car = GetComponent<CarController>();
            cannon = GetComponent<CarCannon>();
            progress = GetComponent<RaceProgress>();
            SelectionPage = weapons != null ? (int)weapons.PrimaryType / PrimaryWeaponCatalog.SlotsPerPage : 0;
            var asset = InputSystem.actions;
            map = asset != null ? asset.FindActionMap("Driving") : null;
            if (map != null)
            {
                throttleAction = map.FindAction("Throttle");
                steerAction = map.FindAction("Steer");
                brakeAction = map.FindAction("Brake");
                fireAction = map.FindAction("Fire");
                secondaryAction = map.FindAction("Secondary");
                resetAction = map.FindAction("Reset");
                rearViewAction = map.FindAction("RearView");
                for (int i = 0; i < primaryActions.Length; i++)
                    primaryActions[i] = map.FindAction(i == 0 ? "PrimaryCannon" : i == 1 ? "PrimaryMachineGun" : "PrimarySlot" + (i + 1));
                map.Enable();
            }
            else
            {
                Debug.LogWarning("[PlayerDriverInput] 'Driving' action map not found in InputSystem.actions; falling back to device polling.", this);
            }
        }

        private void OnDisable()
        {
            PauseController.PausedChanged -= OnPaused;
            ClearInput();
        }

        private void OnApplicationFocus(bool focused)
        {
            focusLost = !focused;
            if (!focused) ClearInput();
        }

        private void OnPaused(bool paused)
        {
            // Simulation stops during a pause, but frame input continues. Discard commands
            // already waiting for a tick as well as those entered while a menu is open.
            if (paused) ClearInput();
        }

        private void ClearInput()
        {
            if (cannon != null) cannon.ClearAimCommit();
            RearViewHeld = false; rearNeedsRelease = true;
            throttle = steer = 0f; brake = fire = secondary = mouseAimActive = false; resetLatch.Clear(); pendingPrimary = 0;
            WeaponWheelOpen = false; WheelSelection = 0;
        }

        private void Update()
        {
            if (PauseController.IsPaused) { ClearInput(); return; }
            if (map != null) ReadActions(); else PollDevices();
            bool rearPressed = rearViewAction != null ? rearViewAction.IsPressed()
                : Keyboard.current?.vKey.isPressed == true || Gamepad.current?.rightStickButton.isPressed == true;
            if (!rearPressed) rearNeedsRelease = false;
            RearViewHeld = rearPressed && !rearNeedsRelease && !focusLost;

            ReadWeaponSelection();
            if (PlatformGlue.Save != null && PlatformGlue.Save.Loaded) autoAim = PlatformGlue.Save.Data.settings.autoAim;
            var mouse = Mouse.current;
            if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 0.01f || mouse.leftButton.isPressed)) mouseAimActive = true;
            var gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.rightShoulder.isPressed || gamepad.buttonWest.isPressed)) mouseAimActive = false;
            if (autoThrottle && Mathf.Abs(throttle) < 0.05f) throttle = brake ? 0f : 1f;
        }

        private void ReadWeaponSelection()
        {
            if (PauseController.IsPaused || weapons == null || cannon == null || cannon.locked)
            {
                WeaponWheelOpen = false;
                WheelSelection = pendingPrimary = 0;
                return;
            }

            var mouse = Mouse.current;
            if (weapons.CompactLoadout)
            {
                WeaponWheelOpen = false;
                WheelSelection = 0;
                SelectionPage = 0;
                if ((mouse != null && mouse.scroll.y.ReadValue() != 0f)
                    || (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)) CycleLoadout();
                return;
            }
            bool held = Keyboard.current != null && Keyboard.current.qKey.isPressed;
            bool wasOpen = WeaponWheelOpen;
            WeaponWheelOpen = held;
            if (held && !wasOpen) { wheelPointer = Vector2.zero; WheelSelection = 0; }
            if (held && mouse != null)
            {
                wheelPointer = Vector2.ClampMagnitude(wheelPointer + mouse.delta.ReadValue(), 180f);
                int localSlot = WeaponWheelGraphic.SlotAt(wheelPointer);
                WheelSelection = localSlot == 0 ? 0 : SelectionPage * PrimaryWeaponCatalog.SlotsPerPage + localSlot;
            }

            if (held || wasOpen) fire = secondary = false;

            float scroll = mouse != null ? mouse.scroll.y.ReadValue() : 0f;
            bool tab = held && Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
            if (held && (scroll != 0f || tab))
            {
                int step = scroll != 0f ? (scroll > 0f ? 1 : -1) : Keyboard.current.leftShiftKey.isPressed ? -1 : 1;
                SelectionPage = (SelectionPage + step + PrimaryWeaponCatalog.PageCount) % PrimaryWeaponCatalog.PageCount;
                WheelSelection = pendingPrimary = 0;
                wheelPointer = Vector2.zero;
            }
            else if (!held && scroll != 0f)
            {
                int current = pendingPrimary != 0 ? pendingPrimary : (int)weapons.PrimaryType + 1;
                pendingPrimary = weapons.CyclePrimarySlot(current, scroll > 0f ? 1 : -1);
                SelectionPage = (pendingPrimary - 1) / PrimaryWeaponCatalog.SlotsPerPage;
            }
            if (pendingPrimary != 0 && held)
            {
                WheelSelection = pendingPrimary;
                wheelPointer = WeaponWheelGraphic.SlotDirection((pendingPrimary - 1) % PrimaryWeaponCatalog.SlotsPerPage + 1) * 100f;
            }
            if (wasOpen && !held && pendingPrimary == 0) pendingPrimary = WheelSelection;
        }

        public void CycleLoadout()
        {
            if (PauseController.IsPaused || weapons == null || cannon == null || cannon.locked) return;
            pendingPrimary = weapons.CyclePrimarySlot((int)weapons.PrimaryType + 1, 1);
        }

        private void SelectSlot(int index)
        {
            if (weapons != null && weapons.CompactLoadout)
            {
                if (index < 2) pendingPrimary = weapons.LoadoutSlot(index);
            }
            else pendingPrimary = SelectionPage * PrimaryWeaponCatalog.SlotsPerPage + index + 1;
        }

        private void ReadActions()
        {
            throttle = throttleAction != null ? throttleAction.ReadValue<float>() : 0f;
            steer = steerAction != null ? steerAction.ReadValue<float>() : 0f;
            brake = brakeAction != null && brakeAction.IsPressed();
            fire = fireAction != null && fireAction.IsPressed();
            secondary = secondaryAction != null && secondaryAction.IsPressed();
            if (resetAction != null && resetAction.WasPressedThisFrame()) resetLatch.Press();
            for (int i = 0; i < primaryActions.Length; i++)
                if (primaryActions[i] != null && primaryActions[i].WasPressedThisFrame()) SelectSlot(i);
        }

        private void PollDevices()
        {
            throttle = 0f; steer = 0f; brake = false; fire = false; secondary = false;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.wKey.isPressed || k.upArrowKey.isPressed) throttle += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) throttle -= 1f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) steer -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) steer += 1f;
                brake = k.spaceKey.isPressed;
                fire = k.leftCtrlKey.isPressed || k.rightCtrlKey.isPressed;
                secondary = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed || k.eKey.isPressed;
                if (k.rKey.wasPressedThisFrame) resetLatch.Press();
                for (int i = 0; i < NumberKeys.Length; i++)
                    if (k[NumberKeys[i]].wasPressedThisFrame || k[NumpadKeys[i]].wasPressedThisFrame) SelectSlot(i);
            }
            var m = Mouse.current;
            if (m != null)
            {
                if (m.leftButton.isPressed) fire = true;
                if (m.rightButton.isPressed) secondary = true;
            }
            var g = Gamepad.current;
            if (g != null)
            {
                float x = g.leftStick.x.ReadValue();
                if (Mathf.Abs(x) > 0.15f) steer = x;
                float rt = g.rightTrigger.ReadValue(), lt = g.leftTrigger.ReadValue();
                if (rt > 0.05f || lt > 0.05f) throttle = rt - lt;
                if (g.buttonSouth.isPressed) brake = true;
                if (g.rightShoulder.isPressed || g.buttonWest.isPressed) fire = true;
                if (g.leftShoulder.isPressed || g.buttonEast.isPressed) secondary = true;
                if (g.buttonNorth.wasPressedThisFrame) resetLatch.Press();
                if (g.dpad.down.wasPressedThisFrame) SelectSlot(0);
                if (g.dpad.up.wasPressedThisFrame) SelectSlot(1);
            }
        }

        private float AssistedSteer()
        {
            if (!TouchControlsPresenter.IsShown || touchSteeringAssist <= 0f ||
                PlatformGlue.Save == null || PlatformGlue.Save.Data.Races >= 3 ||
                car == null || progress == null || progress.Path == null) return steer;
            var path = progress.Path;
            if (path.OnFork(transform.position)) return steer;
            float speed = car.Body.linearVelocity.magnitude;
            float ahead = path.Wrap(progress.Distance + Mathf.Clamp(speed * 0.5f, 6f, 14f));
            Vector3 target = path.Position(ahead) + path.Right(ahead) * path.Profile.LaneAt(ahead);
            Vector3 local = transform.InverseTransformPoint(target);
            if (local.z <= 0f) return steer;
            return BlendSteering(steer, Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, touchSteeringAssist);
        }

        public static float BlendSteering(float input, float angleDegrees, float strength) =>
            Mathf.Clamp(input + Mathf.Clamp(angleDegrees / 60f, -strength, strength) * (1f - Mathf.Clamp01(Mathf.Abs(input))), -1f, 1f);

        public DriveCommand Sample(int tick)
        {
            if (PauseController.IsPaused) { ClearInput(); return new DriveCommand { Tick = tick }; }
            UpdateMouseAim();
            bool sec = secondary;
            // Touch auto-fire of a planted charge: blow it the moment a rival rolls over it (every tick, not every 10th).
            if (!WeaponWheelOpen && !sec && autoSecondary && weapons != null && weapons.PlantedCharge != null)
                sec = TargetFinder.ChargeWorthFiring(GetComponent<CarController>(), weapons.PlantedCharge, tick);
            else if (!WeaponWheelOpen && !sec && autoSecondary && tick % 10 == 0)
            {
                var car = GetComponent<CarController>();
                if (car != null)
                {
                    var weapons = car.Weapons;
                    if (weapons != null && weapons.secondaryType != SecondaryWeaponType.None && weapons.IsSecondaryReady(tick))
                    {
                        if (TargetFinder.FindPursuer(car, transform.position, -transform.forward, 15f, 25f, 3f) != null)
                        {
                            sec = true;
                        }
                    }
                }
            }

            int select = pendingPrimary;
            pendingPrimary = 0;
            return new DriveCommand
            {
                Tick = tick,
                Throttle = throttle,
                Steer = AssistedSteer(),
                Brake = brake,
                Fire = fire,
                Secondary = sec,
                SelectPrimary = select,
                ResetEdge = resetLatch.ConsumeAll()
            }.Clamped();
        }

        private void UpdateMouseAim()
        {
            if (cannon == null) cannon = GetComponent<CarCannon>();
            if (cannon == null) return;
            var mouse = Mouse.current;
            var camera = Camera.main;
            if (autoAim || !mouseAimActive || mouse == null || camera == null || TouchControlsPresenter.IsShown || WeaponWheelOpen || PauseController.IsPaused)
            {
                cannon.ClearAimCommit();
                return;
            }

            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            float distance = float.PositiveInfinity;
            Vector3 point = default;
            int count = Physics.RaycastNonAlloc(ray, aimHits, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = aimHits[i];
                if (hit.distance >= distance || hit.collider.transform.IsChildOf(transform)) continue;
                distance = hit.distance;
                point = hit.point;
            }
            if (distance == float.PositiveInfinity)
            {
                var plane = new Plane(Vector3.up, transform.position + Vector3.up * 0.5f);
                point = plane.Raycast(ray, out float groundDistance) && groundDistance > 0f
                    ? ray.GetPoint(groundDistance) : ray.GetPoint(cannon.turretMaxRange);
            }
            if (car != null && car.Body != null)
            {
                Vector3 origin = cannon.muzzle != null ? cannon.muzzle.position : transform.position;
                float flight = Vector3.Distance(origin, point) / Mathf.Max(1f, cannon.shellSpeed);
                point -= car.Body.linearVelocity * flight;
            }
            cannon.CommitAim(point, allowHoming: true, yawLimit: manualAimYawLimit);
        }
    }
}
