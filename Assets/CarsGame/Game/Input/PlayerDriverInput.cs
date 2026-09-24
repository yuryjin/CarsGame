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

        private InputActionMap map;
        private InputAction throttleAction, steerAction, brakeAction, fireAction, secondaryAction, resetAction;
        private float throttle, steer;
        private bool brake, fire, secondary;
        private readonly EdgeLatch resetLatch = new EdgeLatch();
        private int pendingPrimary;   // last slot picked (1..10), 0 = none
        private readonly InputAction[] primaryActions = new InputAction[PrimaryWeaponCatalog.Count];
        private static readonly Key[] NumberKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
        private static readonly Key[] NumpadKeys = { Key.Numpad1, Key.Numpad2, Key.Numpad3, Key.Numpad4, Key.Numpad5, Key.Numpad6, Key.Numpad7, Key.Numpad8, Key.Numpad9, Key.Numpad0 };

        private CarWeapons weapons;
        private Vector2 wheelPointer;
        public bool WeaponWheelOpen { get; private set; }
        public int WheelSelection { get; private set; }

        public bool UsingActionMap => map != null;
        /// <summary>Current throttle level (0..1 forward) even while the car ignores commands (countdown revving).</summary>
        public float ThrottleLevel => Mathf.Max(0f, throttle);
        /// <summary>The player's own throttle input before auto-throttle fills it in (0..1 forward).</summary>
        public float RawThrottle { get; private set; }

        private void OnEnable()
        {
            weapons = GetComponent<CarWeapons>();
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
            throttle = steer = 0f; brake = fire = secondary = false; resetLatch.Clear(); pendingPrimary = 0;
            WeaponWheelOpen = false; WheelSelection = 0;
        }

        private void Update()
        {
            if (map != null) ReadActions(); else PollDevices();

            ReadWeaponSelection();
            RawThrottle = Mathf.Max(0f, throttle);
            if (autoThrottle && Mathf.Abs(throttle) < 0.05f) throttle = brake ? 0f : 1f;
        }

        private void ReadWeaponSelection()
        {
            if (PauseController.IsPaused || weapons == null)
            {
                WeaponWheelOpen = false;
                WheelSelection = pendingPrimary = 0;
                return;
            }

            var mouse = Mouse.current;
            bool held = Keyboard.current != null && Keyboard.current.qKey.isPressed;
            bool wasOpen = WeaponWheelOpen;
            WeaponWheelOpen = held;
            if (held && !wasOpen) { wheelPointer = Vector2.zero; WheelSelection = 0; }
            if (held && mouse != null)
            {
                wheelPointer = Vector2.ClampMagnitude(wheelPointer + mouse.delta.ReadValue(), 180f);
                WheelSelection = WeaponWheelGraphic.SlotAt(wheelPointer);
            }

            if (held || wasOpen) fire = secondary = false;

            // Wheel and number keys use the same simulation command.
            if (mouse != null && mouse.scroll.y.ReadValue() != 0f)
            {
                int current = pendingPrimary != 0 ? pendingPrimary : (int)weapons.PrimaryType + 1;
                pendingPrimary = weapons.CyclePrimarySlot(current, mouse.scroll.y.ReadValue() > 0f ? 1 : -1);
            }
            if (pendingPrimary != 0 && held)
            {
                WheelSelection = pendingPrimary;
                wheelPointer = WeaponWheelGraphic.SlotDirection(pendingPrimary) * 100f;
            }
            if (wasOpen && !held && pendingPrimary == 0) pendingPrimary = WheelSelection;
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
                if (primaryActions[i] != null && primaryActions[i].WasPressedThisFrame()) pendingPrimary = i + 1;
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
                    if (k[NumberKeys[i]].wasPressedThisFrame || k[NumpadKeys[i]].wasPressedThisFrame) pendingPrimary = i + 1;
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
                if (g.dpad.down.wasPressedThisFrame) pendingPrimary = 1;
                if (g.dpad.up.wasPressedThisFrame) pendingPrimary = 2;
            }
        }

        public DriveCommand Sample(int tick)
        {
            bool sec = secondary;
            if (!WeaponWheelOpen && !sec && autoSecondary && tick % 10 == 0)
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
                Steer = steer,
                Brake = brake,
                Fire = fire,
                Secondary = sec,
                SelectPrimary = select,
                ResetEdge = resetLatch.ConsumeAll()
            }.Clamped();
        }
    }
}
