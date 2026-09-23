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

        public bool UsingActionMap => map != null;
        /// <summary>Current throttle level (0..1 forward) even while the car ignores commands (countdown revving).</summary>
        public float ThrottleLevel => Mathf.Max(0f, throttle);
        /// <summary>The player's own throttle input before auto-throttle fills it in (0..1 forward).</summary>
        public float RawThrottle { get; private set; }

        private void OnEnable()
        {
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
                map.Enable();
            }
            else
            {
                Debug.LogWarning("[PlayerDriverInput] 'Driving' action map not found in InputSystem.actions; falling back to device polling.", this);
            }
        }

        private void OnDisable()
        {
            throttle = steer = 0f; brake = fire = secondary = false; resetLatch.Clear();
        }

        private void Update()
        {
            if (map != null) ReadActions(); else PollDevices();

            RawThrottle = Mathf.Max(0f, throttle);
            if (autoThrottle && Mathf.Abs(throttle) < 0.05f) throttle = brake ? 0f : 1f;
        }

        private void ReadActions()
        {
            throttle = throttleAction != null ? throttleAction.ReadValue<float>() : 0f;
            steer = steerAction != null ? steerAction.ReadValue<float>() : 0f;
            brake = brakeAction != null && brakeAction.IsPressed();
            fire = fireAction != null && fireAction.IsPressed();
            secondary = secondaryAction != null && secondaryAction.IsPressed();
            if (resetAction != null && resetAction.WasPressedThisFrame()) resetLatch.Press();
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
            }
        }

        public DriveCommand Sample(int tick)
        {
            bool sec = secondary;
            if (!sec && autoSecondary && tick % 10 == 0)
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

            return new DriveCommand
            {
                Tick = tick,
                Throttle = throttle,
                Steer = steer,
                Brake = brake,
                Fire = fire,
                Secondary = sec,
                ResetEdge = resetLatch.ConsumeAll()
            }.Clamped();
        }
    }
}
