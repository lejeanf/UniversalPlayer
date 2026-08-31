using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Footstep checks for Tools/Jeanf/UniversalPlayer/ValidateSetup. The footstep
    /// failure mode is always SILENCE without an error — the component missing from
    /// the variant (how the 1.0 rewrite lost footsteps), wired but with no sound
    /// resources, or surface profiles whose tags do not exist in the project so they
    /// can never match a floor. Every one of those must be loud here.
    /// </summary>
    public static class FootstepSetupChecks
    {
        public static List<SetupValidator.CheckResult> RunFootstepChecks()
        {
            var results = new List<SetupValidator.CheckResult>();

            var footsteps = Object.FindAnyObjectByType<FootstepAudio>(FindObjectsInactive.Include);
            if (footsteps == null)
            {
                results.Add(new SetupValidator.CheckResult("Scene: footsteps", SetupValidator.Severity.Warning,
                    "No FootstepAudio in the open scene — walking is completely silent.",
                    "It ships on the Player prefab (Move/FootstepAudio) since v1.13.0 — update the package, " +
                    "or re-add the component on your Player variant if it was removed."));
                return results;
            }

            results.Add(CheckFootsteps(footsteps));
            results.Add(CheckFootstepSurfaces(footsteps));
            return results;
        }

        /// <summary>Public so the editor tests can exercise each severity path (no InternalsVisibleTo in this package).</summary>
        public static SetupValidator.CheckResult CheckFootsteps(FootstepAudio footsteps)
        {
            const string check = "Scene: footsteps";

            var so = new SerializedObject(footsteps);
            var missing = new List<string>();
            if (so.FindProperty("movement").objectReferenceValue == null) missing.Add("movement");
            if (so.FindProperty("footstepSource").objectReferenceValue == null) missing.Add("footstepSource");
            if (missing.Count > 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Fail,
                    $"FootstepAudio on '{footsteps.gameObject.name}' has unassigned wiring: {string.Join(", ", missing)} — footsteps never play.",
                    "The packaged prefab ships these assigned — re-wire them on your Player variant " +
                    "(movement = the PlayerMovement on the Move object, footstepSource = the FootstepSource child AudioSource).");

            if (!footsteps.HasAnyFootstepSound)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Fail,
                    "FootstepAudio is wired but has NO footstep resource on any surface profile or the default surface — " +
                    "every step is silent (the runtime logs the same warning once, in play mode).",
                    "Assign AudioClips or Audio Random Containers on your Player variant's FootstepAudio. " +
                    "The AudioSystems package samples ship ready-made containers (ARC_FootstepsOnConcrete / ARC_FootstepsOnLinoleum).");

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                $"FootstepAudio wired and audible ({footsteps.ConfiguredSurfaceTags.Count()} tagged surface profile(s) + default).");
        }

        /// <summary>Public so the editor tests can exercise each severity path (no InternalsVisibleTo in this package).</summary>
        public static SetupValidator.CheckResult CheckFootstepSurfaces(FootstepAudio footsteps)
        {
            const string check = "Scene: footstep surfaces";

            var definedTags = new HashSet<string>(InternalEditorUtility.tags);
            var undefined = footsteps.ConfiguredSurfaceTags
                .Where(tag => !definedTags.Contains(tag))
                .Distinct()
                .ToArray();

            var scuffNote = footsteps.HasAnyScuffSound
                ? ""
                : " Also: no scuff resource is assigned anywhere, so the friction layer (abrupt turns / hard stops) is " +
                  "silent — fine if unwanted, otherwise assign per-surface 'scuffs' resources.";

            if (undefined.Length > 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    $"Surface profile tag(s) not defined in this project: {string.Join(", ", undefined)} — those profiles " +
                    $"can NEVER match a floor and always fall back to the default surface.{scuffNote}",
                    "Project Settings > Tags and Layers → add the tag(s) and tag your floor colliders with them — " +
                    "or rename the profiles' surfaceTag to tags the project actually uses.");

            if (!footsteps.HasAnyScuffSound)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "All surface tags exist, but no scuff resource is assigned anywhere — the friction layer " +
                    "(abrupt turns / hard stops) is silent. Fine if unwanted.",
                    "Assign 'scuffs' resources on the surface profiles (or the default surface) of your Player variant's " +
                    "FootstepAudio — e.g. a squeak on linoleum, a gritty scrape on concrete.");

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                "All surface profile tags exist in the project, and the friction (scuff) layer has sounds.");
        }
    }
}
