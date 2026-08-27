using jeanf.EventSystem;
using jeanf.validationTools;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Single authority for the player's control mode (VR / Keyboard&amp;Mouse /
    /// Gamepad). It auto-detects the mode at launch, arbitrates switches at runtime,
    /// and broadcasts the result on <see cref="SendControlScheme"/> so the rest of the
    /// player (hands, camera, gaze rig, cursor) reacts.
    ///
    /// <b>Decoupled input model.</b> The mode is a purely LOGICAL flag — it does NOT
    /// re-mask input devices. At startup every device (present AND future) and every
    /// binding group is made live at once (<c>actions.devices = null</c>,
    /// <c>bindingMask = null</c>); nothing is ever re-paired or re-masked on a mode
    /// change. This removes the device-re-pair race that made action bindings stall on
    /// a switch — the cause of "walking dead after VR" and "hands don't track" — because
    /// the keyboard is ALWAYS bound and the XR controllers (input + TrackedPoseDriver
    /// pose actions) are ALWAYS bound. The two device families coexist harmlessly: each
    /// mode's logic ignores the other's input (PlayerMovement's XR branch ignores WASD,
    /// the camera skips mouse-look in XR, XR locomotion/rays are gated off on desktop,
    /// and desktop-hidden hands simply are not shown by HandsDisplayer).
    ///
    /// The decision logic lives in the pure <see cref="ControlModeArbiter"/> (unit
    /// tested); this component reads devices, polls the headset, and applies the
    /// arbiter's chosen mode via <see cref="SetMode"/>. Model (Approach A):
    ///  • <b>Enter VR</b> on a debounced presence rising edge, an XR controller button,
    ///    or Ctrl+Alt+V. <c>isTracked</c> is a one-time launch hint only (over Link it
    ///    stays true off-head and would latch VR forever).
    ///  • <b>Leave VR</b> on deliberate desktop input, or Ctrl+Alt+K /
    ///    <see cref="ForceDesktopControls"/>.
    ///  • <b>Desktop</b> follows the most recent desktop input; idle keeps the mode.
    ///
    /// FreeCam is a desktop sub-mode owned by PlayerMovement, entered via
    /// <see cref="SetFreecam"/>; arbitration leaves it alone.
    /// </summary>
    public class BroadcastControlsStatus : MonoBehaviour, IDebugBehaviour
    {
        public delegate void SendControlSchemeDelegate(ControlScheme controlScheme);
        public static SendControlSchemeDelegate SendControlScheme;

        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        public enum ControlScheme
        {
            KeyboardMouse,
            XR,
            Gamepad,
            Freecam
        }

        public static ControlScheme controlScheme;

        [Validation("PlayerInput is required — control-mode detection and the shared input asset read from it.")]
        [SerializeField] public PlayerInput playerInput;

        [Header("HMD presence")]
        [Tooltip("How often (seconds) the headset presence sensor is polled.")]
        [SerializeField] private float hmdPollIntervalSeconds = 0.25f;
        [Tooltip("Consecutive 'worn' polls required before VR auto-engages on a presence rising edge — debounces a single proximity-sensor flicker.")]
        [SerializeField] private int wornStablePolls = 2;

        [Header("Leaving VR (deliberate desktop input)")]
        [Tooltip("Square of the mouse-move (pixels) that counts as a deliberate 'leave VR' gesture. Larger than the desktop-arbitration threshold so a desk bump does not kick you out of VR.")]
        [SerializeField] private float exitVrMouseDeltaSqr = 400f;

        [Header("Switch debounce")]
        [Tooltip("After any mode switch, ignore new switch triggers for this long — prevents VR<->desktop oscillation (e.g. controller drift vs. mouse movement fighting each other).")]
        [SerializeField] private float switchCooldownSeconds = 0.4f;

        [Header("Force-keyboard escape hatch (Ctrl+Alt+K)")]
        [SerializeField] private bool forceKeyboardRequiresCtrl = true;
        [SerializeField] private bool forceKeyboardRequiresAlt = true;
        [SerializeField] private Key forceKeyboardKey = Key.K;

        [Header("Force-VR hotkey (Ctrl+Alt+V)")]
        [Tooltip("Requests VR entry even when the runtime never reports a presence rising edge (e.g. userPresence unsupported over Link).")]
        [SerializeField] private bool forceVrRequiresCtrl = true;
        [SerializeField] private bool forceVrRequiresAlt = true;
        [SerializeField] private Key forceVrKey = Key.V;

        /// <summary>
        /// Test seam: how the head-device state is read. Defaults to the real
        /// device (<see cref="DetectUserPresence.ReadState"/>); tests inject a fake.
        /// </summary>
        public Func<DetectUserPresence.HmdState> HmdStateProbe = DetectUserPresence.ReadState;

        private ControlModeArbiter _arbiter;
        private float _switchCooldownUntil;
        private float _nextHmdPoll;
        private bool _hmdValid;
        private bool _warnedNoSignal;
        private bool _lastRaisedInVr;
        // Over-fire guard: SendControlScheme is broadcast once on the first resolve
        // and thereafter only when the mode actually changes.
        private bool _hasBroadcast;
        // Gate persistence until after the startup restore, so the initial resolve
        // cannot overwrite the stored preference before we read it.
        private bool _started;

        /// <summary>Diagnostics: true while the player is logically in VR.</summary>
        public bool InVr => _arbiter != null && _arbiter.InVr;

        private void Awake()
        {
            _arbiter = new ControlModeArbiter(wornStablePolls);

            if (playerInput == null)
            {
                Debug.LogError($"{XrStartupDiagnostics.LogPrefix} BroadcastControlsStatus on '{name}': playerInput is not assigned — " +
                    "control-mode detection is disabled. Assign it on the Player prefab variant.", this);
                return;
            }
            // We never hand the scheme to PlayerInput's native auto-switch (streaming HMD
            // data must not self-enter XR), and we never call SwitchCurrentControlScheme
            // either — the mode is logical (see DecoupleInput / SetMode).
            playerInput.neverAutoSwitchControlSchemes = true;
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        // Re-assert the decoupled state whenever a device appears, so modes that were not
        // available at launch (a gamepad plugged in, the headset connected later) work
        // immediately — with no re-masking.
        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected
                || change == InputDeviceChange.Enabled)
                DecoupleInput();
        }

        /// <summary>
        /// Makes every device (present and future) and every binding group live at once,
        /// so no action ever needs to re-bind on a mode change. This is the whole point of
        /// the decoupled model. Called after PlayerInput has finished its own startup
        /// (which would otherwise leave a Keyboard&amp;Mouse device mask in place) and
        /// re-asserted on device changes.
        /// </summary>
        private void DecoupleInput()
        {
            if (playerInput == null || playerInput.actions == null) return;
            playerInput.actions.devices = null;     // all devices, present and future
            playerInput.actions.bindingMask = null; // all binding groups (KBM + Gamepad + XR + FreeCam)
        }

        private void Start()
        {
            if (playerInput == null) return;

            // Runs after every component's OnEnable, so PlayerInput has already activated
            // its default scheme; override that here to remove the device mask for good.
            DecoupleInput();

            var state = HmdStateProbe();
            WarnIfNoPresenceSignal(state);
            _hmdValid = state.HmdValid;

            // Launch is the ONE decision allowed to use isTracked: at startup the user's
            // intent is ambiguous and "put the headset on to play" is a fine default; if
            // wrong, grabbing the keyboard self-corrects. After launch, tracked is ignored.
            var wornAtLaunch = ComputeWornAtLaunch(state);
            _arbiter.SeedLaunch(wornAtLaunch);

            _lastRaisedInVr = wornAtLaunch;
            // Always emit the initial headset state so channel listeners that initialise
            // from the first event get a value (desktop launch included).
            PlayerEvents.RaiseHmdState(wornAtLaunch);

            SetMode(wornAtLaunch ? ControlScheme.XR : RestorePreferredDesktopMode());

            // From here on, genuine runtime switches are remembered (see Save gate).
            _started = true;
        }

        /// <summary>Starts desktop sessions in the last-used mode (Keyboard&amp;Mouse vs Gamepad) instead of always defaulting to Keyboard&amp;Mouse.</summary>
        private ControlScheme RestorePreferredDesktopMode()
        {
            var preferred = ControlModePreference.Load();
            return preferred == ControlScheme.Gamepad && Gamepad.current != null
                ? ControlScheme.Gamepad
                : ControlScheme.KeyboardMouse;
        }

        private void Update()
        {
            if (playerInput == null) return;

            // After any switch, hold off new switch triggers briefly so VR and desktop
            // can't ping-pong (controller drift vs. mouse movement fighting each other).
            var inSwitchCooldown = Time.unscaledTime < _switchCooldownUntil;

            // Re-enter VR on Ctrl+Alt+V OR by pressing an XR controller BUTTON (trigger/
            // grip/face/stick click) — a discrete press, never an analog value, so a
            // resting/drifting controller can't continuously yank the player into VR.
            // Only consulted on desktop, so using the controllers in VR never blocks exit.
            // The explicit chords BYPASS the switch cooldown: the cooldown exists to keep
            // ambient input (controller drift vs. mouse bumps) from ping-ponging the mode,
            // but Ctrl+Alt+V / Ctrl+Alt+K are unambiguous intent — a V-press right after
            // a keyboard-driven VR exit used to land inside the cooldown and be swallowed.
            // Always evaluated, so the rising-edge latches also stay fresh.
            var forceVr = ForceVrComboPressed();
            var forceKbm = ForceKeyboardComboPressed();
            var vrEntryRequested = forceVr
                || (!inSwitchCooldown && !_arbiter.InVr && XrControllerButtonPressedThisFrame());

            if (Time.unscaledTime >= _nextHmdPoll)
            {
                _nextHmdPoll = Time.unscaledTime + hmdPollIntervalSeconds;
                var state = HmdStateProbe();
                WarnIfNoPresenceSignal(state);
                _hmdValid = state.HmdValid;
                _arbiter.NotifyWornPoll(WornForEntry(state)); // presence-only; tracked never re-enters VR
            }

            var isFreecam = controlScheme == ControlScheme.Freecam;
            var deliberateExit = forceKbm ? ControlModeArbiter.DesktopInput.KeyboardMouse
                : (inSwitchCooldown ? ControlModeArbiter.DesktopInput.None : DeliberateVrExitInput());
            var desktopInput = inSwitchCooldown ? ControlModeArbiter.DesktopInput.None : DesktopInputThisFrame();

            var decision = _arbiter.Decide(isFreecam, _hmdValid, vrEntryRequested, deliberateExit, desktopInput);
            if (decision.ChangeScheme) SetMode(MapScheme(decision.Scheme));

            RaiseHmdStateIfChanged();
        }

        /// <summary>Sets the logical mode and broadcasts it (once on first resolve, then only on change). No device re-masking happens here — that is the whole point.</summary>
        private void SetMode(ControlScheme mode)
        {
            var changed = !_hasBroadcast || mode != controlScheme;
            controlScheme = mode;
            if (!changed) return;

            _hasBroadcast = true;
            _switchCooldownUntil = Time.unscaledTime + switchCooldownSeconds;
            if (_isDebug) Debug.Log($"{XrStartupDiagnostics.LogPrefix} BroadcastControlsStatus: mode -> {mode}.");
            SendControlScheme?.Invoke(mode);

            // Remember the desktop mode (KBM/Gamepad) so the next session can start in it;
            // no-ops for XR/FreeCam, and only after startup so the initial resolve can't
            // clobber the stored preference before it is read.
            if (_started) ControlModePreference.Save(mode);
        }

        /// <summary>PlayerMovement's Ctrl+Alt+F toggle enters/exits the FreeCam sub-mode logically (no device re-masking).</summary>
        public void SetFreecam(bool on, ControlScheme restoreDesktopMode)
        {
            SetMode(on ? ControlScheme.Freecam : restoreDesktopMode);
        }

        private static ControlScheme MapScheme(ControlModeArbiter.Scheme scheme)
        {
            switch (scheme)
            {
                case ControlModeArbiter.Scheme.XR: return ControlScheme.XR;
                case ControlModeArbiter.Scheme.Gamepad: return ControlScheme.Gamepad;
                default: return ControlScheme.KeyboardMouse;
            }
        }

        private void RaiseHmdStateIfChanged()
        {
            if (_arbiter.InVr == _lastRaisedInVr) return;
            _lastRaisedInVr = _arbiter.InVr;
            if (_isDebug) Debug.Log($"BroadcastControlsStatus: VR mode {(_arbiter.InVr ? "entered" : "left")}.");
            PlayerEvents.RaiseHmdState(_arbiter.InVr);
        }

        // One-shot diagnostic: a valid HMD that reports neither userPresence nor
        // isTracked gives us no signal to auto-enter VR — surface it so it isn't a
        // silent "VR never engages".
        private void WarnIfNoPresenceSignal(DetectUserPresence.HmdState state)
        {
            if (_warnedNoSignal) return;
            if (state.HmdValid && !state.PresenceSupported && !state.Tracked)
            {
                _warnedNoSignal = true;
                Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} BroadcastControlsStatus: the HMD reports neither userPresence " +
                    "nor isTracked — VR cannot auto-engage from presence. Use Ctrl+Alt+V or an XR controller button to enter VR, " +
                    "or check the OpenXR runtime / interaction profile.", this);
            }
        }

        /// <summary>The RUNTIME worn signal: userPresence only. Tracked is deliberately excluded — over Link it never clears and would latch VR forever.</summary>
        public static bool WornForEntry(DetectUserPresence.HmdState state) =>
            state.HmdValid && state.PresenceSupported && state.Present;

        /// <summary>The LAUNCH-only worn hint: presence when supported, else the tracking flag. Never call this on the runtime poll path (that is what caused the never-leaves-VR bug).</summary>
        public static bool ComputeWornAtLaunch(DetectUserPresence.HmdState state)
        {
            if (!state.HmdValid) return false;
            return state.PresenceSupported ? state.Present : state.Tracked;
        }

        private bool _forceKeyboardComboWasDown;
        private bool _forceVrComboWasDown;

        private bool ForceKeyboardComboPressed()
        {
            var down = ComboDown(forceKeyboardKey, forceKeyboardRequiresCtrl, forceKeyboardRequiresAlt);
            var rising = down && !_forceKeyboardComboWasDown;
            _forceKeyboardComboWasDown = down;
            return rising;
        }

        private bool ForceVrComboPressed()
        {
            var down = ComboDown(forceVrKey, forceVrRequiresCtrl, forceVrRequiresAlt);
            var rising = down && !_forceVrComboWasDown;
            _forceVrComboWasDown = down;
            return rising;
        }

        // Every Ctrl+Alt combo is a tool chord (Ctrl+Alt+H overlay, Ctrl+Alt+J snapshot,
        // Ctrl+Alt+F freecam, Ctrl+Alt+V force-VR...), i.e. meta input — NOT a hand
        // returning to the keyboard to play — so none of them may read as a deliberate
        // "leave VR" gesture (Ctrl+Alt+J used to dump a diagnostic snapshot AND kick the
        // player out of VR). Ctrl+Alt+K still exits VR through its own force-keyboard
        // path, which is evaluated before the deliberate-exit scan.
        private static bool CtrlAltHeld(Keyboard keyboard) =>
            (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)
            && (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed);

        // The stricter VR-exit variant: EITHER modifier suppresses the scan, because a
        // chord's first keydown (just Ctrl) precedes the frame where both are held.
        private static bool CtrlOrAltHeld(Keyboard keyboard) =>
            keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed
            || keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;

        // Rising edge latched on isPressed, NOT wasPressedThisFrame: the press edge is
        // consumed by whichever InputSystem.Update ran last (a manual update from tooling
        // or tests eats it before this component polls), while the pressed STATE survives
        // any number of input updates — so the combo fires exactly once per physical press.
        private static bool ComboDown(Key key, bool requiresCtrl, bool requiresAlt)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (!keyboard[key].isPressed) return false;
            if (requiresCtrl && !(keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)) return false;
            if (requiresAlt && !(keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed)) return false;
            return true;
        }

        /// <summary>
        /// Failsafe used by diagnostics (e.g. a dying headset/controller battery):
        /// drop to Keyboard&amp;Mouse now. A fresh presence rising edge, an XR controller
        /// button, or Ctrl+Alt+V is then required to return to VR.
        /// </summary>
        public void ForceDesktopControls(string reason)
        {
            if (playerInput == null) return;
            Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} BroadcastControlsStatus: forcing desktop controls — {reason}", this);
            _arbiter.ForceExitVr();
            SetMode(ControlScheme.KeyboardMouse);
            RaiseHmdStateIfChanged();
        }

        /// <summary>
        /// Deliberate "leave VR now" input: a real key/button press, mouse button, a
        /// mouse move above <see cref="exitVrMouseDeltaSqr"/>, or gamepad button/stick.
        /// Stricter than <see cref="DesktopInputThisFrame"/> so a desk bump (which trips
        /// the tiny desktop-arbitration mouse threshold) does not kick you out of VR.
        /// </summary>
        private ControlModeArbiter.DesktopInput DeliberateVrExitInput()
        {
            // The force-VR chord must never read as "leave VR": its own key presses
            // trip anyKey, so the same physical Ctrl+Alt+V that just entered VR would
            // bounce straight back to desktop whenever the switch cooldown is not
            // there to swallow it (it is configurable, and tests run it at 0).
            if (ComboDown(forceVrKey, forceVrRequiresCtrl, forceVrRequiresAlt))
                return ControlModeArbiter.DesktopInput.None;

            // Ctrl/Alt suppress the key scan entirely: tool chords (Ctrl+Alt+J snapshot,
            // Ctrl+Alt+H overlay...) arrive as Ctrl, THEN Alt, THEN the letter — the
            // very first Ctrl keydown must not already read as "leaving VR". Nobody
            // returns to the keyboard by pressing only Ctrl; a real exit is WASD, a
            // mouse move/click, or the gamepad — all still detected below/afterwards.
            var keyboard = Keyboard.current;
            if (keyboard != null && !CtrlOrAltHeld(keyboard) && keyboard.anyKey.wasPressedThisFrame)
                return ControlModeArbiter.DesktopInput.KeyboardMouse;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
                    || mouse.middleButton.wasPressedThisFrame) return ControlModeArbiter.DesktopInput.KeyboardMouse;
                if (mouse.delta.ReadValue().sqrMagnitude > exitVrMouseDeltaSqr) return ControlModeArbiter.DesktopInput.KeyboardMouse;
            }

            return GamepadInputThisFrame();
        }

        private static ControlModeArbiter.DesktopInput DesktopInputThisFrame()
        {
            var keyboard = Keyboard.current;
            // Tool chords don't count here either: toggling the overlay (Ctrl+Alt+H) or
            // dumping a snapshot (Ctrl+Alt+J) while playing on gamepad must not flip the
            // mode to Keyboard&Mouse.
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame && !CtrlAltHeld(keyboard))
                return ControlModeArbiter.DesktopInput.KeyboardMouse;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
                    || mouse.middleButton.wasPressedThisFrame) return ControlModeArbiter.DesktopInput.KeyboardMouse;
                if (mouse.delta.ReadValue().sqrMagnitude > 9f) return ControlModeArbiter.DesktopInput.KeyboardMouse; // deliberate movement, not desk vibration
            }

            return GamepadInputThisFrame();
        }

        // A discrete XR-controller BUTTON press, used as a VR re-entry signal (the mirror
        // of desktop input leaving VR). Deliberately buttons-only (wasPressedThisFrame) —
        // NOT analog trigger/grip/stick values, which drift on a resting controller and
        // would continuously re-trigger VR entry (the desktop<->VR oscillation). Hoisted
        // array keeps this allocation-free per frame.
        private static readonly string[] XrButtonControls =
            { "triggerPressed", "gripPressed", "primaryButton", "secondaryButton", "menuButton", "primary2DAxisClick" };

        private static bool XrControllerButtonPressedThisFrame()
        {
            var devices = InputSystem.devices;
            for (int i = 0; i < devices.Count; i++)
            {
                if (!(devices[i] is UnityEngine.InputSystem.XR.XRController controller)) continue;

                for (int b = 0; b < XrButtonControls.Length; b++)
                {
                    if (controller.TryGetChildControl<ButtonControl>(XrButtonControls[b]) is ButtonControl button
                        && button.wasPressedThisFrame) return true;
                }
            }
            return false;
        }

        private static ControlModeArbiter.DesktopInput GamepadInputThisFrame()
        {
            var gamepad = Gamepad.current;
            if (gamepad == null) return ControlModeArbiter.DesktopInput.None;

            if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame
                || gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame
                || gamepad.leftShoulder.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame
                || gamepad.startButton.wasPressedThisFrame || gamepad.selectButton.wasPressedThisFrame)
                return ControlModeArbiter.DesktopInput.Gamepad;
            if (gamepad.leftStick.ReadValue().sqrMagnitude > 0.1f || gamepad.rightStick.ReadValue().sqrMagnitude > 0.1f
                || gamepad.leftTrigger.ReadValue() > 0.3f || gamepad.rightTrigger.ReadValue() > 0.3f)
                return ControlModeArbiter.DesktopInput.Gamepad;

            return ControlModeArbiter.DesktopInput.None;
        }

        public bool GetHMDState()
        {
            return controlScheme == ControlScheme.XR;
        }
    }
}
