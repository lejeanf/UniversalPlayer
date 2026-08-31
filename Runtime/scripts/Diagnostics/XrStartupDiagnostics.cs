using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Logs one console report when play starts, explaining whether XR is running
    /// and — when it is not — the most likely reasons why (no provider enabled,
    /// Link/Air Link not connected, wrong active OpenXR runtime, init-on-startup off).
    /// The report is logged AGAIN on every switch into VR (the state that matters
    /// there — controllers registered, input actions bound — may have changed since
    /// launch), then including the INPUT-ACTION layer: the enabled/bound state of
    /// the hand Position and DrawPrimaryItem actions, which separates "the runtime
    /// delivered no controller device" from "the device is there but the actions
    /// never bound" when hands stop tracking or buttons stop responding.
    /// Runs automatically, no scene setup required. For ongoing monitoring
    /// (disconnects, battery) add <see cref="XrHealthMonitor"/> to the player.
    /// </summary>
    public static class XrStartupDiagnostics
    {
        public const string LogPrefix = "[UniversalPlayer.XR]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ReportOnStartup()
        {
            var report = BuildReport(out var xrRunning);
            if (xrRunning) Debug.Log(report);
            else Debug.LogWarning(report);

            // Statics survive when Enter Play Mode skips the domain reload — always
            // re-subscribe from a clean slate.
            BroadcastControlsStatus.SendControlScheme -= OnControlSchemeChanged;
            BroadcastControlsStatus.SendControlScheme += OnControlSchemeChanged;
        }

        private static void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme scheme)
        {
            if (scheme != BroadcastControlsStatus.ControlScheme.XR) return;
            var report = BuildReport(out var xrRunning) + BuildInputActionReport();
            if (xrRunning) Debug.Log(report);
            else Debug.LogWarning(report);
        }

        /// <summary>
        /// The INPUT-ACTION layer of the report: enabled/bound state of the actions
        /// VR hands live on. boundControls = 0 with a controller device present means
        /// the binding never resolved (wrong asset, map disabled, missing profile).
        /// </summary>
        public static string BuildInputActionReport()
        {
            var sb = new StringBuilder();

            // Input System DEVICE layer first: layout + usages. Controllers arriving
            // as auto-built fallback layouts ("XRInputV1::...") with EMPTY usages
            // cannot match any '{LeftHand}/{RightHand}' binding — hands freeze and
            // buttons go dead while the device list looks superficially fine.
            sb.Append("• InputSystem XR devices: ");
            var anyDevice = false;
            foreach (var device in UnityEngine.InputSystem.InputSystem.devices)
            {
                if (!(device is UnityEngine.InputSystem.XR.XRController)
                    && !(device is UnityEngine.InputSystem.XR.XRHMD)) continue;
                sb.Append($"[{device.layout} usages:{string.Join("/", device.usages)}] ");
                anyDevice = true;
            }
            sb.AppendLine(anyDevice ? "" : "NONE — the runtime delivered no XR input devices.");

            var manager = Object.FindAnyObjectByType<UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager>(
                FindObjectsInactive.Include);
            var asset = manager != null && manager.actionAssets != null && manager.actionAssets.Count > 0
                ? manager.actionAssets[0]
                : null;
            if (asset == null)
                return $"• No XRI InputActionManager with an action asset found — XRI actions are never enabled.\n";

            foreach (var path in new[]
                     {
                         "XRI LeftHand/Position", "XRI RightHand/Position",
                         "XRI LeftHand/DrawPrimaryItem", "XRI RightHand/DrawPrimaryItem",
                     })
            {
                var action = asset.FindAction(path);
                sb.AppendLine(action == null
                    ? $"✗ Action '{path}' not found on '{asset.name}'."
                    : $"{(action.enabled && action.controls.Count > 0 ? "✓" : "✗")} {path}: enabled={action.enabled}, boundControls={action.controls.Count}");
            }
            return sb.ToString();
        }

        /// <summary>Builds the human-readable XR state report. xrRunning is true when a loader started and an HMD is registered.</summary>
        public static string BuildReport(out bool xrRunning)
        {
            xrRunning = false;
            var sb = new StringBuilder();
            sb.AppendLine($"{LogPrefix} XR startup report:");

            var settings = XRGeneralSettings.Instance;
            if (settings == null || settings.Manager == null)
            {
                sb.AppendLine("✗ XR Plug-in Management has no settings for this platform — VR cannot start.");
                sb.AppendLine("→ Fix: Project Settings > XR Plug-in Management > enable a provider (OpenXR).");
                sb.AppendLine("   (Ignore this if the project is Mouse&Keyboard only.)");
                return sb.ToString();
            }

            var manager = settings.Manager;
            if (manager.activeLoader == null)
            {
                sb.AppendLine("✗ No XR loader is running — falling back to flat (Mouse&Keyboard) mode.");
                if (manager.activeLoaders == null || manager.activeLoaders.Count == 0)
                {
                    sb.AppendLine("→ Fix: no provider is enabled. Project Settings > XR Plug-in Management > tick OpenXR.");
                }
                else
                {
                    var configured = string.Join(", ", manager.activeLoaders.Where(l => l != null).Select(l => l.name));
                    sb.AppendLine($"→ Providers configured ({configured}) but none could start. Usual suspects:");
                    sb.AppendLine("   • Headset not connected: Quest Link/Air Link not active, cable unplugged, or headset asleep.");
                    sb.AppendLine("   • Wrong active OpenXR runtime: Oculus app > Settings > General > 'Set Oculus as active OpenXR runtime'");
                    sb.AppendLine("     (or SteamVR > Settings > OpenXR when using SteamVR).");
                    if (!settings.InitManagerOnStart)
                        sb.AppendLine("   • 'Initialize XR on Startup' is OFF in XR Plug-in Management — XR must then be started from code.");
                }
                return sb.ToString();
            }

            sb.AppendLine($"✓ XR loader running: {manager.activeLoader.name}");
            if (manager.activeLoader is OpenXRLoaderBase)
                sb.AppendLine($"✓ Active OpenXR runtime: {OpenXRRuntime.name} {OpenXRRuntime.version}");

            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!hmd.isValid)
            {
                sb.AppendLine("✗ Loader started but no HMD device is registered — headset asleep, proximity sensor covered, or Link dropped right after init.");
                sb.AppendLine("→ Fix: wake the headset (put it on), then check the Link/Air Link connection in the Oculus app.");
                return sb.ToString();
            }

            xrRunning = true;
            sb.AppendLine($"✓ HMD detected: {hmd.name}");

            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            sb.AppendLine(left.isValid
                ? $"✓ Left controller: {left.name}"
                : "• Left controller not detected — powered off, battery dead, or hand tracking in use.");
            sb.AppendLine(right.isValid
                ? $"✓ Right controller: {right.name}"
                : "• Right controller not detected — powered off, battery dead, or hand tracking in use.");

            return sb.ToString();
        }
    }
}
