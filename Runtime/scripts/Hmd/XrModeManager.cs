using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Rendering;
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

        [Tooltip("Never: the XR display keeps running on desktop (fed by the session keeper) so VR re-entry is instant. EditorOnly / Always: the display is stopped on desktop and restarted on VR entry — fully XR-free desktop, slower re-entry (Link handshake).")]
        [SerializeField] private DisplayStopMode stopXrDisplayOnDesktop = DisplayStopMode.Never;

        [Tooltip("On desktop, a hidden black camera keeps rendering to the XR display so the OpenXR session never idles, and a present-but-idle display is started so the first VR entry is instant.")]
        [SerializeField] private bool keepXrSessionAlive = true;

        [Tooltip("Flat-view vertical FOV restored after VR when no desktop FOV was captured before entering it.")]
        [SerializeField] private float desktopFieldOfView = 60f;

        [Tooltip("Seconds between reconciles of the XR display and the session keeper against the current mode (the display can start on its own after a Link handshake).")]
        [SerializeField] private float reconcileIntervalSeconds = 0.5f;

        public enum DesktopFlatView
        {
            OffscreenPresenter,
            CameraDirect
        }

        [Tooltip("How the flat desktop picture reaches the window while the XR display keeps running. Offscreen Presenter: the player camera renders into a window-sized texture shown through a screen-space overlay (a camera with a target texture is never treated as an XR eye — fixes the stretched picture with a strip of the other eye). Camera Direct: the camera renders straight to the screen (correct only when the engine gives a non-XR camera the window viewport).")]
        [SerializeField] private DesktopFlatView desktopFlatView = DesktopFlatView.OffscreenPresenter;

        private bool ShouldManageDisplay =>
            stopXrDisplayOnDesktop == DisplayStopMode.Always
            || (stopXrDisplayOnDesktop == DisplayStopMode.EditorOnly && Application.isEditor);

        private bool _wantXr;
        private bool _hasApplied;
        private float _nextReconcile;
        private Camera _resolvedCamera;
        private Camera _sessionKeeper;
        private FlatViewPresenter _presenter;
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
            if (_presenter != null) _presenter.End();
        }

        private void Update()
        {
            if (!ShouldManageDisplay && !manageCameraXrRendering) return;
            if (Time.unscaledTime < _nextReconcile) return;
            _nextReconcile = Time.unscaledTime + reconcileIntervalSeconds;
            if (ShouldManageDisplay) SetDisplayRunning(_wantXr);
            ReconcileSessionKeeper();
            ReconcileFlatView();
        }

        private void ReconcileFlatView()
        {
            var wantPresenter = desktopFlatView == DesktopFlatView.OffscreenPresenter && manageCameraXrRendering
                                && !_wantXr && XrDisplayLifecycle.IsDisplayRunning;
            if (!wantPresenter)
            {
                if (_presenter != null && _presenter.IsPresenting)
                {
                    _presenter.End();
                    if (_isDebug) Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: flat view presenter off.");
                }
                return;
            }

            var cam = ResolveCamera();
            if (cam == null) return;
            if (_presenter == null) _presenter = gameObject.AddComponent<FlatViewPresenter>();
            if (!_presenter.IsPresenting)
            {
                _presenter.Begin(cam);
                if (_isDebug) Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: flat view presenter on ({FlatViewPresenter.WindowSize.x}x{FlatViewPresenter.WindowSize.y}) — the XR display is running.");
            }
            else _presenter.ResizeToWindowIfNeeded();
        }

        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme scheme) => Apply(scheme);

        private void Apply(BroadcastControlsStatus.ControlScheme scheme)
        {
            var wantXr = scheme == BroadcastControlsStatus.ControlScheme.XR;
            var enteringXr = wantXr && (!_hasApplied || !_wantXr);
            if (_isDebug && enteringXr)
                Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: VR entry — display {(XrDisplayLifecycle.IsDisplayRunning ? "running" : "idle")}, session focused={XrDisplayLifecycle.SessionFocused}.");
            _wantXr = wantXr;
            _hasApplied = true;

            ApplyCameraXrRendering();
            if (ShouldManageDisplay) SetDisplayRunning(_wantXr);
            ReconcileSessionKeeper();
            ReconcileFlatView();
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

        private void SetDisplayRunning(bool shouldRun)
        {
            if (!XrDisplayLifecycle.HasDisplay)
            {
                if (shouldRun && _isDebug)
                    Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} XrModeManager: entering XR but no XR display subsystem exists — " +
                        "is a headset connected and Initialize XR on Startup enabled?", this);
                return;
            }

            if (shouldRun)
            {
                if (XrDisplayLifecycle.StartDisplay() && _isDebug)
                    Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: XR display started (VR view).");
            }
            else
            {
                if (XrDisplayLifecycle.StopDisplay() && _isDebug)
                    Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: XR display stopped (flat desktop view).");
            }
        }

        private void ReconcileSessionKeeper()
        {
            var wantKeeper = keepXrSessionAlive && manageCameraXrRendering && !ShouldManageDisplay && !_wantXr
                             && XrDisplayLifecycle.HasDisplay;

            if (wantKeeper && !XrDisplayLifecycle.IsDisplayRunning)
            {
                if (XrDisplayLifecycle.StartDisplay() && _isDebug)
                    Debug.Log($"{XrStartupDiagnostics.LogPrefix} XrModeManager: started the XR display on desktop to warm the session (keeper).");
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
