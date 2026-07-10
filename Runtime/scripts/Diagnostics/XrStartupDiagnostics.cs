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
