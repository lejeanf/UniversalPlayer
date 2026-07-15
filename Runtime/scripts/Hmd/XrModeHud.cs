using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Debug-only diagnostics for control-mode + VR: an on-screen HUD (Ctrl+Alt+H) AND
    /// structured logging. It logs a full snapshot to the Console on every mode change
    /// (so entering/leaving VR is captured automatically) and on demand with Ctrl+Alt+J
    /// (to capture the exact state WHILE a bug is happening — e.g. walk dead, headset
    /// black). The snapshot covers the movement pipeline, the input bindings, and the
    /// camera / XR-display state, so failures are read off, not guessed at.
    ///
    /// Renders to the desktop mirror; ships disabled overlay but logging is always live
    /// (it's a package debug tool). OnGUI/log string building allocates a little — fine.
    /// </summary>
    public class XrModeHud : MonoBehaviour
    {
        [Tooltip("Draw the overlay. Also toggleable at runtime with Ctrl+Alt+H.")]
        [SerializeField] private bool showOverlay = false;
        [SerializeField] private bool allowRuntimeToggle = true;
        [Tooltip("Log a full state snapshot to the Console on every mode change and on Ctrl+Alt+J.")]
        [SerializeField] private bool logSnapshots = true;

        private BroadcastControlsStatus _broadcaster;
        private PlayerMovement _movement;
        private FPSCameraMovement _camera;
        private NoPeeking _noPeeking;
        private static readonly List<XRDisplaySubsystem> _displays = new List<XRDisplaySubsystem>();

        // A mode change captures TWO snapshots: one immediately (transition), and one a
        // moment later once camera/rig setup has settled (the steady-state truth — the
        // immediate one shows stale pre-setup camera state).
        private float _settledLogAt = -1f;
        private string _settledTrigger;

        private void OnEnable()
        {
            BroadcastControlsStatus.SendControlScheme += OnModeChanged;
        }

        private void OnDisable()
        {
            BroadcastControlsStatus.SendControlScheme -= OnModeChanged;
        }

        private void OnModeChanged(BroadcastControlsStatus.ControlScheme mode)
        {
            if (!logSnapshots) return;
            LogSnapshot($"mode -> {mode} (transition)");
            _settledLogAt = Time.unscaledTime + 0.75f;
            _settledTrigger = $"mode {mode} (SETTLED +0.75s)";
        }

        private void Update()
        {
            if (logSnapshots && _settledLogAt > 0f && Time.unscaledTime >= _settledLogAt)
            {
                _settledLogAt = -1f;
                LogSnapshot(_settledTrigger);
            }

            var kb = Keyboard.current;
            if (kb == null) return;
            var ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            var alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;

            if (allowRuntimeToggle && kb.hKey.wasPressedThisFrame && ctrl && alt)
                showOverlay = !showOverlay;

            if (logSnapshots && kb.jKey.wasPressedThisFrame && ctrl && alt)
                LogSnapshot("manual (Ctrl+Alt+J)");
        }

        private void EnsureRefs()
        {
            if (_broadcaster == null) _broadcaster = FindObjectOfType<BroadcastControlsStatus>();
            if (_movement == null) _movement = FindObjectOfType<PlayerMovement>();
            if (_camera == null) _camera = FindObjectOfType<FPSCameraMovement>();
            if (_noPeeking == null) _noPeeking = FindObjectOfType<NoPeeking>();
        }

        private void LogSnapshot(string trigger)
        {
            EnsureRefs();
            var sb = new StringBuilder(512);
            sb.Append("[MODE-DIAG] === ").Append(trigger).Append(" ===\n");
            sb.Append("mode: controlScheme=").Append(BroadcastControlsStatus.controlScheme)
              .Append(" inVr=").Append(_broadcaster != null ? _broadcaster.InVr.ToString() : "?").Append('\n');

            if (_movement != null) sb.Append(_movement.DiagnosticSnapshot()).Append('\n');
            else sb.Append("movement: <no PlayerMovement>\n");

            AppendCameraState(sb);
            AppendXrState(sb);

            if (_noPeeking != null)
                sb.Append("fade(NoPeeking): headInWall=").Append(_noPeeking.GetFadeState())
                  .Append(" sceneLoading=").Append(NoPeeking.IsCurrentlyLoading());

            Debug.Log(sb.ToString());
        }

        private void AppendCameraState(StringBuilder sb)
        {
            var cam = _camera != null ? _camera.playerCamera : null;
            if (cam != null)
            {
                var tpd = cam.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                sb.Append("camera: '").Append(cam.name).Append("' enabled=").Append(cam.enabled)
                  .Append(" activeAndEnabled=").Append(cam.isActiveAndEnabled)
                  .Append(" stereoEnabled=").Append(cam.stereoEnabled)
                  .Append(" stereoTargetEye=").Append(cam.stereoTargetEye)
                  .Append(" targetTexture=").Append(cam.targetTexture != null ? cam.targetTexture.name : "null")
                  .Append(" cullingMask=0x").Append(cam.cullingMask.ToString("X"))
                  .Append(" nearClip=").Append(cam.nearClipPlane)
                  .Append(" worldPos=").Append(cam.transform.position)
                  .Append(" tpd=").Append(tpd != null ? (tpd.enabled ? "ON" : "off") : "none").Append('\n');
                sb.Append("cameraComponents: ");
                foreach (var comp in cam.GetComponents<Component>())
                    if (comp != null) sb.Append(comp.GetType().Name).Append(' ');
                sb.Append('\n');
            }
            else sb.Append("camera: <no FPSCameraMovement.playerCamera>\n");

            var all = Camera.allCameras;
            sb.Append("activeCameras(").Append(all.Length).Append("): ");
            for (var i = 0; i < all.Length; i++)
                sb.Append(all[i].name).Append("(en=").Append(all[i].enabled).Append(",depth=").Append(all[i].depth).Append(") ");
            sb.Append('\n');
        }

        private static void AppendXrState(StringBuilder sb)
        {
            SubsystemManager.GetSubsystems(_displays);
            var display = _displays.Count > 0 ? _displays[0].running.ToString() : "no-display-subsystem";
            var displayOpaque = _displays.Count > 0 ? _displays[0].displayOpaque.ToString() : "?";
            var pipeline = GraphicsSettings.currentRenderPipeline;
            sb.Append("XR: settings.enabled=").Append(XRSettings.enabled)
              .Append(" isDeviceActive=").Append(XRSettings.isDeviceActive)
              .Append(" stereoMode=").Append(XRSettings.stereoRenderingMode)
              .Append(" device='").Append(XRSettings.loadedDeviceName)
              .Append("' displayRunning=").Append(display)
              .Append(" displayOpaque=").Append(displayOpaque)
              .Append(" renderScale=").Append(XRSettings.eyeTextureResolutionScale).Append('\n');
            sb.Append("pipeline: ").Append(pipeline != null ? pipeline.GetType().Name : "Built-in").Append('\n');
        }

        private void OnGUI()
        {
            if (!showOverlay) return;
            EnsureRefs();

            var d = DetectUserPresence.ReadDiagnostics();
            var style = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                padding = new RectOffset(10, 10, 8, 8),
                normal = { textColor = Color.white }
            };

            var scheme = BroadcastControlsStatus.controlScheme.ToString();
            var inVr = _broadcaster != null ? _broadcaster.InVr.ToString() : "?";
            var grounded = _movement != null ? _movement.IsGrounded.ToString() : "?";
            var move = _movement != null ? $"moveInput={_movement.MoveInput} isMoving={_movement.IsMoving} ctrlEnabled={_movement.ControllerEnabled}" : "?";
            var fade = _noPeeking != null
                ? $"headInWall={_noPeeking.GetFadeState()} sceneLoading={NoPeeking.IsCurrentlyLoading()}"
                : "?";

            var text =
                "XR MODE HUD  (Ctrl+Alt+H)   log: Ctrl+Alt+J\n" +
                $"scheme={scheme}  inVr={inVr}  grounded={grounded}\n" +
                $"{move}\nfade(NoPeeking): {fade}\n" +
                "\nLegacy InputDevices:\n" +
                $"  present={d.LegacyPresent}  tracked={d.LegacyTracked}  presenceSupported={d.LegacyPresenceSupported}\n" +
                "Input System XRHMD:\n" +
                $"  present={d.InputSystemPresent}  tracked={d.InputSystemTracked}  presenceSupported={d.InputSystemPresenceSupported}\n" +
                "\nEnter VR: controller button / Ctrl+Alt+V   Leave: keyboard/mouse / Ctrl+Alt+K";

            GUI.Box(new Rect(10, 10, 620, 260), text, style);
        }
    }
}
