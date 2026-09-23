using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace RacingGame.Tests
{
    public class InputActionAssetTests
    {
        private const string AssetPath = "Assets/InputSystem_Actions.inputactions";

        [Test]
        public void InputSystem_Actions_AssetFileExistsAndIsValidJson()
        {
            Assert.IsTrue(File.Exists(AssetPath), $"Expected input actions asset at {AssetPath}");
            string json = File.ReadAllText(AssetPath);
            Assert.IsFalse(string.IsNullOrEmpty(json));

            var asset = InputActionAsset.FromJson(json);
            Assert.IsNotNull(asset, "InputActionAsset.FromJson must successfully parse the asset.");
        }

        [Test]
        public void DrivingActionMap_ContainsSecondaryAction()
        {
            string json = File.ReadAllText(AssetPath);
            var asset = InputActionAsset.FromJson(json);
            var drivingMap = asset.FindActionMap("Driving");
            Assert.IsNotNull(drivingMap, "Action map 'Driving' must exist.");

            var secondary = drivingMap.FindAction("Secondary");
            Assert.IsNotNull(secondary, "Action 'Secondary' must exist in 'Driving' action map.");
            Assert.AreEqual(InputActionType.Button, secondary.type);
        }

        [Test]
        public void DrivingActionMap_SecondaryAction_HasRequiredBindings()
        {
            string json = File.ReadAllText(AssetPath);
            var asset = InputActionAsset.FromJson(json);
            var drivingMap = asset.FindActionMap("Driving");
            var secondary = drivingMap.FindAction("Secondary");

            var paths = secondary.bindings.Select(b => b.path).ToList();

            // Keyboard bindings
            Assert.IsTrue(paths.Contains("<Keyboard>/leftShift") || paths.Contains("<Keyboard>/shift"),
                "Secondary action must have Keyboard shift binding.");
            Assert.IsTrue(paths.Contains("<Keyboard>/e"),
                "Secondary action must have Keyboard E binding.");

            // Mouse binding
            Assert.IsTrue(paths.Contains("<Mouse>/rightButton"),
                "Secondary action must have Mouse rightButton binding.");

            // Gamepad / Touch binding (<Gamepad>/leftShoulder used by TouchControlsPresenter)
            Assert.IsTrue(paths.Contains("<Gamepad>/leftShoulder"),
                "Secondary action must have <Gamepad>/leftShoulder binding (matches TouchControlsPresenter).");
            Assert.IsTrue(paths.Contains("<Gamepad>/buttonEast"),
                "Secondary action must have <Gamepad>/buttonEast binding.");
        }
    }
}
