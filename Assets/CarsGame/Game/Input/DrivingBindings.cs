using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace RacingGame
{
    internal static class DrivingBindings
    {
        public static InputActionMap Map => InputSystem.actions?.FindActionMap("Driving");
        private static InputActionMap appliedMap;
        private static string appliedJson;
        private static readonly Dictionary<string, string> hints = new Dictionary<string, string>();

        public static void Load(string json, bool force = false)
        {
            var map = Map;
            if (map == null || !force && map == appliedMap && json == appliedJson) return;
            if (string.IsNullOrEmpty(json)) map.RemoveAllBindingOverrides();
            else
            {
                try { map.LoadBindingOverridesFromJson(json); }
                catch (ArgumentException) { map.RemoveAllBindingOverrides(); }
            }
            appliedMap = map;
            appliedJson = json;
            hints.Clear();
        }

        public static void Save()
        {
            var save = PlatformGlue.Save;
            appliedMap = Map;
            appliedJson = appliedMap.SaveBindingOverridesAsJson();
            hints.Clear();
            save.Data.settings.drivingKeyBindings = appliedJson;
            save.MarkDirty();
        }

        public static bool IsKeyboard(InputBinding binding) =>
            binding.path != null && binding.path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase);

        public static string Label(InputAction action, int index)
        {
            switch (action.name)
            {
                case "Throttle": return Loc.Get(action.bindings[index].name == "positive" ? "keys.accelerate" : "keys.reverse");
                case "Steer": return Loc.Get(action.bindings[index].name == "negative" ? "keys.left" : "keys.right");
                case "Fire": return Loc.Get("touch.fire");
                case "Brake": return Loc.Get("touch.brake");
                case "Reset": return Loc.Get("touch.reset");
                case "Pause": return Loc.Get("pause.title");
                case "Secondary": return Loc.Get("keys.secondary");
                case "RearView": return Loc.Get("touch.rear_view");
                case "PrimaryCannon": return Loc.Format("keys.slot", "n", 1);
                case "PrimaryMachineGun": return Loc.Format("keys.slot", "n", 2);
                default: return action.name.StartsWith("PrimarySlot", StringComparison.Ordinal)
                    ? Loc.Format("keys.slot", "n", action.name.Substring("PrimarySlot".Length)) : action.name;
            }
        }

        public static string Conflict(InputAction action, int index, string path)
        {
            if (path == "<Keyboard>/q" || path == "<Keyboard>/tab") return Loc.Get("keys.wheel");
            foreach (var other in Map.actions)
            for (int i = 0; i < other.bindings.Count; i++)
            {
                var binding = other.bindings[i];
                if ((other != action || i != index) && IsKeyboard(binding) &&
                    string.Equals(path, binding.effectivePath, StringComparison.OrdinalIgnoreCase)) return Label(other, i);
            }
            return null;
        }

        public static string KeyName(InputAction action, int index)
        {
            switch (action.bindings[index].effectivePath)
            {
                case "<Keyboard>/leftArrow": return "←";
                case "<Keyboard>/rightArrow": return "→";
                case "<Keyboard>/upArrow": return "↑";
                case "<Keyboard>/downArrow": return "↓";
            }
            return action.GetBindingDisplayString(index);
        }

        // Keep translated key names for the original layout. Only changed commands need runtime keycap labels.
        private static string CommandKeys(string token)
        {
            string original = Loc.Get("keys.default_" + token);
            var map = Map;
            if (map == null) return original;
            var names = new List<string>();
            bool changed = false;
            void Read(string actionName, string part = null)
            {
                var action = map.FindAction(actionName);
                if (action == null) return;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (!IsKeyboard(binding) || (part != null && part != binding.name)) continue;
                    if (binding.overridePath != null) changed = true;
                    var name = KeyName(action, i);
                    if (!names.Contains(name)) names.Add(name);
                }
            }
            switch (token)
            {
                case "gas": Read("Throttle", "positive"); break;
                case "reverse": Read("Throttle", "negative"); break;
                case "steer": Read("Steer"); break;
                case "brake": Read("Brake"); break;
                case "reset": Read("Reset"); break;
                case "fire": Read("Fire"); break;
                case "secondary": Read("Secondary"); break;
                case "rear": Read("RearView"); break;
                case "one": Read("PrimaryCannon"); break;
                case "two": Read("PrimaryMachineGun"); break;
                case "slots":
                    var slots = new string[PrimaryWeaponCatalog.SlotsPerPage];
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var slot = map.FindAction(i == 0 ? "PrimaryCannon" : i == 1 ? "PrimaryMachineGun" : "PrimarySlot" + (i + 1));
                        if (slot == null) return original;
                        var alternatives = new List<string>();
                        for (int j = 0; j < slot.bindings.Count; j++)
                        {
                            var binding = slot.bindings[j];
                            if (!IsKeyboard(binding)) continue;
                            if (binding.overridePath != null) changed = true;
                            if (alternatives.Count == 0 || binding.overridePath != null) alternatives.Add(KeyName(slot, j));
                        }
                        slots[i] = string.Join("/", alternatives);
                    }
                    return changed ? string.Join(" · ", slots) : original;
            }
            return changed && names.Count > 0 ? string.Join(" / ", names) : original;
        }

        public static string Hint(string text)
        {
            int start = text.IndexOf("{bind.", StringComparison.Ordinal);
            if (start < 0) return text;
            string template = Loc.Language + text;
            if (hints.TryGetValue(template, out var known)) return known;
            while (start >= 0)
            {
                int end = text.IndexOf('}', start + 6);
                if (end < 0) break;
                string keys = CommandKeys(text.Substring(start + 6, end - start - 6));
                text = text.Remove(start, end - start + 1).Insert(start, keys);
                start = text.IndexOf("{bind.", start + keys.Length, StringComparison.Ordinal);
            }
            hints[template] = text;
            return text;
        }
    }
}
