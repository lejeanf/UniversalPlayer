using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEditor.XR.Management;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Tools/UniversalPlayer/ValidateSetup — checks the project configuration the
    /// Universal Player depends on and prints actionable console feedback for every
    /// problem (what broke, likely cause, where to fix it). Covers the recurring VR
    /// issues: provider not enabled, no interaction profiles (controllers undetected),
    /// Link focus loss, missing render pipeline, wrong input handling.
    /// </summary>
    public static class SetupValidator
    {
        public enum Severity { Pass, Warning, Fail }

        public readonly struct CheckResult
        {
            public readonly string Name;
            public readonly Severity Severity;
            public readonly string Message;
            public readonly string Hint;

            public CheckResult(string name, Severity severity, string message, string hint = "")
            {
                Name = name;
                Severity = severity;
                Message = message;
                Hint = hint;
            }
        }

        private const string LogPrefix = "[UniversalPlayer.Validate]";

        [MenuItem("Tools/UniversalPlayer/ValidateSetup")]
        public static void ValidateSetup()
        {
            var results = RunProjectConfigChecks();
            results.AddRange(ProjectSetupChecks.RunAssetChecks());
            results.AddRange(ProjectSetupChecks.RunOpenSceneChecks());
            LogResults(results);
        }

        /// <summary>Runs all project-configuration checks. Kept UI-free so tests and CI can call it.</summary>
        public static List<CheckResult> RunProjectConfigChecks()
        {
            var results = new List<CheckResult>();
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

            results.Add(CheckInputSystem());
            results.Add(CheckRenderPipeline());
            results.AddRange(CheckXrManagement(buildTargetGroup));
            results.Add(CheckOpenXrInteractionProfiles(buildTargetGroup));
            results.Add(CheckRunInBackground());
            results.Add(DiffusionProfileRegistration.RunCheck());

            return results;
        }

        private static CheckResult CheckInputSystem()
        {
#if ENABLE_INPUT_SYSTEM
            return new CheckResult("Input System", Severity.Pass, "Input System package is the active input handler.");
#else
            return new CheckResult("Input System", Severity.Fail,
                "The Input System package is not active — the player's input (and XRI) will not work.",
                "Project Settings > Player > Other Settings > Active Input Handling → 'Input System Package' (or 'Both').");
#endif
        }

        private static CheckResult CheckRenderPipeline()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null)
                return new CheckResult("Render pipeline", Severity.Fail,
                    "No scriptable render pipeline is active (built-in) — FadeMask/NoPeeking need URP or HDRP.",
                    "Project Settings > Graphics → assign a URP or HDRP pipeline asset.");

            var typeName = pipeline.GetType().Name;
            if (typeName.Contains("Universal") || typeName.Contains("HDRenderPipeline"))
                return new CheckResult("Render pipeline", Severity.Pass, $"Active pipeline: {typeName}.");

            return new CheckResult("Render pipeline", Severity.Fail,
                $"Unknown render pipeline '{typeName}' — the fade system only supports URP and HDRP.",
                "Use a URP or HDRP pipeline asset in Project Settings > Graphics.");
        }

        private static IEnumerable<CheckResult> CheckXrManagement(BuildTargetGroup buildTargetGroup)
        {
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey,
                out XRGeneralSettingsPerBuildTarget perBuildTarget);
            var settings = perBuildTarget != null ? perBuildTarget.SettingsForBuildTarget(buildTargetGroup) : null;

            if (settings == null || settings.Manager == null)
            {
                yield return new CheckResult("XR Plug-in Management", Severity.Fail,
                    $"No XR settings exist for build target '{buildTargetGroup}' — VR cannot start.",
                    "Project Settings > XR Plug-in Management → install/initialize it and tick a provider (OpenXR).");
                yield break;
            }

            var loaders = settings.Manager.activeLoaders;
            if (loaders == null || loaders.Count == 0)
            {
                yield return new CheckResult("XR provider", Severity.Fail,
                    $"No XR provider is enabled for '{buildTargetGroup}' — VR will never be detected.",
                    "Project Settings > XR Plug-in Management → tick OpenXR for this platform.");
            }
            else
            {
                var names = string.Join(", ", loaders.Where(l => l != null).Select(l => l.name));
                yield return new CheckResult("XR provider", Severity.Pass, $"Enabled provider(s): {names}.");
            }

            if (!settings.InitManagerOnStart)
            {
                yield return new CheckResult("XR init on startup", Severity.Warning,
                    "'Initialize XR on Startup' is OFF — VR only starts if the project starts it from code.",
                    "Project Settings > XR Plug-in Management → tick 'Initialize XR on Startup' (unless the project initializes XR manually).");
            }
            else
            {
                yield return new CheckResult("XR init on startup", Severity.Pass, "XR initializes on startup.");
            }
        }

        private static CheckResult CheckOpenXrInteractionProfiles(BuildTargetGroup buildTargetGroup)
        {
            OpenXRSettings openXrSettings;
            try
            {
                openXrSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(buildTargetGroup);
            }
            catch (Exception e)
            {
                return new CheckResult("OpenXR interaction profiles", Severity.Warning,
                    $"Could not read OpenXR settings ({e.GetType().Name}) — is the OpenXR package healthy?",
                    "Reimport com.unity.xr.openxr or check the console for OpenXR errors.");
            }

            if (openXrSettings == null)
                return new CheckResult("OpenXR interaction profiles", Severity.Warning,
                    $"No OpenXR settings for '{buildTargetGroup}' — skipped (fine if another provider is used).");

            var enabledProfiles = openXrSettings.GetFeatures<OpenXRInteractionFeature>()
                .Where(f => f != null && f.enabled)
                .Select(f => f.name)
                .ToArray();

            if (enabledProfiles.Length == 0)
                return new CheckResult("OpenXR interaction profiles", Severity.Fail,
                    "No OpenXR interaction profile is enabled — the headset may connect but CONTROLLERS WILL NOT BE DETECTED.",
                    "Project Settings > XR Plug-in Management > OpenXR > Enabled Interaction Profiles → add 'Oculus Touch Controller Profile' (plus any other controllers you target).");

            return new CheckResult("OpenXR interaction profiles", Severity.Pass,
                $"Enabled profile(s): {string.Join(", ", enabledProfiles)}.");
        }

        private static CheckResult CheckRunInBackground()
        {
            if (PlayerSettings.runInBackground)
                return new CheckResult("Run in background", Severity.Pass, "Application keeps running without focus.");

            return new CheckResult("Run in background", Severity.Warning,
                "'Run In Background' is OFF — with Quest Link, alt-tabbing or losing window focus pauses the app (looks like VR froze).",
                "Project Settings > Player > Resolution and Presentation → tick 'Run In Background'.");
        }

        private static void LogResults(List<CheckResult> results)
        {
            var fails = results.Count(r => r.Severity == Severity.Fail);
            var warnings = results.Count(r => r.Severity == Severity.Warning);

            var sb = new StringBuilder();
            sb.AppendLine($"{LogPrefix} {results.Count} checks — {fails} failed, {warnings} warning(s).");
            foreach (var result in results)
            {
                var icon = result.Severity switch
                {
                    Severity.Pass => "✓",
                    Severity.Warning => "⚠",
                    _ => "✗"
                };
                sb.AppendLine($"{icon} {result.Name}: {result.Message}");
                if (!string.IsNullOrEmpty(result.Hint))
                    sb.AppendLine($"   → Fix: {result.Hint}");
            }

            if (fails > 0) Debug.LogError(sb.ToString());
            else if (warnings > 0) Debug.LogWarning(sb.ToString());
            else Debug.Log(sb.ToString());
        }
    }
}
