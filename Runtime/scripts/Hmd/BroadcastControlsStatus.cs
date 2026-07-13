using jeanf.EventSystem;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
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

        [SerializeField] public PlayerInput playerInput;

        [Header("HMD presence")]
        [Tooltip("How often (seconds) the headset presence sensor is polled to switch VR <-> Keyboard&Mouse.")]
        [SerializeField] private float hmdPollIntervalSeconds = 0.25f;
        [Tooltip("After entering XR (headset donned), desktop input is ignored for this long — putting a headset on usually bumps the desk.")]
        [SerializeField] private float xrInputGraceSeconds = 1.5f;
        [Tooltip("Reclaiming XR via headset movement / controller buttons requires the keyboard+mouse to have been quiet for this long.")]
        [SerializeField] private float desktopQuietSecondsBeforeXrReclaim = 2f;

        // Broadcasts go through PlayerEvents/SendControlScheme; the PlayerEventBridge
        // forwards them onto the project's channels.

        /// <summary>
        /// Test seam: how "is the headset on a head right now" is probed.
        /// Defaults to the real presence sensor (DetectUserPresence).
        /// </summary>
        public Func<bool> HmdMountedProbe = DetectUserPresence.IsHMDMounted;

        private float _nextHmdPoll;
        private bool _hmdMounted;
        private float _xrGraceUntil;
        private float _lastDesktopInputTime = -10f;
        private Vector3 _lastHmdPosition;
        private bool _hasHmdPosition;
        private static readonly System.Collections.Generic.List<UnityEngine.XR.InputDevice> XrDeviceScratch =
            new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();

        private enum DesktopInputKind { None, KeyboardMouse, Gamepad }

        private void Awake()
        {
            if (playerInput == null)
            {
                Debug.LogError($"{XrStartupDiagnostics.LogPrefix} BroadcastControlsStatus on '{name}': playerInput is not assigned — " +
                    "control scheme detection and VR/keyboard switching are disabled. Assign it on the Player prefab variant.", this);
                return;
            }
            SetCurrentControlSchemeOnSwitch();
        }

        private void OnEnable()
        {
            if (playerInput != null) playerInput.onControlsChanged += OnControlsChanged;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (playerInput != null) playerInput.onControlsChanged -= OnControlsChanged;
        }

        private void OnControlsChanged(PlayerInput _) => SetCurrentControlSchemeOnSwitch();

        private void Start()
        {
            _hmdMounted = HmdMountedProbe();
            PlayerEvents.RaiseHmdState(_hmdMounted);
            if (_hmdMounted) _xrGraceUntil = Time.unscaledTime + xrInputGraceSeconds;
            ApplyHmdMountedState(_hmdMounted);
        }

        private void Update()
        {
            if (playerInput == null) return;

            // ---- arbitration rule: the most recent MEANINGFUL input wins. ----
            // A headset resting on a desk frequently reports 'user present' (light on the
            // proximity sensor, face-down resting, Link idling), so presence alone must
            // never be able to lock the mouse and keyboard out.
            var desktop = DesktopInputThisFrame();
            if (desktop != DesktopInputKind.None) _lastDesktopInputTime = Time.unscaledTime;

            if (controlScheme == ControlScheme.XR && desktop != DesktopInputKind.None
                && Time.unscaledTime > _xrGraceUntil)
            {
                if (_isDebug) Debug.Log("BroadcastControlsStatus: desktop input while in XR — switching to desktop controls " +
                    "(presence sensor may be misreporting).");
                // Presence may still read 'mounted': keep auto-switching blocked so the
                // continuously-streaming HMD data cannot yank the scheme straight back.
                playerInput.neverAutoSwitchControlSchemes = _hmdMounted;
                if (desktop == DesktopInputKind.Gamepad && Gamepad.current != null)
                    TrySwitchScheme("Gamepad", new InputDevice[] { Gamepad.current });
                else if (Keyboard.current != null)
                    TrySwitchScheme("Keyboard&Mouse", KeyboardMouseDevices());
            }
            else if (desktop == DesktopInputKind.Gamepad && controlScheme == ControlScheme.KeyboardMouse
                     && Gamepad.current != null)
            {
                // While a plugged headset keeps the latch on, PlayerInput's native
                // KBM<->Gamepad auto-switch is blocked — arbitrate those manually too.
                TrySwitchScheme("Gamepad", new InputDevice[] { Gamepad.current });
            }
            else if (desktop == DesktopInputKind.KeyboardMouse && controlScheme == ControlScheme.Gamepad
                     && Keyboard.current != null)
            {
                TrySwitchScheme("Keyboard&Mouse", KeyboardMouseDevices());
            }

            if (Time.unscaledTime < _nextHmdPoll) return;
            _nextHmdPoll = Time.unscaledTime + hmdPollIntervalSeconds;

            var mounted = HmdMountedProbe();
            var hmdMoved = HmdMovedSinceLastPoll();

            if (mounted != _hmdMounted)
            {
                _hmdMounted = mounted;
                if (_isDebug) Debug.Log($"BroadcastControlsStatus: HMD {(mounted ? "mounted" : "removed or disconnected")}.");
                PlayerEvents.RaiseHmdState(mounted);
                if (mounted) _xrGraceUntil = Time.unscaledTime + xrInputGraceSeconds;
                ApplyHmdMountedState(mounted);
            }
            else if (mounted && (hmdMoved || XrControllerButtonHeld()))
            {
                // The headset is actually being USED (it moved / a controller button is
                // held): reclaim XR without needing a presence edge — but only when the
                // desktop has been quiet, so typing near a worn headset doesn't ping-pong.
                if ((controlScheme == ControlScheme.KeyboardMouse || controlScheme == ControlScheme.Gamepad)
                    && Time.unscaledTime - _lastDesktopInputTime > desktopQuietSecondsBeforeXrReclaim)
                {
                    if (_isDebug) Debug.Log("BroadcastControlsStatus: headset activity — reclaiming XR.");
                    _xrGraceUntil = Time.unscaledTime + xrInputGraceSeconds;
                    ApplyHmdMountedState(true);
                }
            }
        }

        /// <summary>
        /// Arbitration override for failsafes (dying headset/controller battery, ...):
        /// switch to desktop controls NOW and hold off the XR reclaim as if the user
        /// had just used the keyboard.
        /// </summary>
        public void ForceDesktopControls(string reason)
        {
            if (playerInput == null) return;
            Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} BroadcastControlsStatus: forcing desktop controls — {reason}", this);
            playerInput.neverAutoSwitchControlSchemes = _hmdMounted;
            _lastDesktopInputTime = Time.unscaledTime;
            if (Keyboard.current != null) TrySwitchScheme("Keyboard&Mouse", KeyboardMouseDevices());
        }

        private static DesktopInputKind DesktopInputThisFrame()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return DesktopInputKind.KeyboardMouse;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame
                    || mouse.middleButton.wasPressedThisFrame) return DesktopInputKind.KeyboardMouse;
                if (mouse.delta.ReadValue().sqrMagnitude > 9f) return DesktopInputKind.KeyboardMouse; // deliberate movement, not desk vibration
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                if (gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame
                    || gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame
                    || gamepad.leftShoulder.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame
                    || gamepad.startButton.wasPressedThisFrame || gamepad.selectButton.wasPressedThisFrame)
                    return DesktopInputKind.Gamepad;
                if (gamepad.leftStick.ReadValue().sqrMagnitude > 0.1f || gamepad.rightStick.ReadValue().sqrMagnitude > 0.1f
                    || gamepad.leftTrigger.ReadValue() > 0.3f || gamepad.rightTrigger.ReadValue() > 0.3f)
                    return DesktopInputKind.Gamepad;
            }

            return DesktopInputKind.None;
        }

        /// <summary>True when the HMD's tracked position moved more than 2cm since the previous poll — a worn head always does, a desk headset does not.</summary>
        private bool HmdMovedSinceLastPoll()
        {
            var head = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
            if (!head.isValid || !head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out var position))
            {
                _hasHmdPosition = false;
                return false;
            }
            var moved = _hasHmdPosition && (position - _lastHmdPosition).magnitude > 0.02f;
            _lastHmdPosition = position;
            _hasHmdPosition = true;
            return moved;
        }

        private static bool XrControllerButtonHeld()
        {
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(
                UnityEngine.XR.InputDeviceCharacteristics.Controller | UnityEngine.XR.InputDeviceCharacteristics.HeldInHand,
                XrDeviceScratch);
            foreach (var device in XrDeviceScratch)
            {
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out var pressed) && pressed) return true;
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out pressed) && pressed) return true;
                if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out pressed) && pressed) return true;
            }
            return false;
        }

        /// <summary>
        /// The old code set neverAutoSwitchControlSchemes = true permanently on entering
        /// XR, so VR -> keyboard never switched back. The latch is still wanted WHILE the
        /// headset is worn (idle mouse/keyboard input must not yank the player out of VR),
        /// so it now follows the presence sensor instead of being one-way.
        /// </summary>
        private void ApplyHmdMountedState(bool mounted)
        {
            if (playerInput == null) return;

            if (mounted)
            {
                playerInput.neverAutoSwitchControlSchemes = true;
                TrySwitchScheme("XR", XrInputDevices());
            }
            else
            {
                playerInput.neverAutoSwitchControlSchemes = false;
                if (Keyboard.current != null)
                    TrySwitchScheme("Keyboard&Mouse", KeyboardMouseDevices());
            }
        }

        private void TrySwitchScheme(string schemeName, InputDevice[] devices)
        {
            if (devices.Length == 0) return;
            if (playerInput.currentControlScheme == schemeName) return;
            try
            {
                playerInput.SwitchCurrentControlScheme(schemeName, devices);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} BroadcastControlsStatus: could not switch to control scheme " +
                    $"'{schemeName}': {e.Message}. Check the scheme exists under exactly that name in the PlayerInput actions asset.", this);
            }
        }

        private static InputDevice[] XrInputDevices() =>
            InputSystem.devices.Where(d => d is UnityEngine.InputSystem.XR.XRHMD || d is UnityEngine.InputSystem.XR.XRController).ToArray();

        private static InputDevice[] KeyboardMouseDevices() =>
            new InputDevice[] { Keyboard.current, Mouse.current }.Where(d => d != null).ToArray();

        private void SetCurrentControlSchemeOnSwitch()
        {
            switch (playerInput.currentControlScheme)
            {
                case "Keyboard&Mouse":
                    controlScheme = ControlScheme.KeyboardMouse;
                    break;
                case "Gamepad":
                    controlScheme = ControlScheme.Gamepad;
                    break;
                case "XR":
                    controlScheme = ControlScheme.XR;
                    break;
                case "FreeCam":
                    controlScheme = ControlScheme.Freecam;
                    break;
            }

            SendControlScheme?.Invoke(controlScheme);
        }

        public bool GetHMDState()
        {
            return controlScheme == ControlScheme.XR;
        }
    }
}
