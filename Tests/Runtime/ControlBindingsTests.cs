#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// End-to-end binding checks per input mode: fake keyboard/gamepad devices press
    /// the promised controls against the PACKAGED input asset with the matching
    /// control-scheme binding mask, and the bound action must fire. A binding that
    /// silently moves, loses its group, or gets consumed by a chord fails here
    /// instead of in someone's playtest. (XR bindings need real XR devices and are
    /// covered by the on-hardware checklist instead.)
    /// </summary>
    public class ControlBindingsTests
    {
        private InputActionAsset _asset;
        private InputActionMap _fps;
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("UniversalPlayer_InputActions t:InputActionAsset");
            Assert.That(guids, Is.Not.Empty,
                "UniversalPlayer_InputActions not found — if the asset was renamed, update this test, the Player prefab and the README.");
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));

            // A clone: enabling maps and setting binding masks must never dirty the packaged asset.
            _asset = Object.Instantiate(source);
            _fps = _asset.FindActionMap("FPS", throwIfNotFound: true);

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _fps.Disable();
            InputSystem.RemoveDevice(_keyboard);
            InputSystem.RemoveDevice(_gamepad);
            Object.Destroy(_asset);
            yield return null;
        }

        private void UseScheme(string bindingGroup)
        {
            _fps.Disable();
            _fps.bindingMask = InputBinding.MaskByGroup(bindingGroup);
            _fps.Enable();
        }

        private bool Fires(string actionName, System.Action press)
        {
            var action = _fps.FindAction(actionName);
            Assert.That(action, Is.Not.Null,
                $"No '{actionName}' action in the FPS map — components resolve it by name; renaming silently disables the feature.");
            var fired = false;
            void Handler(InputAction.CallbackContext _) => fired = true;
            action.performed += Handler;
            press();
            InputSystem.Update();
            action.performed -= Handler;
            ReleaseEverything();
            return fired;
        }

        private void ReleaseEverything()
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            InputSystem.Update();
        }

        private void PressKeys(params Key[] keys) => InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));

        private void PressButtons(params GamepadButton[] buttons)
        {
            var state = new GamepadState();
            foreach (var button in buttons) state = state.WithButton(button);
            InputSystem.QueueStateEvent(_gamepad, state);
        }

        // ---- Keyboard & Mouse ----

        [UnityTest]
        public IEnumerator KeyboardMouse_EveryPromisedKey_FiresItsAction()
        {
            UseScheme("Keyboard&Mouse");
            foreach (var (action, key) in new[]
                     {
                         ("Crouch", Key.C), ("Jump", Key.Space), ("Sprint", Key.LeftShift),
                         ("ToggleMap", Key.M), ("ToggleInventory", Key.I),
                         ("MainMenu", Key.Escape), ("PauseGame", Key.P), ("Interact", Key.E),
                         ("DrawPrimaryItem", Key.Digit1), ("DrawSecondaryItem", Key.Digit2),
                     })
            {
                Assert.That(Fires(action, () => PressKeys(key)), Is.True,
                    $"'{key}' no longer triggers '{action}' in Keyboard&Mouse — the binding moved, lost its group, or is consumed by a composite.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator KeyboardMouse_W_Moves()
        {
            UseScheme("Keyboard&Mouse");
            Assert.That(Fires("Move", () => PressKeys(Key.W)), Is.True, "'W' no longer moves the player in Keyboard&Mouse.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator KeyboardMouse_CtrlShiftC_TogglesFreecam_AndPlainC_StillCrouches()
        {
            UseScheme("Keyboard&Mouse");

            Assert.That(Fires("ActivateFreeCam", () =>
            {
                PressKeys(Key.LeftCtrl, Key.LeftShift);
                InputSystem.Update();
                PressKeys(Key.LeftCtrl, Key.LeftShift, Key.C);
            }), Is.True, "Ctrl+Shift+C no longer toggles the free camera.");

            // The chord shares the C key with Crouch: a consumption regression here is
            // exactly the kind of silent breakage this suite exists for.
            Assert.That(Fires("Crouch", () => PressKeys(Key.C)), Is.True,
                "Plain 'C' no longer crouches — is the freecam chord consuming the key?");
            yield return null;
        }

        // ---- Gamepad ----

        [UnityTest]
        public IEnumerator Gamepad_EveryPromisedButton_FiresItsAction()
        {
            UseScheme("Gamepad");
            foreach (var (action, button) in new[]
                     {
                         ("Crouch", GamepadButton.RightStick), ("Jump", GamepadButton.North),
                         ("Sprint", GamepadButton.LeftStick),
                         ("Interact", GamepadButton.South), ("TakeObject", GamepadButton.South),
                         ("DrawPrimaryItem", GamepadButton.DpadUp), ("DrawSecondaryItem", GamepadButton.DpadDown),
                         ("ToggleMap", GamepadButton.DpadLeft), ("ToggleInventory", GamepadButton.DpadRight),
                         ("MainMenu", GamepadButton.Start), ("PauseGame", GamepadButton.Select),
                     })
            {
                Assert.That(Fires(action, () => PressButtons(button)), Is.True,
                    $"Gamepad '{button}' no longer triggers '{action}' — the binding moved, lost its group, or is consumed by a chord.");
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gamepad_Sticks_MoveAndLook()
        {
            UseScheme("Gamepad");
            Assert.That(Fires("Move", () => InputSystem.QueueStateEvent(_gamepad, new GamepadState { leftStick = new Vector2(0f, 1f) })),
                Is.True, "The left stick no longer moves the player in Gamepad mode.");
            Assert.That(Fires("LookAround", () => InputSystem.QueueStateEvent(_gamepad, new GamepadState { rightStick = new Vector2(1f, 0f) })),
                Is.True, "The right stick no longer looks around in Gamepad mode (this regressed once via control-scheme device lists).");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Gamepad_DpadDownPlusY_TogglesFreecam()
        {
            UseScheme("Gamepad");
            Assert.That(Fires("ActivateFreeCam", () =>
            {
                PressButtons(GamepadButton.DpadDown);
                InputSystem.Update();
                PressButtons(GamepadButton.DpadDown, GamepadButton.North);
            }), Is.True, "Hold dpad-down + Y no longer toggles the free camera on gamepad.");
            yield return null;
        }

        // ---- FreeCam ----

        [UnityTest]
        public IEnumerator Freecam_FliesWithBothInputFamilies_AndBothExitsWork()
        {
            UseScheme("FreeCam");

            Assert.That(Fires("Move", () => PressKeys(Key.W)), Is.True, "FreeCam lost keyboard movement (W).");
            Assert.That(Fires("Elevate", () => PressKeys(Key.E)), Is.True, "FreeCam lost keyboard elevation (E).");
            Assert.That(Fires("Move", () => InputSystem.QueueStateEvent(_gamepad, new GamepadState { leftStick = new Vector2(0f, 1f) })),
                Is.True, "FreeCam lost gamepad movement (left stick).");
            Assert.That(Fires("LookAround", () => InputSystem.QueueStateEvent(_gamepad, new GamepadState { rightStick = new Vector2(1f, 0f) })),
                Is.True, "FreeCam lost gamepad look (right stick).");
            Assert.That(Fires("Elevate", () => InputSystem.QueueStateEvent(_gamepad, new GamepadState { rightTrigger = 1f })),
                Is.True, "FreeCam lost gamepad elevation (right trigger).");

            Assert.That(Fires("ActivateFreeCam", () =>
            {
                PressKeys(Key.LeftCtrl, Key.LeftShift);
                InputSystem.Update();
                PressKeys(Key.LeftCtrl, Key.LeftShift, Key.C);
            }), Is.True, "Ctrl+Shift+C cannot EXIT the free camera (keyboard).");

            Assert.That(Fires("ActivateFreeCam", () =>
            {
                PressButtons(GamepadButton.DpadDown);
                InputSystem.Update();
                PressButtons(GamepadButton.DpadDown, GamepadButton.North);
            }), Is.True, "Dpad-down + Y cannot EXIT the free camera (gamepad).");
            yield return null;
        }

        // ---- Scheme hygiene ----

        [Test]
        public void FreecamScheme_HasNoGamepadDevice_SoAutoSwitchCannotHijackIt()
        {
            // Regression guard: listing the gamepad among FreeCam's devices made Unity's
            // native auto-switch prefer FreeCam over Gamepad on any stick input, killing
            // right-stick look. FreeCam is toggle-only; devices are paired explicitly.
            var found = false;
            foreach (var scheme in _asset.controlSchemes)
            {
                if (scheme.name != "FreeCam") continue;
                found = true;
                foreach (var requirement in scheme.deviceRequirements)
                {
                    Assert.That(requirement.controlPath, Does.Not.Contain("Gamepad"),
                        "The FreeCam control scheme lists a Gamepad device again — Unity's auto-switch will steal gamepad input from the Gamepad scheme (broken right-stick look).");
                }
            }
            Assert.That(found, Is.True, "No 'FreeCam' control scheme in the input asset — was it renamed?");
        }
    }
}
#endif
