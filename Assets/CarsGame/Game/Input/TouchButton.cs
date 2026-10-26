using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Controls;

namespace RacingGame
{
    /// <summary>
    /// OnScreenButton with physical-touch release recovery. Some WebGL multi-touch sequences
    /// end in the Touchscreen state without delivering PointerUp to the original UI target.
    /// </summary>
    public sealed class TouchButton : OnScreenButton, IPointerDownHandler, IPointerUpHandler
    {
        private struct Press
        {
            public int pointerId, touchId;
            public Touchscreen screen;
            public ButtonControl button;

            public bool IsPressed
            {
                get
                {
                    if (screen == null) return button != null && button.device.added && button.isPressed;
                    if (!screen.added) return false;
                    foreach (var touch in screen.touches)
                        if (touch.touchId.ReadValue() == touchId && touch.press.isPressed) return true;
                    return false;
                }
            }
        }

        private readonly List<Press> presses = new List<Press>(2);
        // Read only on an explicit QA snapshot; no per-frame logging or string construction.
        internal int TrackedPressCount => presses.Count;
        internal int DownCount { get; private set; }
        internal int UpCount { get; private set; }
        internal int RecoveredCount { get; private set; }
        internal int LastTouchId { get; private set; }
        internal string LastDevice { get; private set; }

        public new void OnPointerDown(PointerEventData eventData)
        {
            DownCount++;
            var pointer = eventData as ExtendedPointerEventData;
            LastDevice = pointer?.device?.layout ?? eventData?.GetType().Name;
            LastTouchId = pointer?.touchId ?? 0;
            if (pointer != null)
            {
                Remove(pointer.pointerId);
                var press = new Press { pointerId = pointer.pointerId };
                if (pointer.device is Touchscreen screen && pointer.touchId != 0)
                {
                    press.screen = screen; press.touchId = pointer.touchId;
                }
                else
                {
                    // WebGL may route the first finger through its emulated Mouse. Associate
                    // that exact screen position with the finger, whose release is reliable.
                    var touchscreen = Touchscreen.current;
                    if (touchscreen != null)
                        foreach (var touch in touchscreen.touches)
                            if (touch.press.isPressed && (touch.position.ReadValue() - pointer.position).sqrMagnitude <= 16f)
                            {
                                press.screen = touchscreen; press.touchId = touch.touchId.ReadValue(); break;
                            }
                    if (press.screen == null && pointer.device is Mouse mouse)
                        press.button = pointer.button == PointerEventData.InputButton.Right ? mouse.rightButton
                            : pointer.button == PointerEventData.InputButton.Middle ? mouse.middleButton : mouse.leftButton;
                    else if (press.screen == null && pointer.device is Pen pen) press.button = pen.tip;
                }
                if (press.screen != null || press.button != null)
                {
                    presses.Add(press);
                    LastTouchId = press.touchId;
                }
            }
            base.OnPointerDown(eventData);
        }

        public new void OnPointerUp(PointerEventData eventData)
        {
            UpCount++;
            if (eventData != null) Remove(eventData.pointerId);
            // Lifting one of two fingers on the same control must not release the other one.
            if (presses.Count == 0) base.OnPointerUp(eventData);
        }

        private void Remove(int pointerId)
        {
            for (int i = presses.Count - 1; i >= 0; i--)
                if (presses[i].pointerId == pointerId) presses.RemoveAt(i);
        }

        private void LateUpdate()
        {
            bool released = false;
            for (int i = presses.Count - 1; i >= 0; i--)
                if (!presses[i].IsPressed) { presses.RemoveAt(i); released = true; }
            if (released && presses.Count == 0) { RecoveredCount++; base.OnPointerUp(null); }
        }

        protected override void OnDisable()
        {
            presses.Clear();
            base.OnDisable();
        }
    }
}
