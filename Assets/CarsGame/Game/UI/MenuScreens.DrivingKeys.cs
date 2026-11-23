using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace RacingGame
{
    public sealed partial class MenuScreens
    {
        private InputAction captureAction;
        private int captureIndex, keyFrame = -1;
        private TextMeshProUGUI keyPrompt;
        public bool HandlingDrivingKey => current == "capture-key" || keyFrame == Time.frameCount;

        private void BuildKeySettings(RectTransform root)
        {
            var content = UiFactory.Scroll(Header(root, "keys.title", showWallet: false), "KeysScroll", Vector2.zero, Vector2.zero);
            var map = DrivingBindings.Map;
            if (map == null) return;
            foreach (var action in map.actions)
            {
                var groups = new Dictionary<string, List<int>>();
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (!DrivingBindings.IsKeyboard(action.bindings[i])) continue;
                    string label = DrivingBindings.Label(action, i);
                    if (!groups.TryGetValue(label, out var group)) groups.Add(label, group = new List<int>());
                    group.Add(i);
                }
                foreach (var group in groups)
                {
                    var row = Item(content, "KeyRow" + action.name + group.Value[0], Compact ? 100f : 86f);
                    Label(row, "Action", group.Key, 27f, Vector2.zero, new Vector2(.39f, 1f),
                        new Vector2(14f, 4f), new Vector2(-4f, -4f));
                    for (int slot = 0; slot < group.Value.Count; slot++)
                    {
                        int index = group.Value[slot];
                        float step = .61f / group.Value.Count;
                        UiFactory.Button(row, "Key" + action.name + index, DrivingBindings.KeyName(action, index),
                            () => { captureAction = action; captureIndex = index; keyFrame = Time.frameCount; Show("capture-key"); EventSystem.current?.SetSelectedGameObject(null); },
                            new Vector2(.39f + slot * step, 0f), new Vector2(.39f + (slot + 1) * step, 1f),
                            new Vector2(4f, 6f), new Vector2(-4f, -6f), 24f, UiFactory.Raised);
                    }
                }
            }
            UiFactory.Button(Item(content, "RestoreKeyRow", 88f, false), "RestoreDrivingKeys", Loc.Get("keys.restore"), () =>
            {
                map.RemoveAllBindingOverrides();
                DrivingBindings.Save();
                Show("keys");
            }, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 26f, UiFactory.Raised);
        }

        private void BuildKeyCapture(RectTransform root)
        {
            var body = Header(root, "keys.title", back: false, showWallet: false);
            keyPrompt = Label(body, "KeyPrompt", Loc.Format("keys.press", "action", DrivingBindings.Label(captureAction, captureIndex)), 34f,
                new Vector2(0f, .35f), Vector2.one, new Vector2(16f, 4f), new Vector2(-16f, -4f), Color.white, TextAlignmentOptions.Center);
            UiFactory.Button(body, "CancelDrivingKey", Loc.Get("keys.cancel"), () => { keyFrame = Time.frameCount; Show("keys"); },
                new Vector2(.5f, .08f), new Vector2(.5f, .08f), new Vector2(-164f, 0f), new Vector2(164f, 68f), 26f, UiFactory.Raised);
        }

        private void CaptureDrivingKey()
        {
            var keyboard = Keyboard.current;
            if (keyFrame == Time.frameCount) return;
            if (keyboard?.escapeKey.wasPressedThisFrame == true || Gamepad.current?.buttonEast.wasPressedThisFrame == true)
            {
                keyFrame = Time.frameCount;
                Show("keys");
                return;
            }
            if (keyboard == null) return;
            foreach (var key in keyboard.allKeys)
            {
                if (!key.wasPressedThisFrame) continue;
                keyFrame = Time.frameCount;
                string path = "<Keyboard>/" + key.name;
                string conflict = DrivingBindings.Conflict(captureAction, captureIndex, path);
                if (conflict != null)
                {
                    keyPrompt.text = Loc.Format("keys.conflict", "button", key.displayName, "action", conflict);
                    return;
                }
                captureAction.ApplyBindingOverride(captureIndex, path);
                DrivingBindings.Save();
                var button = "Key" + captureAction.name + captureIndex;
                Show("keys");
                var selected = System.Array.Find(panel.GetComponentsInChildren<UnityEngine.UI.Button>(), b => b.name == button);
                if (selected != null) EventSystem.current?.SetSelectedGameObject(selected.gameObject);
                return;
            }
        }
    }
}
