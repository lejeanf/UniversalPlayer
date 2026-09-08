using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;
#if UNIVERSALPLAYER_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace jeanf.universalplayer
{
    public class XrModeManager : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        [Tooltip("Flat monitor view on desktop, stereo in VR, on the one player camera (URP / HDRP / built-in).")]
        [SerializeField] private bool manageCameraXrRendering = true;

        [Tooltip("Explicit player camera. Empty: the FPSCameraMovement camera found inside this player rig.")]
        [SerializeField] public Camera playerCameraOverride;

        public enum DisplayStopMode
        {
            Never,
            EditorOnly,
            Always
        }

        [Tooltip("Never: the XR display keeps running on desktop (fed by the session keeper) so VR re-entry is instant. EditorOnly / Always: the XR loader stops the display on desktop and restarts it on VR entry — fully XR-free desktop, slower re-entry (Link handshake). The display is only ever started or stopped through the XR loader.")]
        [SerializeField] private DisplayStopMode stopXrDisplayOnDesktop = DisplayStopMode.Never;

        [Tooltip("On desktop, a hidden black camera keeps rendering to an ALREADY running XR display so the OpenXR session never idles. It never starts the display itself.")]
        [SerializeField] private bool keepXrSessionAlive = true;

        [Tooltip("Flat-view vertical FOV restored after VR when no desktop FOV was captured before entering it.")]
        [SerializeField] private float desktopFieldOfView = 60f;

        [Tooltip("Seconds between session keeper reconciles (the XR display can start or stop on its own).")]
        [SerializeField] private float reconcileIntervalSeconds = 0.5f;

        private bool ShouldManageDisplay =>
            stopXrDisplayOnDesktop == DisplayStopMode.Always
            || (stopXrDisplayOnDesktop == DisplayStopMode.EditorOnly && Application.isEditor);

        private bool _wantXr;
        private bool _hasApplied;
        private float _nextReconcile;
        private Camera _resolvedCamera;
        private Camera _sessionKeeper;
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

        private void Update()
        {
            if (!ShouldManageDisplay && !manageCameraXrRendering) return;
            if (Time.unscaledTime < _nextReconcile) return;
            _nextReconcile = Time.unscaledTime + reconcileIntervalSeconds;
            ReconcileSessionKeeper();
        }

        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme scheme) => Apply(scheme);

        private void Apply(BroadcastControlsStatus.ControlScheme scheme)
        {
            var wantXr = scheme == BroadcastControlsStatus.ControlScheme.XR;
            var enteringXr = wantXr && (!_hasApplied || !_wantXr);
            var leavingXr = !wantXr && (!_hasApplied || _wantXr);
            _wantXr = wantXr;
            _hasApplied = true;

            ApplyCameraXrRendering();
            if (enteringXr) EnterXrDisplay();
            if (leavingXr) LeaveXrDisplay();
            ReconcileSessionKeeper();
        }

        private void ApplyCameraXrRendering()
        {
            if (!manageCameraXrRendering) return;
            var cam = ResolveCamera();
            if (cam == null) return;

            if (_wantXr && _hasAppliedCameraState && !_lastAppliedXr)
                _capturedDesktopFov = cam.fieldOfView;

            CameraXrRendering.Set(cam, _wantXr);

            if (!_wantXr && _hasAppliedCameraState && _lastAppliedXr)
                cam.fieldOfView = _capturedDesktopFov > 0f ? _capturedDesktopFov : desktopFieldOfView;

            _hasAppliedCameraState = true;
            _lastAppliedXr = _wantXr;
        }

        private void EnterXrDisplay()
        {
            XrDisplayLifecycle.RequestMirrorBlitMode(XRMirrorViewBlitMode.Default);
            var displayRunning = XrDisplayLifecycle.IsDisplayRunning;
            if (ShouldManageDisplay || !displayRunning)
            {
                var accepted = XrDisplayLifecycle.RequestStart();
                if (_isDebug)
                    Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: VR entry — display {(displayRunning ? "running" : "idle")}, " +
                        $"loader start {(accepted ? "requested" : "unavailable (no initialized XR loader)")}, session focused={XrDisplayLifecycle.SessionFocused}.");
                return;
            }
            if (_isDebug)
                Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: VR entry — display already running, session focused={XrDisplayLifecycle.SessionFocused}.");
        }

        private void LeaveXrDisplay()
        {
            XrDisplayLifecycle.RequestMirrorBlitMode(XRMirrorViewBlitMode.None);
            if (!ShouldManageDisplay) return;
            var accepted = XrDisplayLifecycle.RequestStop();
            if (_isDebug)
                Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: desktop — loader stop {(accepted ? "requested" : "unavailable (no initialized XR loader)")}.");
        }

        private void ReconcileSessionKeeper()
        {
            var display = XrDisplayLifecycle.FirstDisplay;
            if (display != null)
                XrDisplayLifecycle.RequestMirrorBlitMode(_wantXr ? XRMirrorViewBlitMode.Default : XRMirrorViewBlitMode.None);

            var wantKeeper = keepXrSessionAlive && manageCameraXrRendering && !ShouldManageDisplay && !_wantXr
                             && display != null && display.running;

            if (wantKeeper && _sessionKeeper == null) CreateSessionKeeper();
            if (_sessionKeeper != null && _sessionKeeper.enabled != wantKeeper)
            {
                _sessionKeeper.enabled = wantKeeper;
                if (_isDebug) Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: session keeper {(wantKeeper ? "on (desktop, display running)" : "off")}.");
            }
        }

        private void CreateSessionKeeper()
        {
            var playerCamera = ResolveCamera();
            var go = new GameObject("XR Session Keeper (UniversalPlayer)");
            go.transform.SetParent(transform, false);
            _sessionKeeper = go.AddComponent<Camera>();
            _sessionKeeper.cullingMask = 0;
            _sessionKeeper.clearFlags = CameraClearFlags.SolidColor;
            _sessionKeeper.backgroundColor = Color.black;
            _sessionKeeper.depth = -100f;
            _sessionKeeper.nearClipPlane = playerCamera != null ? playerCamera.nearClipPlane : 0.05f;
            _sessionKeeper.farClipPlane = playerCamera != null ? playerCamera.farClipPlane : 1000f;
            _sessionKeeper.allowMSAA = false;
            _sessionKeeper.enabled = false;
#if UNIVERSALPLAYER_HDRP
            if (GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset)
            {
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

            var look = transform.root.GetComponentInChildren<FPSCameraMovement>(true);
            _resolvedCamera = look != null ? look.playerCamera : null;

            if (_resolvedCamera == null && _isDebug)
                Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} XrModeManager: no player camera inside the rig '{transform.root.name}' " +
                    "(no FPSCameraMovement.playerCamera and no override) — camera XR rendering is not managed.", this);
            return _resolvedCamera;
        }
    }
}
