using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;

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

            public bool IsPressed
            {
                get
                {
                    if (screen == null || !screen.added) return false;
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
            if (eventData is ExtendedPointerEventData input && input.device is Touchscreen screen && input.touchId != 0)
            {
                Remove(input.pointerId);
                presses.Add(new Press { pointerId = input.pointerId, touchId = input.touchId, screen = screen });
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
