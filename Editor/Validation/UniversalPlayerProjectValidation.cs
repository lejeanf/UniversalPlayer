using System;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Registers every Universal Player setup check as a rule of Unity's Project
    /// Validation window (Project Settings > XR Plug-in Management > Project
    /// Validation) under the "Universal Player" category. Same checks as
    /// Tools/Jeanf/UniversalPlayer/ValidateSetup — the console validator stays the source
    /// of truth; this file only wraps its checks in BuildValidationRules so each
    /// issue gets a status icon and a Fix/Edit button next to the XR/XRI rules.
    /// </summary>
    public static class UniversalPlayerProjectValidation
    {
        private const string Category = "Universal Player";

        // Batch checks (assets: FindAssets over every prefab; scene: FindObjectsByType
        // sweeps) are far too heavy to re-run once per rule per window refresh, so one
        // batch run is shared by all rules of a refresh through this short-lived cache.
        private const double CacheSeconds = 1.0;

        private static readonly BuildTargetGroup[] TargetGroups =
            { BuildTargetGroup.Standalone, BuildTargetGroup.Android };

        [InitializeOnLoadMethod]
        private static void RegisterRules()
        {
            foreach (var group in TargetGroups)
                BuildValidator.AddRules(group, BuildRules(group));
        }

        [MenuItem("Tools/Jeanf/UniversalPlayer/Project Validation")]
        private static void OpenProjectValidation() =>
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Project Validation");

        /// <summary>All Universal Player rules for one build target tab. Public so tests can inspect them.</summary>
        public static List<BuildValidationRule> BuildRules(BuildTargetGroup group)
        {
            var rules = new List<BuildValidationRule>
            {
                // --- project configuration ---------------------------------------
                Rule("Input System", () => SetupValidator.CheckInputSystem(),
                    OpenSettings("Project/Player")),
                Rule("Render pipeline", () => SetupValidator.CheckRenderPipeline(),
                    OpenSettings("Project/Graphics")),
                Rule("XR provider", () => SetupValidator.CheckXrProvider(group),
                    OpenSettings("Project/XR Plug-in Management")),
                Rule("XR init on startup", () => SetupValidator.CheckXrInitOnStartup(group),
                    () =>
                    {
                        if (!SetupValidator.TryGetXrGeneralSettings(group, out var settings)) return;
                        settings.InitManagerOnStart = true;
                        EditorUtility.SetDirty(settings);
                        AssetDatabase.SaveAssetIfDirty(settings);
                    },
                    fixItAutomatic: true,
                    isRuleEnabled: () => SetupValidator.TryGetXrGeneralSettings(group, out _)),
                Rule("OpenXR interaction profiles", () => SetupValidator.CheckOpenXrInteractionProfiles(group),
                    OpenSettings("Project/XR Plug-in Management/OpenXR")),
                Rule("Run in background", () => SetupValidator.CheckRunInBackground(),
                    () => PlayerSettings.runInBackground = true,
                    fixItAutomatic: true),
#if UNIVERSALPLAYER_HDRP
                Rule("HDRP diffusion profiles", DiffusionProfileRegistration.RunCheck,
                    DiffusionProfileRegistration.RegisterPackageProfiles,
                    fixItAutomatic: true),
#else
                Rule("HDRP diffusion profiles", DiffusionProfileRegistration.RunCheck,
                    OpenSettings("Project/Graphics")),
#endif

                // --- project assets ----------------------------------------------
                Rule("Player prefab variant", () => AssetResult("Player prefab variant"),
                    () => Ping(AssetDatabase.LoadAssetAtPath<GameObject>(ProjectSetupChecks.PlayerPrefabPath()))),
                Rule("Variant overrides", () => AssetResult("Variant overrides"),
                    VariantOverrideFixer.RemoveDeadOverrides,
                    fixItAutomatic: true),
                Rule("Stale imported samples", () => AssetResult("Stale imported samples"),
                    () => Ping(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        AssetDatabase.IsValidFolder("Assets/Samples") ? "Assets/Samples" : "Assets"))),
            };

            // --- open scene ------------------------------------------------------
            // One rule per named check of RunOpenSceneChecks/RunHandChecks; the Fix
            // button pings the object to repair (these need judgment, never auto-fix).
            foreach (var (name, select) in SceneRuleTargets())
            {
                var checkName = name;
                var selectTarget = select;
                rules.Add(Rule(checkName, () => SceneResult(checkName),
                    () => Ping(selectTarget() ?? PlayerRoot()),
                    isRuleEnabled: () => PlayerRoot() != null,
                    sceneOnly: true));
            }

            return rules;
        }

        // What the Fix button should select for each scene rule; null falls back to the player root.
        private static IEnumerable<(string name, Func<UnityEngine.Object> select)> SceneRuleTargets()
        {
            UnityEngine.Object First<T>() where T : Component =>
                UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

            yield return ("Scene: player is a variant", PlayerRoot);
            yield return ("Scene: player camera", PlayerRoot);
            yield return ("Scene: single gravity system", PlayerRoot);
            yield return ("Scene: player ground collision", PlayerRoot);
            yield return ("Scene: player event bridge", First<PlayerEventBridge>);
            yield return ("Scene: NoPeeking", First<NoPeeking>);
            yield return ("Scene: teleport listener", First<TeleportOnEvent>);
            yield return ("Scene: XR health monitor", First<XrHealthMonitor>);
            yield return ("Scene: fade profile", First<FadeMask>);
            yield return ("Scene: fade volume vs camera mask", First<FadeMask>);
            yield return ("Scene: camera post-processing (URP)", First<FadeMask>);
            yield return ("Scene: seat heights", First<Seat>);
            yield return ("Scene: seat colliders", First<Seat>);
            yield return ("Scene: seat ids (scenario targeting)", First<Seat>);
            yield return ("Scene: seat data bridge", First<SeatDataBridge>);
            yield return ("Scene: scenario seating", First<SitController>);
            yield return ("Scene: XR-clickable UI", First<Canvas>);
            yield return ("Scene: hand visibility", First<HandsDisplayer>);
            yield return ("Scene: hand visibility authority", First<HandsDisplayer>);
            yield return ("Scene: hand pose managers", First<HandPoseManager>);
            yield return ("Scene: hand rigs", First<BaseHand>);
            yield return ("Scene: hand poses vs rig", First<BaseHand>);
            yield return ("Scene: hand pose bone names", First<BaseHand>);
            yield return ("Scene: hand pose driver", First<ControllerHandPoseDriver>);
            yield return ("Scene: finger pointing ray", First<FingerPointingRay>);
            yield return ("Scene: hand colliders", First<BlendableHand>);
        }

        /// <summary>
        /// Wraps one CheckResult-producing check in a rule. The predicate re-runs the
        /// check and mirrors its outcome onto the rule (BuildValidationRule instances
        /// are mutable by design): severity drives the icon, the live message and fix
        /// hint replace the static ones — so the window shows the same rich, situation-
        /// specific feedback as the console validator.
        /// </summary>
        private static BuildValidationRule Rule(string name, Func<SetupValidator.CheckResult> check,
            Action fixIt, bool fixItAutomatic = false, Func<bool> isRuleEnabled = null, bool sceneOnly = false)
        {
            var rule = new BuildValidationRule
            {
                Category = Category,
                Message = name,
                FixIt = fixIt,
                FixItAutomatic = fixItAutomatic,
                SceneOnlyValidation = sceneOnly,
            };
            if (isRuleEnabled != null) rule.IsRuleEnabled = isRuleEnabled;

            rule.CheckPredicate = () =>
            {
                var result = check();
                rule.Error = result.Severity == SetupValidator.Severity.Fail;
                rule.Message = $"{result.Name} — {result.Message}";
                rule.FixItMessage = string.IsNullOrEmpty(result.Hint) ? result.Message : result.Hint;
                return result.Severity == SetupValidator.Severity.Pass;
            };
            return rule;
        }

        private static Action OpenSettings(string path) => () => SettingsService.OpenProjectSettings(path);

        private static void Ping(UnityEngine.Object target)
        {
            if (target == null) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static GameObject PlayerRoot()
        {
            var broadcaster = UnityEngine.Object.FindFirstObjectByType<BroadcastControlsStatus>(FindObjectsInactive.Include);
            return broadcaster != null ? broadcaster.transform.root.gameObject : null;
        }

        // --- shared batch-result caches ------------------------------------------

        private static double s_AssetTime = double.NegativeInfinity;
        private static Dictionary<string, SetupValidator.CheckResult> s_AssetResults;

        private static SetupValidator.CheckResult AssetResult(string name)
        {
            if (EditorApplication.timeSinceStartup - s_AssetTime > CacheSeconds)
            {
                s_AssetResults = new Dictionary<string, SetupValidator.CheckResult>();
                var overrideResults = new List<SetupValidator.CheckResult>();
                foreach (var result in ProjectSetupChecks.RunAssetChecks())
                {
                    // per-variant results ("Variant overrides: <name>") fold into ONE rule
                    if (result.Name.StartsWith("Variant overrides")) overrideResults.Add(result);
                    else s_AssetResults[result.Name] = result;
                }
                s_AssetResults["Variant overrides"] = AggregateOverrides(overrideResults);
                s_AssetTime = EditorApplication.timeSinceStartup;
            }

            return s_AssetResults.TryGetValue(name, out var cached)
                ? cached
                : new SetupValidator.CheckResult(name, SetupValidator.Severity.Pass, "Not applicable.");
        }

        private static SetupValidator.CheckResult AggregateOverrides(List<SetupValidator.CheckResult> results)
        {
            const string name = "Variant overrides";
            if (results.Count == 0)
                return new SetupValidator.CheckResult(name, SetupValidator.Severity.Pass,
                    "No project Player variant to inspect.");

            var failed = results.Where(r => r.Severity != SetupValidator.Severity.Pass).ToArray();
            if (failed.Length == 0)
                return new SetupValidator.CheckResult(name, SetupValidator.Severity.Pass,
                    $"All overrides of {results.Count} variant(s) target objects that still exist in the base prefab.");

            var worst = failed.Any(r => r.Severity == SetupValidator.Severity.Fail)
                ? SetupValidator.Severity.Fail
                : SetupValidator.Severity.Warning;
            return new SetupValidator.CheckResult(name, worst,
                string.Join(" ", failed.Select(r => $"{r.Name}: {r.Message}")),
                failed[0].Hint);
        }

        private static double s_SceneTime = double.NegativeInfinity;
        private static Dictionary<string, SetupValidator.CheckResult> s_SceneResults;

        private static SetupValidator.CheckResult SceneResult(string name)
        {
            if (EditorApplication.timeSinceStartup - s_SceneTime > CacheSeconds)
            {
                s_SceneResults = ProjectSetupChecks.RunOpenSceneChecks()
                    .Concat(HandSetupChecks.RunHandChecks())
                    .GroupBy(result => result.Name)
                    .ToDictionary(group => group.Key, group => group.First());
                s_SceneTime = EditorApplication.timeSinceStartup;
            }

            // A missing entry means the batch skipped it (no player in the scene) —
            // the rule is disabled in that case, so reporting Pass keeps it quiet.
            return s_SceneResults.TryGetValue(name, out var cached)
                ? cached
                : new SetupValidator.CheckResult(name, SetupValidator.Severity.Pass, "Not applicable in the open scene.");
        }
    }
}
