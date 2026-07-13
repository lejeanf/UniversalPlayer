using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using jeanf.EventSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Regression tests for the control scheme switching bug: entering VR used to set
    /// neverAutoSwitchControlSchemes permanently, so VR -> keyboard never switched back.
    /// Uses fake input devices and a fake HMD-presence probe — no headset needed.
    /// </summary>
    public class ControlSchemeTests : InputTestFixture
    {
        private GameObject _playerGo;
        private PlayerInput _playerInput;
        private BroadcastControlsStatus _broadcaster;
        private InputActionAsset _actions;
        private bool _fakeHmdMounted;
        private readonly List<bool> _hmdEvents = new List<bool>();

        [UnitySetUp]
        public IEnumerator UnitySetUp()
        {
            base.Setup(); // InputTestFixture: isolated input system state

            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            try { InputSystem.AddDevice<XRHMD>(); }
            catch
            {
                InputSystem.RegisterLayout<XRHMD>();
                InputSystem.AddDevice<XRHMD>();
            }

            _actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = _actions.AddActionMap("Player");
            var action = map.AddAction("probe", binding: "<Keyboard>/space");
            action.AddBinding("<XRHMD>/devicePosition");
            _actions.AddControlScheme("Keyboard&Mouse").WithRequiredDevice("<Keyboard>").WithOptionalDevice("<Mouse>");
            _actions.AddControlScheme("XR").WithRequiredDevice("<XRHMD>");
            _actions.AddControlScheme("Gamepad").WithRequiredDevice("<Gamepad>");
            InputSystem.AddDevice<Gamepad>();

            _hmdEvents.Clear();
            PlayerEvents.HmdStateChanged += RecordHmdEvent;

            _fakeHmdMounted = false;
            _playerGo = new GameObject("ControlSchemeTestPlayer");
            _playerGo.SetActive(false);
            _playerInput = _playerGo.AddComponent<PlayerInput>();
            _playerInput.actions = _actions;
            _playerInput.defaultControlScheme = "Keyboard&Mouse";
            _playerInput.neverAutoSwitchControlSchemes = false;
            _playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            _broadcaster = _playerGo.AddComponent<BroadcastControlsStatus>();
            _broadcaster.playerInput = _playerInput;
            _broadcaster.HmdMountedProbe = () => _fakeHmdMounted;
            SetField(_broadcaster, "hmdPollIntervalSeconds", 0.01f);

            _playerGo.SetActive(true);
            yield return null; // Awake/Start
        }

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            PlayerEvents.HmdStateChanged -= RecordHmdEvent;
            Object.Destroy(_playerGo);
            Object.Destroy(_actions);
            yield return null;
            base.TearDown();
        }

        private void RecordHmdEvent(bool mounted) => _hmdEvents.Add(mounted);

        private IEnumerator WaitForPoll() => WaitSeconds(0.1f);

        private static IEnumerator WaitSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        [UnityTest]
        public IEnumerator PuttingHmdOn_SwitchesToXr_LocksScheme_AndRaisesChannel()
        {
            _fakeHmdMounted = true;
            yield return WaitForPoll();

            Assert.That(_playerInput.currentControlScheme, Is.EqualTo("XR"),
                "Putting the headset on did not switch PlayerInput to the XR scheme.");
            Assert.That(BroadcastControlsStatus.controlScheme, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR),
                "The static controlScheme did not follow the switch to XR — listeners (hands, cursor) won't react.");
            Assert.That(_playerInput.neverAutoSwitchControlSchemes, Is.True,
                "While the headset is worn, auto-switching must be locked so idle mouse/keyboard input " +
                "cannot yank the player out of VR.");
            Assert.That(_hmdEvents, Does.Contain(true),
                "PlayerEvents.HmdStateChanged was not raised with 'true' when the headset was put on.");
        }

        [UnityTest]
        public IEnumerator RemovingHmd_SwitchesBackToKeyboard_TheOldLatchBug()
        {
            _fakeHmdMounted = true;
            yield return WaitForPoll();
            Assert.That(_playerInput.currentControlScheme, Is.EqualTo("XR"), "Precondition failed: XR scheme not active.");

            _fakeHmdMounted = false;
            yield return WaitForPoll();

            Assert.That(_playerInput.currentControlScheme, Is.EqualTo("Keyboard&Mouse"),
                "THE VR->KEYBOARD BUG IS BACK: removing the headset did not switch back to Keyboard&Mouse. " +
                "Most likely neverAutoSwitchControlSchemes is latched permanently again (BroadcastControlsStatus).");
            Assert.That(_playerInput.neverAutoSwitchControlSchemes, Is.False,
                "After removing the headset, auto-switching must be re-enabled so gamepad/other devices work.");
            Assert.That(_hmdEvents, Does.Contain(false),
                "PlayerEvents.HmdStateChanged was not raised with 'false' when the headset was removed.");
            Assert.That(BroadcastControlsStatus.controlScheme, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "The static controlScheme did not follow the switch back to Keyboard&Mouse.");
        }

        [UnityTest]
        public IEnumerator StuckPresenceSensor_DesktopInput_TakesControlBack()
        {
            // A headset resting on a desk often reports 'user present' forever (light on
            // the proximity sensor, Link idling). That must NEVER lock out the desktop:
            // after the donning grace period, real keyboard/mouse input wins.
            SetField(_broadcaster, "xrInputGraceSeconds", 0.05f);
            _fakeHmdMounted = true; // and it never goes false again — the stuck sensor
            yield return WaitForPoll();
            Assert.That(_playerInput.currentControlScheme, Is.EqualTo("XR"), "Precondition failed: XR scheme not active.");

            yield return WaitSeconds(0.15f); // let the donning grace expire
            Press(Keyboard.current.wKey);
            yield return null;
            yield return null;

            Assert.That(_playerInput.currentControlScheme, Is.EqualTo("Keyboard&Mouse"),
                "THE VR-LOCKOUT BUG IS BACK: with the presence sensor stuck on 'mounted', pressing a key " +
                "did not hand control back to the keyboard — desktop players are locked out whenever a " +
                "headset is plugged in (BroadcastControlsStatus desktop-input arbitration).");
            Assert.That(BroadcastControlsStatus.controlScheme, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "The static controlScheme did not follow the desktop takeover.");
            Assert.That(_playerInput.neverAutoSwitchControlSchemes, Is.True,
                "While presence still claims 'mounted', auto-switching must stay blocked — otherwise the " +
                "continuously-streaming HMD data yanks the scheme straight back to XR.");
            Release(Keyboard.current.wKey);
        }

        [UnityTest]
        public IEnumerator PluggedHeadsetLatch_GamepadStillSwitchesFromKeyboard()
        {
            // With a desk headset keeping presence 'mounted', the auto-switch latch stays
            // on and PlayerInput's native KBM<->Gamepad switching is blocked — the
            // arbitration must handle desktop<->desktop swaps manually.
            SetField(_broadcaster, "xrInputGraceSeconds", 0.05f);
            _fakeHmdMounted = true; // stuck sensor
            yield return WaitForPoll();
            yield return WaitSeconds(0.15f);
            Press(Keyboard.current.wKey);
            yield return null;
            yield return null;
            Release(Keyboard.current.wKey);
            Assert.That(_playerInput.currentControlScheme, Is.EqualTo("Keyboard&Mouse"), "Precondition failed: desktop takeover.");

            Press(Gamepad.current.buttonSouth);
            yield return null;
            yield return null;

            Assert.That(_playerInput.currentControlScheme, Is.EqualTo("Gamepad"),
                "Pressing a gamepad button in Keyboard&Mouse (with the headset latch active) did not switch to " +
                "the Gamepad scheme — gamepads are unusable whenever a headset is plugged in.");
            Release(Gamepad.current.buttonSouth);

            Press(Keyboard.current.wKey);
            yield return null;
            yield return null;
            Assert.That(_playerInput.currentControlScheme, Is.EqualTo("Keyboard&Mouse"),
                "Keyboard input did not take the scheme back from Gamepad.");
            Release(Keyboard.current.wKey);
        }

        [UnityTest]
        public IEnumerator HmdOnOffOn_KeepsWorkingBothWays()
        {
            for (var cycle = 0; cycle < 2; cycle++)
            {
                _fakeHmdMounted = true;
                yield return WaitForPoll();
                Assert.That(_playerInput.currentControlScheme, Is.EqualTo("XR"),
                    $"Cycle {cycle}: putting the headset back on no longer switches to XR.");

                _fakeHmdMounted = false;
                yield return WaitForPoll();
                Assert.That(_playerInput.currentControlScheme, Is.EqualTo("Keyboard&Mouse"),
                    $"Cycle {cycle}: removing the headset no longer switches back to Keyboard&Mouse.");
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                $"Field '{fieldName}' not found on {target.GetType().Name} — it was renamed; update ControlSchemeTests alongside the refactor.");
            field.SetValue(target, value);
        }
    }
}
