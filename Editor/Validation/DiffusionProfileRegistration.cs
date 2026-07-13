using System.Linq;
using UnityEditor;
using UnityEngine;
#if UNIVERSALPLAYER_HDRP
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#endif

namespace jeanf.universalplayer
{
    /// <summary>
    /// HDRP renders materials whose diffusion profile is not registered with a wrong,
    /// pinkish skin tone — silently. The check (also part of ValidateSetup) verifies
    /// every diffusion profile shipped in this package is referenced by the Diffusion
    /// Profile List of HDRP's default volume profile, and the menu action registers
    /// the missing ones in one click. Compiled out entirely in URP-only projects.
    /// </summary>
    public static class DiffusionProfileRegistration
    {
        /// <summary>Validator entry — always returns a result so ValidateSetup can report the area in every project.</summary>
        public static SetupValidator.CheckResult RunCheck()
        {
#if UNIVERSALPLAYER_HDRP
            var packageProfiles = FindPackageProfiles();
            if (packageProfiles.Length == 0)
                return new SetupValidator.CheckResult("HDRP diffusion profiles", SetupValidator.Severity.Pass,
                    "No diffusion profile assets found in the package — nothing to register.");

            var registered = RegisteredProfiles();
            if (registered == null)
                return new SetupValidator.CheckResult("HDRP diffusion profiles", SetupValidator.Severity.Warning,
                    "Could not read HDRP's default volume profile — skipped (open Project Settings > Graphics > HDRP once, then re-run).");

            var missing = packageProfiles.Where(profile => !registered.Contains(profile)).ToArray();
            if (missing.Length == 0)
                return new SetupValidator.CheckResult("HDRP diffusion profiles", SetupValidator.Severity.Pass,
                    $"All {packageProfiles.Length} package diffusion profile(s) are registered.");

            var hdrpActive = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null &&
                             UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.GetType().Name.Contains("HDRenderPipeline");
            var names = string.Join(", ", missing.Select(profile => profile.name));
            return new SetupValidator.CheckResult("HDRP diffusion profiles",
                hdrpActive ? SetupValidator.Severity.Fail : SetupValidator.Severity.Warning,
                $"Unregistered diffusion profile(s): {names} — skin/gloves materials render with a wrong pinkish tone in HDRP.",
                "Run Tools/UniversalPlayer/Register HDRP Diffusion Profiles (one click), or add them manually to the " +
                "Diffusion Profile List of the default volume profile (Project Settings > Graphics > HDRP > Default Volume Profile).");
#else
            return new SetupValidator.CheckResult("HDRP diffusion profiles", SetupValidator.Severity.Pass,
                "HDRP is not installed — check skipped.");
#endif
        }

#if UNIVERSALPLAYER_HDRP
        [MenuItem("Tools/UniversalPlayer/Register HDRP Diffusion Profiles")]
        public static void RegisterPackageProfiles()
        {
            var packageProfiles = FindPackageProfiles();
            if (packageProfiles.Length == 0)
            {
                Debug.Log($"{XrStartupDiagnostics.LogPrefix} No diffusion profiles found in the package — nothing to register.");
                return;
            }

            var volumeProfile = DefaultVolumeProfile();
            if (volumeProfile == null)
            {
                Debug.LogError($"{XrStartupDiagnostics.LogPrefix} HDRP's default volume profile is not available — " +
                    "open Project Settings > Graphics > HDRP once so HDRP creates it, then run this again.");
                return;
            }

            if (!volumeProfile.TryGet<DiffusionProfileList>(out var list))
                list = volumeProfile.Add<DiffusionProfileList>(true);

            var current = list.diffusionProfiles.value ?? new DiffusionProfileSettings[0];
            var missing = packageProfiles.Where(profile => !current.Contains(profile)).ToArray();
            if (missing.Length == 0)
            {
                Debug.Log($"{XrStartupDiagnostics.LogPrefix} All package diffusion profiles are already registered.");
                return;
            }

            var merged = current.Where(profile => profile != null).Concat(missing).ToArray();
            // slot 0 is reserved for the neutral profile, so 15 usable slots
            if (merged.Length > 15)
            {
                Debug.LogError($"{XrStartupDiagnostics.LogPrefix} Cannot register: the Diffusion Profile List would hold {merged.Length} entries " +
                    "but HDRP supports at most 15 (+1 neutral). Remove unused profiles from the list first.");
                return;
            }

            list.diffusionProfiles.value = merged;
            list.diffusionProfiles.overrideState = true;
            EditorUtility.SetDirty(list);
            EditorUtility.SetDirty(volumeProfile);
            AssetDatabase.SaveAssets();

            Debug.Log($"{XrStartupDiagnostics.LogPrefix} Registered {missing.Length} diffusion profile(s) on '{volumeProfile.name}': " +
                $"{string.Join(", ", missing.Select(profile => profile.name))}. Hands should lose their pinkish tint now.");
        }

        private static DiffusionProfileSettings[] FindPackageProfiles()
        {
            var packageRoot = PackageRoot();
            return AssetDatabase.FindAssets("t:DiffusionProfileSettings")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => packageRoot == null || path.StartsWith(packageRoot))
                .Select(AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>)
                .Where(profile => profile != null)
                .ToArray();
        }

        private static DiffusionProfileSettings[] RegisteredProfiles()
        {
            var volumeProfile = DefaultVolumeProfile();
            if (volumeProfile == null) return null;
            if (!volumeProfile.TryGet<DiffusionProfileList>(out var list)) return new DiffusionProfileSettings[0];
            return list.diffusionProfiles.value ?? new DiffusionProfileSettings[0];
        }

        private static VolumeProfile DefaultVolumeProfile()
        {
            var settings = UnityEngine.Rendering.GraphicsSettings.GetRenderPipelineSettings<HDRPDefaultVolumeProfileSettings>();
            return settings?.volumeProfile;
        }

        /// <summary>Package root ("Assets/UniversalPlayer" or "Packages/fr.jeanf.universal.player"), located via the runtime asmdef.</summary>
        private static string PackageRoot()
        {
            var guids = AssetDatabase.FindAssets("jeanf.universalplayer t:AssemblyDefinitionAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("Runtime/scripts/jeanf.universalplayer.asmdef")) continue;
                return path.Substring(0, path.Length - "Runtime/scripts/jeanf.universalplayer.asmdef".Length).TrimEnd('/');
            }
            return null;
        }
#endif
    }
}
