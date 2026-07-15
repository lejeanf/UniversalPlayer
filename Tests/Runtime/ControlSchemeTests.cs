using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Integration tests for the control-mode authority (BroadcastControlsStatus). The
    /// mode is now a LOGICAL flag (<see cref="BroadcastControlsStatus.controlScheme"/>) —
    /// input devices are never re-masked — so these assert the static, not
    /// PlayerInput.currentControlScheme. They cover auto-detect at launch and the
    /// Approach-A switching model (deliberate desktop input leaves VR; a debounced
    /// presence edge / controller button / Ctrl+Alt+V enters; Link-style runtimes don't
    /// re-lock). Exhaustive decision-table coverage is in <see cref="ControlModeArbiterTests"/>.
    /// </summary>
    public class ControlSchemeTests : InputTestFixture
    {
        private GameObject _playerGo;
        private PlayerInput _playerInput;
        private BroadcastControlsStatus _broadcaster;
        private InputActionAsset _actions;
        private DetectUserPresence.HmdState _fakeState;
        private readonly List<bool> _hmdEvents = new List<bool>();

        private static BroadcastControlsStatus.ControlScheme Mode => BroadcastControlsStatus.controlScheme;

        // Headset connected but OFF the head (presence supported, not present).
        private static DetectUserPresence.HmdState OffHead =>
            new DetectUserPresence.HmdState(hmdValid: true, presenceSupported: true, present: false, tracked: true);

        // Headset worn (presence reported).
        private static DetectUserPresence.HmdState Worn =>
            new DetectUserPresence.HmdState(hmdValid: true, presenceSupported: true, present: true, tracked: true);

        // Link-style runtime: userPresence NOT reported, tracking stuck true even off-head.
        private static DetectUserPresence.HmdState LinkNoPresence =>
            new DetectUserPresence.HmdState(hmdValid: true, presenceSupported: false, present: false, tracked: true);

        public override void Setup()
        {
            base.Setup(); // InputTestFixture: isolated input system state

            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            InputSystem.AddDevice<Gamepad>();
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

            _hmdEvents.Clear();
            PlayerEvents.HmdStateChanged += RecordHmdEvent;
            _fakeState = OffHead;

            // Isolate the mode-preference from real PlayerPrefs and from other tests.
            ControlModePreference.ExternalProvider = null;
            ControlModePreference.Clear();
        }

        public override void TearDown()
        {
            PlayerEvents.HmdStateChanged -= RecordHmdEvent;
            ControlModePreference.ExternalProvider = null;
            ControlModePreference.Clear();
            if (_playerGo != null) Object.Destroy(_playerGo);
            if (_actions != null) Object.Destroy(_actions);
            base.TearDown();
        }

        private void RecordHmdEvent(bool mounted) => _hmdEvents.Add(mounted);

        /// <summary>Creates and starts the player with the current fake HMD state, so start-in-mode is observable.</summary>
        private IEnumerator Begin()
        {
            _playerGo = new GameObject("ControlSchemeTestPlayer");
            _playerGo.SetActive(false);
            _playerInput = _playerGo.AddComponent<PlayerInput>();
            _playerInput.actions = _actions;
            _playerInput.defaultControlScheme = "Keyboard&Mouse";
            _playerInput.defaultActionMap = "Player";
            _playerInput.neverAutoSwitchControlSchemes = false;
            _playerInput.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

            _broadcaster = _playerGo.AddComponent<BroadcastControlsStatus>();
            _broadcaster.playerInput = _playerInput;
            _broadcaster.HmdStateProbe = () => _fakeState;
            SetField(_broadcaster, "hmdPollIntervalSeconds", 0.01f);
            SetField(_broadcaster, "switchCooldownSeconds", 0f); // no debounce in tests (they switch fast)

            _playerGo.SetActive(true);
            yield return null; // Awake/Start
            yield return WaitForPoll();
        }

        private static IEnumerator WaitForPoll() => WaitSeconds(0.05f);

        private static IEnumerator WaitSeconds(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        private IEnumerator PressKey(KeyControl key)
        {
            Press(key);
            yield return null;
            yield return null;
            Release(key);
            yield return null;
        }

        // ---- start-in-mode (auto-detection) -------------------------------------

        [UnityTest]
        public IEnumerator StartsInVr_WhenWornAtLaunch()
        {
            _fakeState = Worn;
            yield return Begin();

            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR),
                "Launching with the headset worn did not auto-detect VR.");
            Assert.That(_hmdEvents, Does.Contain(true));
        }

        [UnityTest]
        public IEnumerator StartsInVr_OverLink_WhenTrackedAtLaunch()
        {
            _fakeState = LinkNoPresence;
            yield return Begin();

            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR),
                "Launching worn over Link (tracked, no presence) should start in VR via the launch hint.");
        }

        [UnityTest]
        public IEnumerator StartsInDesktop_WhenOffHeadAtLaunch()
        {
            _fakeState = OffHead;
            yield return Begin();

            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "Launching with the headset off the head should start in Keyboard&Mouse.");
        }

        [UnityTest]
        public IEnumerator Startup_AlwaysRaisesInitialHmdState_EvenOnDesktop()
        {
            _fakeState = OffHead; // desktop launch
            yield return Begin();

            Assert.That(_hmdEvents, Is.Not.Empty,
                "The initial headset state must be broadcast at startup (desktop included) so channel listeners initialise.");
            Assert.That(_hmdEvents[0], Is.False, "Desktop launch should report not-in-VR.");
        }

        [UnityTest]
        public IEnumerator StartsInLastKnownDesktopScheme_Gamepad()
        {
            ControlModePreference.ExternalProvider = () => BroadcastControlsStatus.ControlScheme.Gamepad;
            _fakeState = OffHead;
            yield return Begin();

            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.Gamepad),
                "A desktop launch should restore the last-used desktop mode (Gamepad), not always default to Keyboard&Mouse.");
        }

        // ---- leaving VR ----------------------------------------------------------

        [UnityTest]
        public IEnumerator Worn_DeliberateKeyboard_ExitsVr_AndStays()
        {
            _fakeState = Worn;
            yield return Begin();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR), "Precondition: XR active.");

            yield return PressKey(Keyboard.current.wKey);
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "Deliberate keyboard input must leave VR (Approach A).");

            // Still worn, but no NEW presence rising edge → must not bounce back to XR.
            yield return WaitForPoll();
            yield return WaitForPoll();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "A continuously-worn headset must not yank the player back into VR after a deliberate exit.");
        }

        [UnityTest]
        public IEnumerator HeadsetOff_NoInput_StaysInCurrentScheme()
        {
            _fakeState = Worn;
            yield return Begin();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR), "Precondition: XR active.");

            _fakeState = OffHead; // taken off, but nothing touched yet
            yield return WaitForPoll();
            yield return WaitForPoll();

            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR),
                "Removing the headset must not blank the view until a desktop control is touched.");
        }

        [UnityTest]
        public IEnumerator HeadsetOff_ThenKeyboard_SwitchesToKbm()
        {
            _fakeState = Worn;
            yield return Begin();

            _fakeState = OffHead;
            yield return WaitForPoll();
            yield return PressKey(Keyboard.current.wKey);

            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "Headset off + keyboard input should switch to Keyboard&Mouse.");
        }

        [UnityTest]
        public IEnumerator HeadsetOff_ThenGamepad_SwitchesToGamepad()
        {
            _fakeState = Worn;
            yield return Begin();

            _fakeState = OffHead;
            yield return WaitForPoll();
            Press(Gamepad.current.buttonSouth);
            yield return null;
            yield return null;
            Release(Gamepad.current.buttonSouth);

            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.Gamepad),
                "Headset off + gamepad input should switch to Gamepad.");
        }

        // ---- the Link regression: no re-lock, and Ctrl+Alt+V returns -------------

        [UnityTest]
        public IEnumerator LinkRegime_LeaveVr_StaysDesktop_NoReLock()
        {
            _fakeState = LinkNoPresence;
            yield return Begin();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR), "Precondition: launched in VR over Link.");

            yield return PressKey(Keyboard.current.wKey);
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse), "Keyboard must leave VR.");

            for (var i = 0; i < 3; i++) yield return WaitForPoll();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "Stuck isTracked must never re-lock the player into VR over Link.");
        }

        [UnityTest]
        public IEnumerator LinkRegime_CtrlAltV_ReEntersVr()
        {
            _fakeState = LinkNoPresence;
            yield return Begin();
            yield return PressKey(Keyboard.current.wKey); // leave VR
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse), "Precondition: on desktop.");

            // Ctrl+Alt+V — the reliable re-entry when the runtime reports no presence edge.
            Press(Keyboard.current.leftCtrlKey);
            Press(Keyboard.current.leftAltKey);
            yield return null;
            Press(Keyboard.current.vKey);
            yield return null;
            yield return null;
            Release(Keyboard.current.vKey);
            Release(Keyboard.current.leftAltKey);
            Release(Keyboard.current.leftCtrlKey);

            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR),
                "Ctrl+Alt+V must re-enter VR over a no-presence runtime.");
        }

        // ---- desktop <-> desktop -------------------------------------------------

        [UnityTest]
        public IEnumerator Desktop_KbmToGamepadAndBack()
        {
            _fakeState = OffHead;
            yield return Begin();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse), "Precondition: KBM.");

            Press(Gamepad.current.buttonSouth);
            yield return null;
            yield return null;
            Release(Gamepad.current.buttonSouth);
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.Gamepad),
                "Gamepad input in KBM must switch to Gamepad.");

            yield return PressKey(Keyboard.current.wKey);
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "Keyboard input must take the mode back from Gamepad.");
        }

        [UnityTest]
        public IEnumerator HmdOnOffOn_KeepsWorkingBothWays()
        {
            _fakeState = OffHead;
            yield return Begin();

            for (var cycle = 0; cycle < 2; cycle++)
            {
                _fakeState = Worn; // a real presence rising edge (debounced) re-enters VR
                yield return WaitForPoll();
                Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR),
                    $"Cycle {cycle}: putting the headset on (presence edge) should switch to XR.");

                _fakeState = OffHead;
                yield return WaitForPoll();
                yield return PressKey(Keyboard.current.wKey);
                Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                    $"Cycle {cycle}: headset off + keyboard should return to Keyboard&Mouse.");
            }
        }

        // ---- force-desktop failsafe ----------------------------------------------

        [UnityTest]
        public IEnumerator ForceDesktopControls_DropsToKbm_AndStaysWhileWorn()
        {
            _fakeState = Worn;
            yield return Begin();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR), "Precondition: XR active.");

            _broadcaster.ForceDesktopControls("battery critical (test)");
            yield return null;
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "ForceDesktopControls must drop to Keyboard&Mouse even while worn.");

            yield return WaitForPoll();
            yield return WaitForPoll();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "The force-desktop drop must hold while the headset stays continuously worn.");
        }

        // ---- freecam sub-mode ----------------------------------------------------

        [UnityTest]
        public IEnumerator SetFreecam_EntersAndExits_LogicalSubMode()
        {
            _fakeState = OffHead;
            yield return Begin();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse), "Precondition: KBM.");

            _broadcaster.SetFreecam(true, BroadcastControlsStatus.ControlScheme.KeyboardMouse);
            yield return null;
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.Freecam),
                "SetFreecam(true) must enter the FreeCam sub-mode, and arbitration must leave it alone.");

            yield return WaitForPoll();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.Freecam),
                "FreeCam must persist — arbitration does not override it.");

            _broadcaster.SetFreecam(false, BroadcastControlsStatus.ControlScheme.KeyboardMouse);
            yield return null;
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse),
                "SetFreecam(false) must restore the desktop mode.");
        }

        // ---- broadcast dedup (no over-firing) ------------------------------------

        [UnityTest]
        public IEnumerator SwitchingMode_BroadcastsOncePerChange()
        {
            _fakeState = Worn;
            yield return Begin();
            Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.XR), "Precondition: XR active.");

            var count = 0;
            BroadcastControlsStatus.SendControlScheme += Counter;
            try
            {
                _fakeState = OffHead;
                yield return WaitForPoll();
                yield return PressKey(Keyboard.current.wKey);

                Assert.That(Mode, Is.EqualTo(BroadcastControlsStatus.ControlScheme.KeyboardMouse), "Precondition: switched to KBM.");
                Assert.That(count, Is.EqualTo(1),
                    "The XR->Keyboard&Mouse switch fired SendControlScheme " + count + " times; it must fire exactly once.");
            }
            finally
            {
                BroadcastControlsStatus.SendControlScheme -= Counter;
            }

            void Counter(BroadcastControlsStatus.ControlScheme _) => count++;
        }

        // ---- pure worn-signal helpers --------------------------------------------

        [Test]
        public void WornForEntry_IsPresenceOnly_NeverTracked()
        {
            Assert.That(BroadcastControlsStatus.WornForEntry(
                new DetectUserPresence.HmdState(true, true, true, false)), Is.True,
                "Presence supported + present is worn.");
            Assert.That(BroadcastControlsStatus.WornForEntry(
                new DetectUserPresence.HmdState(true, true, false, true)), Is.False,
                "Presence supported + not present is not worn (tracked ignored).");
            Assert.That(BroadcastControlsStatus.WornForEntry(
                new DetectUserPresence.HmdState(true, false, false, true)), Is.False,
                "The Link fix: presence unsupported + tracked must NOT count as worn at runtime.");
            Assert.That(BroadcastControlsStatus.WornForEntry(
                new DetectUserPresence.HmdState(false, true, true, true)), Is.False,
                "An invalid HMD is never worn.");
        }

        [Test]
        public void ComputeWornAtLaunch_UsesPresence_ThenTracking()
        {
            Assert.That(BroadcastControlsStatus.ComputeWornAtLaunch(
                new DetectUserPresence.HmdState(true, true, true, false)), Is.True);
            Assert.That(BroadcastControlsStatus.ComputeWornAtLaunch(
                new DetectUserPresence.HmdState(true, true, false, true)), Is.False,
                "Presence supported + not present ignores tracked at launch.");
            Assert.That(BroadcastControlsStatus.ComputeWornAtLaunch(
                new DetectUserPresence.HmdState(true, false, false, true)), Is.True,
                "Presence unsupported falls back to tracking for the ONE launch decision.");
            Assert.That(BroadcastControlsStatus.ComputeWornAtLaunch(
                new DetectUserPresence.HmdState(true, false, false, false)), Is.False);
            Assert.That(BroadcastControlsStatus.ComputeWornAtLaunch(
                new DetectUserPresence.HmdState(false, false, false, true)), Is.False,
                "An invalid HMD is never worn.");
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
