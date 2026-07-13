using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Guards the user-facing FPS bindings the docs and tutorials promise: an action
    /// that quietly loses its binding (or lands in the wrong control-scheme group)
    /// fails here instead of at playtest time.
    /// </summary>
    public class InputBindingsTests
    {
        private InputActionAsset _asset;

        [OneTimeSetUp]
        public void LoadAsset()
        {
            var path = $"{PackagePaths.Runtime}/InputActions/UniversalPlayer_InputActions.inputactions";
            _asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            Assert.That(_asset, Is.Not.Null,
                $"UniversalPlayer_InputActions not found at '{path}'. If the asset moved, update this test and the Player prefab.");
        }

        [TestCase("ToggleMap", "<Keyboard>/m", "<Gamepad>/dpad/left")]
        [TestCase("ToggleInventory", "<Keyboard>/i", "<Gamepad>/dpad/right")]
        [TestCase("DrawSecondaryItem", "<Keyboard>/2", "<Gamepad>/dpad/down")]
        public void FpsAction_HasKeyboardAndGamepadBindings(string actionName, string keyboardPath, string gamepadPath)
        {
            var action = _asset.FindAction($"FPS/{actionName}", throwIfNotFound: false);
            Assert.That(action, Is.Not.Null,
                $"No '{actionName}' action in the FPS map — UiToggleInput/PrimaryItemController resolve it by name, " +
                "so renaming it silently disables the feature. Rename it back or update every FindAction call.");

            foreach (var expectedPath in new[] { keyboardPath, gamepadPath })
            {
                Assert.That(action.bindings.Any(b => b.path == expectedPath), Is.True,
                    $"'{actionName}' lost its '{expectedPath}' binding — that control does nothing on the affected device.");
            }
        }
    }
}
