using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
#if UNIVERSALPLAYER_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace jeanf.universalplayer
{
    /// <summary>
    /// Matches the rendering to the current control mode: stereo while in VR, a flat
    /// full-screen view on desktop. Follows
    /// <see cref="BroadcastControlsStatus.SendControlScheme"/>. Two cooperating
    /// strategies on the ONE player camera:
    ///
    /// <b>Camera XR rendering (<see cref="manageCameraXrRendering"/> = true).</b>
    /// Toggles XR rendering on the player camera via <see cref="CameraXrRendering"/>
    /// (URP/HDRP/built-in agnostic), which on the way OUT of VR also resets the
    /// camera's projection/view matrices — leaving stereo otherwise strands the camera
    /// on the left eye's asymmetric projection, which is what made the desktop view
    /// "stay on Left Eye" after a VR round-trip. The pipelines skip the VR mirror blit
    /// for a camera with XR rendering off, so the flat render reaches the window even
    /// while the <see cref="XRDisplaySubsystem"/> keeps running, and VR re-entry is a
    /// plain flag flip — instant, no display restart.
    ///
    /// <b>Session keeper (<see cref="keepXrSessionAlive"/> = true).</b> The OpenXR
    /// runtime winds the session down as soon as the app stops submitting XR frames —
    /// observed as displayRunning flipping to False ~a second after the desktop switch
    /// with nothing stopping it — and the next VR entry then pays a full session
    /// (re)start: seconds of black headset over Link while the controllers already
    /// work. While on desktop, a hidden black camera (cullingMask 0) keeps rendering
    /// to the display so the session stays warm and every VR entry — the FIRST one
    /// included — is instant. It also starts a present-but-not-running display so the
    /// session is warmed during desktop play rather than on VR entry.
    ///
    /// <b>Display stop on desktop (<see cref="stopXrDisplayOnDesktop"/> =
    /// <see cref="DisplayStopMode.Never"/>).</b> Stopping the display on desktop is
    /// NOT needed for a correct flat view (see above), defeats the session keeper, and
    /// costs the session restart on every VR re-entry. Its only visible gain is Editor
    /// chrome: with the display running the Game view keeps its Left/Right Eye
    /// dropdown (the CONTENT is the correct flat view regardless). EditorOnly/Always
    /// exist for setups that want the fully XR-free desktop state anyway; the interval
    /// reconcile retries a failed start every <see cref="reconcileIntervalSeconds"/>.
    ///
    /// In both strategies the input/tracking subsystem stays running so
    /// <c>userPresence</c> and the head pose remain readable on desktop, and OpenXR
    /// loader init is never re-run (so the "DPad" gamepad-breaking layout is not
    /// re-registered; see <see cref="DpadLayoutGuard"/>).
    /// </summary>
    public class XrModeManager : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        [Tooltip("Toggle XR rendering on the player camera per control mode: flat monitor view on desktop, stereo in VR. Pipeline-agnostic (URP/HDRP/built-in) and leaves the XR display subsystem running, so VR re-entry never goes through a display restart.")]
        [SerializeField] private bool manageCameraXrRendering = true;

        [Tooltip("Optional explicit player camera. When empty, FPSCameraMovement's playerCamera is used (searched on this object's children first, then the scene).")]
        [SerializeField] public Camera playerCameraOverride;

        public enum DisplayStopMode
        {
            Never,
            EditorOnly,
            Always
        }

        [Tooltip("Stop the XR display subsystem while on desktop. Never (default): the camera switch already yields a correct flat desktop view with the display running, and re-entering VR stays INSTANT — no display restart. Stopping costs a full Link re-handshake on every VR re-entry (measured: seconds of black headset while controllers already work), defeats the session keeper, and buys only cosmetic Editor chrome (the Game view's eye dropdown disappears). EditorOnly / Always: stop it anyway (Editor / everywhere) if a fully XR-free desktop state matters more than fast re-entry.")]
        [SerializeField] private DisplayStopMode stopXrDisplayOnDesktop = DisplayStopMode.Never;

        [Tooltip("While on desktop, keep a hidden black camera (cullingMask 0) rendering to the XR display so the OpenXR session never winds down — without frames the runtime idles the session and the next VR entry (the FIRST included) pays a full session start: seconds of black headset over Link. The keeper only runs when a display subsystem exists, the mode is desktop, and the display is not being stopped by Stop Xr Display On Desktop.")]
        [SerializeField] private bool keepXrSessionAlive = true;

        [Tooltip("Fallback flat-view vertical FOV restored on the player camera after leaving VR — stereo rendering overwrites Camera.fieldOfView with the headset's FOV (~100°). Only used when VR was active from the very first mode resolve; otherwise the FOV captured on VR entry is restored, preserving whatever the project/gameplay had set.")]
        [SerializeField] private float desktopFieldOfView = 60f;

        private bool ShouldManageDisplay =>
            stopXrDisplayOnDesktop == DisplayStopMode.Always
            || (stopXrDisplayOnDesktop == DisplayStopMode.EditorOnly && Application.isEditor);

        [Tooltip("How often (seconds) the XR display's running state is reconciled against the current mode — catches the display subsystem starting asynchronously (Link handshake) after the last scheme change.")]
        [SerializeField] private float reconcileIntervalSeconds = 0.5f;

        private static readonly List<XRDisplaySubsystem> DisplaySubsystems = new List<XRDisplaySubsystem>();
        private bool _wantXr;
        private float _nextReconcile;
        private Camera _resolvedCamera;
        private Camera _sessionKeeper;
        // FOV round-trip state: stereo overwrites Camera.fieldOfView with the HMD's
        // (~100°); the desktop value must be captured on the desktop->VR edge and put
        // back on the VR->desktop edge (matrix resets alone re-derive the projection
        // from the WRONG fieldOfView otherwise — the "FOV wrong after VR" bug).
        private bool _hasAppliedCameraState;
        private bool _lastAppliedXr;
        private float _capturedDesktopFov = -1f;

        private void OnEnable()
        {
            if (!ShouldManageDisplay && !manageCameraXrRendering) return;
            BroadcastControlsStatus.SendControlScheme += OnControlSchemeChanged;
            Apply(BroadcastControlsStatus.controlScheme);
        }

        private void OnDisable()
        {
            BroadcastControlsStatus.SendControlScheme -= OnControlSchemeChanged;
            if (_sessionKeeper != null) _sessionKeeper.enabled = false;
        }

        // The display subsystem can start asynchronously (headset connect / Link
        // handshake) AFTER the last scheme broadcast, which would leave a stereo
        // display running on desktop (the one-eye view). A cheap interval reconcile
        // re-asserts the desired state; it is allocation-free (reused static list).
        // Camera XR state is deliberately NOT reconciled here: nothing flips it back
        // behind our back, and re-writing it every tick spams the SRP console warning
        // path (Camera setters log) for zero benefit.
        private void Update()
        {
            if (!ShouldManageDisplay && !manageCameraXrRendering) return;
            if (Time.unscaledTime < _nextReconcile) return;
            _nextReconcile = Time.unscaledTime + reconcileIntervalSeconds;
            if (ShouldManageDisplay) SetDisplayRunning(_wantXr);
            ReconcileSessionKeeper(); // also catches a display appearing mid-session
        }

        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme scheme) => Apply(scheme);

        private void Apply(BroadcastControlsStatus.ControlScheme scheme)
        {
            _wantXr = scheme == BroadcastControlsStatus.ControlScheme.XR;
            ApplyCurrent();
        }

        private void ApplyCurrent()
        {
            if (manageCameraXrRendering)
            {
                var cam = ResolveCamera();
                if (cam != null)
                {
                    // Edge-triggered: capture only desktop->VR, restore only VR->desktop,
                    // so desktop<->desktop switches never touch a gameplay-driven FOV.
                    if (_wantXr && _hasAppliedCameraState && !_lastAppliedXr)
                        _capturedDesktopFov = cam.fieldOfView;

                    CameraXrRendering.Set(cam, _wantXr);

                    if (!_wantXr && _hasAppliedCameraState && _lastAppliedXr)
                        cam.fieldOfView = _capturedDesktopFov > 0f ? _capturedDesktopFov : desktopFieldOfView;

                    _hasAppliedCameraState = true;
                    _lastAppliedXr = _wantXr;
                }
            }
            if (ShouldManageDisplay) SetDisplayRunning(_wantXr);
            // Same frame as the mode change, so leaving VR hands the frame stream to
            // the keeper with no gap — a gap is what lets the runtime idle the session.
            ReconcileSessionKeeper();
        }

        // See the class comment: without frames the OpenXR runtime winds the session
        // down (displayRunning drops to False on its own shortly after the desktop
        // switch) and the next VR entry pays a full session start. On desktop the
        // keeper renders black at near-zero cost to keep the stream alive; it renders
        // FIRST (depth -100) so the flat player camera still owns the desktop view and
        // the pipelines still skip the VR mirror blit (gated on the LAST game camera).
        private void ReconcileSessionKeeper()
        {
            var wantKeeper = keepXrSessionAlive && manageCameraXrRendering && !ShouldManageDisplay && !_wantXr;

            XRDisplaySubsystem display = null;
            if (wantKeeper)
            {
                SubsystemManager.GetSubsystems(DisplaySubsystems);
                display = DisplaySubsystems.Count > 0 ? DisplaySubsystems[0] : null;
                wantKeeper = display != null;
            }

            // Warm a present-but-idle display during desktop play (launch, or a headset
            // connected mid-session) so the FIRST VR entry doesn't pay the session start.
            if (wantKeeper && !display.running)
            {
                display.Start();
                DpadLayoutGuard.RepairIfNeeded();
                if (_isDebug) Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: started the XR display on desktop to warm the session (keeper).");
            }

            if (wantKeeper && _sessionKeeper == null) CreateSessionKeeper();
            if (_sessionKeeper != null && _sessionKeeper.enabled != wantKeeper)
            {
                _sessionKeeper.enabled = wantKeeper;
                if (_isDebug) Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: session keeper {(wantKeeper ? "on (desktop)" : "off (VR)")}.");
            }
        }

        private void CreateSessionKeeper()
        {
            var go = new GameObject("XR Session Keeper (UniversalPlayer)");
            go.transform.SetParent(transform, false);
            _sessionKeeper = go.AddComponent<Camera>();
            _sessionKeeper.cullingMask = 0;
            _sessionKeeper.clearFlags = CameraClearFlags.SolidColor;
            _sessionKeeper.backgroundColor = Color.black;
            _sessionKeeper.depth = -100f;
            _sessionKeeper.nearClipPlane = 0.01f;
            _sessionKeeper.farClipPlane = 0.02f;
            _sessionKeeper.allowMSAA = false;
            _sessionKeeper.enabled = false;
#if UNIVERSALPLAYER_HDRP
            if (GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset)
            {
                // HDRP without additional data clears to sky; force a plain black clear
                // so the keeper stays near-free. xrRendering defaults to true.
                var hdData = go.AddComponent<HDAdditionalCameraData>();
                hdData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                hdData.backgroundColorHDR = Color.black;
            }
#endif
        }

        private Camera ResolveCamera()
        {
            if (playerCameraOverride != null) return playerCameraOverride;
            if (_resolvedCamera != null) return _resolvedCamera;

            var fpsCamera = GetComponentInChildren<FPSCameraMovement>(true);
            if (fpsCamera == null) fpsCamera = FindFirstObjectByType<FPSCameraMovement>(FindObjectsInactive.Include);
            _resolvedCamera = fpsCamera != null ? fpsCamera.playerCamera : null;

            if (_resolvedCamera == null && _isDebug)
                Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} XrModeManager: no player camera found " +
                    "(no FPSCameraMovement.playerCamera and no override) — camera XR rendering is not managed.", this);
            return _resolvedCamera;
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
