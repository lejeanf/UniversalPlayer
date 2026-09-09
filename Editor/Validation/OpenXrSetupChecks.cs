using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace jeanf.universalplayer
{
    /// <summary>
    /// OpenXR configuration the Universal Player is validated against (interaction
    /// profiles, render settings, package version). Informative only: every result
    /// explains the trade-off and where the setting lives, but nothing is enforced —
    /// a project targeting other hardware may deliberately diverge. Motivated by a
    /// week lost to Quest 3 hands that never tracked because the Meta Quest Touch
    /// Plus profile was enabled (see CheckProfileChoice).
    /// </summary>
    public static class OpenXrSetupChecks
    {
        public const string ProfileChoiceCheck = "OpenXR profile choice";
        public const string RenderSettingsCheck = "OpenXR render settings";
        public const string PackageVersionCheck = "OpenXR package version";

        internal const string OpenXrPackageName = "com.unity.xr.openxr";
        internal static readonly Version MinimumKnownGoodOpenXr = new Version(1, 18, 0);

        private const string SettingsPage = "Project Settings > XR Plug-in Management > OpenXR";

        /// <summary>
        /// Profiles that make Quest 3 / 3S controllers enumerate as 'Meta Quest Touch Plus/Pro
        /// Controller OpenXR' devices. On Unity 6000.4 (OpenXR 1.17–1.18) those devices come up
        /// WITHOUT their LeftHand/RightHand usages, so every hand binding resolves to nothing:
        /// the head tracks, the hands stay on the floor. Without these profiles the Meta runtime
        /// falls back to the Oculus Touch profile, which works for Rift, Quest 2 and Quest 3 alike.
        /// </summary>
        internal static readonly Type[] RiskyProfiles =
        {
            typeof(MetaQuestTouchPlusControllerProfile),
            typeof(MetaQuestTouchProControllerProfile),
        };

        public static IEnumerable<SetupValidator.CheckResult> RunOpenXrChecks(BuildTargetGroup buildTargetGroup)
        {
            yield return CheckProfileChoice(buildTargetGroup);
            yield return CheckRenderSettings(buildTargetGroup);
            yield return CheckPackageVersion();
        }

        // ---- interaction profiles -------------------------------------------------------

        internal static SetupValidator.CheckResult CheckProfileChoice(BuildTargetGroup buildTargetGroup)
        {
            if (!TryGetSettings(buildTargetGroup, ProfileChoiceCheck, out var settings, out var skipped))
                return skipped;

            var enabled = settings.GetFeatures<OpenXRInteractionFeature>()
                .Where(f => f != null && f.enabled)
                .Select(f => f.GetType())
                .ToArray();
            return EvaluateProfileChoice(enabled, buildTargetGroup);
        }

        /// <summary>Pure evaluation, public so the editor tests can cover every branch without OpenXR settings (no InternalsVisibleTo in this package).</summary>
        public static SetupValidator.CheckResult EvaluateProfileChoice(IReadOnlyCollection<Type> enabledProfiles,
            BuildTargetGroup buildTargetGroup = BuildTargetGroup.Standalone)
        {
            if (enabledProfiles.Count == 0)
                return new SetupValidator.CheckResult(ProfileChoiceCheck, SetupValidator.Severity.Warning,
                    "No interaction profile enabled — skipped (see 'OpenXR interaction profiles').");

            var where = $"{SettingsPage} > {buildTargetGroup} tab > Enabled Interaction Profiles";
            var risky = enabledProfiles.Where(RiskyProfiles.Contains).Select(t => t.Name).ToArray();
            if (risky.Length > 0)
                return new SetupValidator.CheckResult(ProfileChoiceCheck, SetupValidator.Severity.Warning,
                    $"{string.Join(" + ", risky)} enabled — Quest 3 controllers then bind through that profile, and on " +
                    "Unity 6000.4 those devices get NO LeftHand/RightHand usage: the head tracks but the hands never move. " +
                    "Rays, poses and buttons all depend on that usage.",
                    $"{where} → untick the Meta Quest Touch Plus/Pro profiles unless you need their exclusive controls; " +
                    "keep 'Oculus Touch Controller Profile', which every Meta headset falls back to (the setup the player is validated with).");

            if (!enabledProfiles.Contains(typeof(OculusTouchControllerProfile)))
                return new SetupValidator.CheckResult(ProfileChoiceCheck, SetupValidator.Severity.Warning,
                    $"'Oculus Touch Controller Profile' is not enabled (enabled: {string.Join(", ", enabledProfiles.Select(t => t.Name))}) — " +
                    "Rift and Quest via Link will not have controllers unless another profile covers them.",
                    $"{where} → add 'Oculus Touch Controller Profile' (fine to skip if the project never targets Meta headsets).");

            return new SetupValidator.CheckResult(ProfileChoiceCheck, SetupValidator.Severity.Pass,
                "Oculus Touch profile without Meta Quest Touch Plus/Pro — the configuration the player is validated with.");
        }

        // ---- render settings ------------------------------------------------------------

        internal static SetupValidator.CheckResult CheckRenderSettings(BuildTargetGroup buildTargetGroup)
        {
            if (!TryGetSettings(buildTargetGroup, RenderSettingsCheck, out var settings, out var skipped))
                return skipped;
            return EvaluateRenderSettings(settings.renderMode, settings.latencyOptimization, buildTargetGroup);
        }

        public static SetupValidator.CheckResult EvaluateRenderSettings(OpenXRSettings.RenderMode renderMode,
            OpenXRSettings.LatencyOptimization latencyOptimization,
            BuildTargetGroup buildTargetGroup = BuildTargetGroup.Standalone)
        {
            var differences = new List<string>();
            if (renderMode != OpenXRSettings.RenderMode.SinglePassInstanced)
                differences.Add($"Render Mode = {renderMode} (reference: Single Pass Instanced)");
            if (latencyOptimization != OpenXRSettings.LatencyOptimization.PrioritizeRendering)
                differences.Add($"Latency Optimization = {latencyOptimization} (reference: Prioritize Rendering)");

            if (differences.Count == 0)
                return new SetupValidator.CheckResult(RenderSettingsCheck, SetupValidator.Severity.Pass,
                    "Single Pass Instanced + Prioritize Rendering — matches the reference project.");

            return new SetupValidator.CheckResult(RenderSettingsCheck, SetupValidator.Severity.Warning,
                $"{string.Join("; ", differences)} — legitimate values, but the player is only validated with the reference " +
                "ones; when the headset shows nothing while the game view still renders, align these first.",
                $"{SettingsPage} > {buildTargetGroup} tab → Render Mode 'Single Pass Instanced', Latency Optimization " +
                "'Prioritize Rendering' (or keep your values knowingly).");
        }

        // ---- package version ------------------------------------------------------------

        internal static SetupValidator.CheckResult CheckPackageVersion()
        {
            var package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .FirstOrDefault(p => p.name == OpenXrPackageName);
            return EvaluatePackageVersion(package?.version);
        }

        public static SetupValidator.CheckResult EvaluatePackageVersion(string version)
        {
            if (string.IsNullOrEmpty(version))
                return new SetupValidator.CheckResult(PackageVersionCheck, SetupValidator.Severity.Warning,
                    $"{OpenXrPackageName} is not installed — skipped (fine if another provider is used).");

            if (!TryParseVersion(version, out var parsed))
                return new SetupValidator.CheckResult(PackageVersionCheck, SetupValidator.Severity.Warning,
                    $"Could not parse {OpenXrPackageName} version '{version}' — skipped.");

            if (parsed < MinimumKnownGoodOpenXr)
                return new SetupValidator.CheckResult(PackageVersionCheck, SetupValidator.Severity.Warning,
                    $"{OpenXrPackageName} {version} — 1.17.x ships a documented regression (OpenXR devices lose their " +
                    $"LeftHand/RightHand usages, fixed in {MinimumKnownGoodOpenXr}); the player is validated on {MinimumKnownGoodOpenXr}+.",
                    $"Package Manager → update {OpenXrPackageName} to {MinimumKnownGoodOpenXr} or newer, then RESTART the editor " +
                    "(input devices do not survive an XR package swap in a live session).");

            return new SetupValidator.CheckResult(PackageVersionCheck, SetupValidator.Severity.Pass,
                $"{OpenXrPackageName} {version}.");
        }

        /// <summary>Accepts registry versions such as '1.18.0' or '1.18.0-pre.2' (pre-release tag dropped).</summary>
        public static bool TryParseVersion(string version, out Version parsed)
        {
            parsed = null;
            if (string.IsNullOrEmpty(version)) return false;
            var core = version.Split('-', '+')[0];
            return Version.TryParse(core, out parsed);
        }

        // ---- shared ---------------------------------------------------------------------

        private static bool TryGetSettings(BuildTargetGroup buildTargetGroup, string checkName,
            out OpenXRSettings settings, out SetupValidator.CheckResult skipped)
        {
            skipped = default;
            try
            {
                settings = OpenXRSettings.GetSettingsForBuildTargetGroup(buildTargetGroup);
            }
            catch (Exception e)
            {
                settings = null;
                skipped = new SetupValidator.CheckResult(checkName, SetupValidator.Severity.Warning,
                    $"Could not read OpenXR settings ({e.GetType().Name}) — skipped; is the OpenXR package healthy?");
                return false;
            }

            if (settings != null) return true;
            skipped = new SetupValidator.CheckResult(checkName, SetupValidator.Severity.Warning,
                $"No OpenXR settings for '{buildTargetGroup}' — skipped (fine if another provider is used).");
            return false;
        }
    }
}
