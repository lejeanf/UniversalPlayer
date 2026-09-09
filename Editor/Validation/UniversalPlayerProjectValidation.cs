using System;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer
{
    public static class UniversalPlayerProjectValidation
    {
        private const string Category = "Universal Player";
        private const string DiffusionProfilesRuleName = "HDRP diffusion profiles";
        private const double MinimumSecondsBetweenBatchRuns = 2.0;

        private static readonly BuildTargetGroup[] TargetGroups =
            { BuildTargetGroup.Standalone, BuildTargetGroup.Android };

        private static bool s_AssetResultsStale = true;
        private static bool s_SceneResultsStale = true;
        private static bool s_PlayerRootStale = true;
        private static double s_AssetRunTime = double.NegativeInfinity;
        private static double s_SceneRunTime = double.NegativeInfinity;
        private static Dictionary<string, SetupValidator.CheckResult> s_AssetResults = new Dictionary<string, SetupValidator.CheckResult>();
        private static Dictionary<string, SetupValidator.CheckResult> s_SceneResults = new Dictionary<string, SetupValidator.CheckResult>();
        private static GameObject s_PlayerRoot;
        private static bool s_PlayerRootFound;

        [InitializeOnLoadMethod]
        private static void RegisterRules()
        {
            foreach (var group in TargetGroups)
                BuildValidator.AddRules(group, BuildRules(group));

            EditorApplication.hierarchyChanged += MarkSceneStale;
            Undo.undoRedoPerformed += MarkSceneStale;
            ObjectChangeEvents.changesPublished += MarkSceneStaleOnObjectChanges;
            EditorApplication.projectChanged += MarkProjectStale;
        }

        private static void MarkSceneStale()
        {
            s_SceneResultsStale = true;
            s_PlayerRootStale = true;
        }

        private static void MarkSceneStaleOnObjectChanges(ref ObjectChangeEventStream stream) => MarkSceneStale();

        private static void MarkProjectStale()
        {
            s_AssetResultsStale = true;
            MarkSceneStale();
        }

        [MenuItem("Tools/Jeanf/UniversalPlayer/Project Validation")]
        private static void OpenProjectValidation() =>
            SettingsService.OpenProjectSettings("Project/XR Plug-in Management/Project Validation");

        public static List<BuildValidationRule> BuildRules(BuildTargetGroup group)
        {
            var rules = new List<BuildValidationRule>
            {
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
                Rule(DiffusionProfilesRuleName, () => AssetResult(DiffusionProfilesRuleName),
                    DiffusionProfileRegistration.RegisterPackageProfiles,
                    fixItAutomatic: true),
#else
                Rule(DiffusionProfilesRuleName, () => AssetResult(DiffusionProfilesRuleName),
                    OpenSettings("Project/Graphics")),
#endif

                Rule("Player prefab variant", () => AssetResult("Player prefab variant"),
                    () => Ping(AssetDatabase.LoadAssetAtPath<GameObject>(ProjectSetupChecks.PlayerPrefabPath()))),
                Rule("Variant overrides", () => AssetResult("Variant overrides"),
                    VariantOverrideFixer.RemoveDeadOverrides,
                    fixItAutomatic: true),
                Rule("Stale imported samples", () => AssetResult("Stale imported samples"),
                    () => Ping(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        AssetDatabase.IsValidFolder("Assets/Samples") ? "Assets/Samples" : "Assets"))),
                Rule(InputActionAssetChecks.ProjectCheck, () => AssetResult(InputActionAssetChecks.ProjectCheck),
                    InputActionAssetChecks.RepairProjectInputActions,
                    fixItAutomatic: true),
            };

            rules.Add(SceneRule("Scene: player action assets", CreateMissingPlayerActionAssets));
            rules.Add(SceneRule("Scene: teleport listener", TeleportListenerFixer.WireSceneListeners));
            rules.Add(SceneRule("Scene: hand pose driver", HandPoseDriverFixer.RestoreScenePrefabPoses));
            rules.Add(SceneRule(InputActionAssetChecks.SceneCheck, InputActionAssetChecks.WireSceneInputActionAssets));
            rules.Add(SceneRule(TrackedControllerChecks.SceneCheck, TrackedControllerChecks.RepairScene));
            rules.Add(SceneRule("Scene: XR-clickable UI", ProjectSetupChecks.AddTrackedDeviceGraphicRaycasters));
            rules.Add(SceneRule("Scene: pickable rigidbodies", ProjectSetupChecks.AddMissingPickableRigidbodies));
            rules.Add(SceneRule("Scene: finger pointing ray", HandSetupChecks.CopyReticleHoverMaskToSceneRay));

            foreach (var (name, select) in SceneRuleTargets())
            {
                var checkName = name;
                var selectTarget = select;
                rules.Add(Rule(checkName, () => SceneResult(checkName),
                    () => SelectSceneResultTargets(checkName, selectTarget),
                    isRuleEnabled: () => PlayerRoot() != null,
                    sceneOnly: true));
            }

            return rules;
        }

        private static BuildValidationRule SceneRule(string name, Action automaticFix) =>
            Rule(name, () => SceneResult(name), automaticFix,
                fixItAutomatic: true,
                isRuleEnabled: () => PlayerRoot() != null,
                sceneOnly: true);

        private static IEnumerable<(string name, Func<UnityEngine.Object> select)> SceneRuleTargets()
        {
            UnityEngine.Object First<T>() where T : Component =>
                UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);

            yield return ("Scene: player is a variant", PlayerRoot);
            yield return ("Scene: player camera", PlayerRoot);
            yield return ("Scene: camera pipeline data", PlayerRoot);
            yield return ("Scene: single gravity system", PlayerRoot);
            yield return ("Scene: player ground collision", PlayerRoot);
            yield return ("Scene: player event bridge", First<PlayerEventBridge>);
            yield return ("Scene: NoPeeking", First<NoPeeking>);
            yield return ("Scene: cursor palette", First<CursorStateController>);
            yield return ("Scene: XR mode manager", First<XrModeManager>);
            yield return ("Scene: XR health monitor", First<XrHealthMonitor>);
            yield return ("Scene: fade profile", First<FadeMask>);
            yield return ("Scene: fade volume vs camera mask", First<FadeMask>);
            yield return ("Scene: camera post-processing (URP)", First<FadeMask>);
            yield return ("Scene: seat heights", First<Seat>);
            yield return ("Scene: seat colliders", First<Seat>);
            yield return ("Scene: seat ids (scenario targeting)", First<Seat>);
            yield return ("Scene: seat data bridge", First<SeatDataBridge>);
            yield return ("Scene: scenario seating", First<SitController>);
            yield return ("Scene: hand visibility", First<HandsDisplayer>);
            yield return ("Scene: hand visibility authority", First<HandsDisplayer>);
            yield return ("Scene: hand pose managers", First<HandPoseManager>);
            yield return ("Scene: hand rigs", First<BaseHand>);
            yield return ("Scene: hand poses vs rig", First<BaseHand>);
            yield return ("Scene: hand pose bone names", First<BaseHand>);
            yield return ("Scene: hand colliders", First<BlendableHand>);
            yield return ("Scene: footsteps", First<FootstepAudio>);
            yield return ("Scene: footstep surfaces", First<FootstepAudio>);
        }

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

        private static void SelectSceneResultTargets(string checkName, Func<UnityEngine.Object> fallback)
        {
            var targets = SceneResult(checkName).Targets.Where(target => target != null).ToArray();
            if (targets.Length == 0)
            {
                Ping(fallback() ?? PlayerRoot());
                return;
            }
            Selection.objects = targets;
            EditorGUIUtility.PingObject(targets[0]);
        }

        private static void CreateMissingPlayerActionAssets()
        {
            var root = PlayerRoot();
            var manager = root != null ? root.GetComponentInChildren<PlayerActionManager>(true) : null;
            if (manager == null) { Ping(root); return; }

            if (new SerializedObject(manager).FindProperty("m_InputActionAsset")?.objectReferenceValue == null)
            {
                Ping(manager);
                return;
            }

            manager.CreatePlayerActions();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void Ping(UnityEngine.Object target)
        {
            if (target == null) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        private static GameObject PlayerRoot()
        {
            var cachedRootWasDestroyed = s_PlayerRootFound && s_PlayerRoot == null;
            if (s_PlayerRootStale || cachedRootWasDestroyed)
            {
                var broadcaster = UnityEngine.Object.FindAnyObjectByType<BroadcastControlsStatus>(FindObjectsInactive.Include);
                s_PlayerRoot = broadcaster != null ? broadcaster.transform.root.gameObject : null;
                s_PlayerRootFound = s_PlayerRoot != null;
                s_PlayerRootStale = false;
            }
            return s_PlayerRoot;
        }

        private static bool BatchIsDue(bool stale, double lastRunTime) =>
            stale && EditorApplication.timeSinceStartup - lastRunTime >= MinimumSecondsBetweenBatchRuns;

        private static SetupValidator.CheckResult AssetResult(string name)
        {
            if (BatchIsDue(s_AssetResultsStale, s_AssetRunTime)) RunAssetBatch();

            return s_AssetResults.TryGetValue(name, out var cached)
                ? cached
                : new SetupValidator.CheckResult(name, SetupValidator.Severity.Pass, "Not applicable.");
        }

        private static void RunAssetBatch()
        {
            var results = new Dictionary<string, SetupValidator.CheckResult>();
            var overrideResults = new List<SetupValidator.CheckResult>();
            foreach (var result in ProjectSetupChecks.RunAssetChecks())
            {
                if (result.Name.StartsWith("Variant overrides")) overrideResults.Add(result);
                else results[result.Name] = result;
            }
            results["Variant overrides"] = AggregateOverrides(overrideResults);
            results[DiffusionProfilesRuleName] = DiffusionProfileRegistration.RunCheck();

            s_AssetResults = results;
            s_AssetResultsStale = false;
            s_AssetRunTime = EditorApplication.timeSinceStartup;
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

        private static SetupValidator.CheckResult SceneResult(string name)
        {
            if (BatchIsDue(s_SceneResultsStale, s_SceneRunTime)) RunSceneBatch();

            return s_SceneResults.TryGetValue(name, out var cached)
                ? cached
                : new SetupValidator.CheckResult(name, SetupValidator.Severity.Pass, "Not applicable in the open scene.");
        }

        private static void RunSceneBatch()
        {
            s_SceneResults = ProjectSetupChecks.RunOpenSceneChecks()
                .Concat(HandSetupChecks.RunHandChecks())
                .Concat(FootstepSetupChecks.RunFootstepChecks())
                .GroupBy(result => result.Name)
                .ToDictionary(group => group.Key, group => group.First());
            s_SceneResultsStale = false;
            s_SceneRunTime = EditorApplication.timeSinceStartup;
        }
    }
}
