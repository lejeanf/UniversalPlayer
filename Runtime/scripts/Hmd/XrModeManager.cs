using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.XR;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Optionally matches the XR <b>display</b> to the current control mode: stereo
    /// while in VR, a flat full-screen view on desktop. Follows
    /// <see cref="BroadcastControlsStatus.SendControlScheme"/>.
    ///
    /// DISABLED BY DEFAULT (<see cref="manageXrDisplay"/> = false). Stopping and
    /// restarting the <see cref="XRDisplaySubsystem"/> at runtime is fragile —
    /// especially in the Editor and over Link — where a restart can fail to
    /// re-establish HMD rendering and can reset the tracking origin (wrong eye
    /// height). With it off, Unity's "Initialize XR on Startup" keeps VR rendering to
    /// the headset normally; the only cost is that a headset left connected while on
    /// desktop mirrors one eye to the monitor. Enable this only after validating the
    /// stop/start behaviour on the target device/runtime.
    ///
    /// When enabled, only the display subsystem is toggled — the input/tracking
    /// subsystem stays running so <c>userPresence</c> and the head pose remain
    /// readable on desktop, and OpenXR loader init is never re-run (so the "DPad"
    /// gamepad-breaking layout is not re-registered; see <see cref="DpadLayoutGuard"/>).
    /// </summary>
    public class XrModeManager : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        [Tooltip("EXPERIMENTAL. Stop the XR display on desktop for a flat monitor view. Off by default because runtime stop/start of the display subsystem can break HMD rendering and reset the tracking origin (wrong camera height) in the Editor / over Link. Validate on-device before enabling.")]
        [SerializeField] private bool manageXrDisplay = false;

        [Tooltip("How often (seconds) the XR display's running state is reconciled against the current mode — catches the display subsystem starting asynchronously (Link handshake) after the last scheme change.")]
        [SerializeField] private float reconcileIntervalSeconds = 0.5f;

        private static readonly List<XRDisplaySubsystem> DisplaySubsystems = new List<XRDisplaySubsystem>();
        private bool _wantXr;
        private float _nextReconcile;

        private void OnEnable()
        {
            if (!manageXrDisplay) return;
            BroadcastControlsStatus.SendControlScheme += OnControlSchemeChanged;
            Apply(BroadcastControlsStatus.controlScheme);
        }

        private void OnDisable()
        {
            BroadcastControlsStatus.SendControlScheme -= OnControlSchemeChanged;
        }

        // The display subsystem can start asynchronously (headset connect / Link
        // handshake) AFTER the last scheme broadcast, which would leave a stereo
        // display running on desktop (the one-eye view). A cheap interval reconcile
        // re-asserts the desired state; it is allocation-free (reused static list).
        private void Update()
        {
            if (!manageXrDisplay) return;
            if (Time.unscaledTime < _nextReconcile) return;
            _nextReconcile = Time.unscaledTime + reconcileIntervalSeconds;
            SetDisplayRunning(_wantXr);
        }

        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme scheme) => Apply(scheme);

        private void Apply(BroadcastControlsStatus.ControlScheme scheme)
        {
            _wantXr = scheme == BroadcastControlsStatus.ControlScheme.XR;
            SetDisplayRunning(_wantXr);
        }

        private void SetDisplayRunning(bool shouldRun)
        {
            SubsystemManager.GetSubsystems(DisplaySubsystems);
            if (DisplaySubsystems.Count == 0)
            {
                // No XR display at all (desktop-only session, headset never connected):
                // nothing to toggle, the flat view is already correct.
                if (shouldRun && _isDebug)
                    Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} XrModeManager: entering XR but no XRDisplaySubsystem exists — " +
                        "is a headset connected and Initialize XR on Startup enabled?", this);
                return;
            }

            for (int i = 0; i < DisplaySubsystems.Count; i++)
            {
                var display = DisplaySubsystems[i];
                if (display == null) continue;

                if (shouldRun && !display.running)
                {
                    display.Start();
                    // OpenXR may have (re)registered layouts around session activity;
                    // keep gamepad creation working.
                    DpadLayoutGuard.RepairIfNeeded();
                    if (_isDebug) Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: XR display started (VR view).");
                }
                else if (!shouldRun && display.running)
                {
                    display.Stop();
                    if (_isDebug) Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: XR display stopped (flat desktop view).");
                }
            }
        }
    }
}
