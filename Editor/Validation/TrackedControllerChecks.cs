using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.XR;

namespace jeanf.universalplayer
{
    public static class TrackedControllerChecks
    {
        public const string SceneCheck = "Scene: tracked controllers";
        private const string LogPrefix = "[UniversalPlayer.Fix]";

        public sealed class Issue
        {
            public GameObject InactiveObject;
            public TrackedPoseDriver DisabledDriver;
            public string Message;
        }

        public static List<Transform> ControllerTargets(GameObject playerRoot)
        {
            var targets = new List<Transform>();
            if (playerRoot == null) return targets;
            foreach (var hand in playerRoot.GetComponentsInChildren<HandsPhysics>(true))
            {
                var target = new SerializedObject(hand).FindProperty("target")?.objectReferenceValue as Transform;
                if (target != null && !targets.Contains(target)) targets.Add(target);
            }
            if (targets.Count == 0)
                foreach (var driver in playerRoot.GetComponentsInChildren<TrackedPoseDriver>(true))
                    if (driver.GetComponent<Camera>() == null && !targets.Contains(driver.transform)) targets.Add(driver.transform);
            return targets;
        }

        public static List<Issue> FindIssues(GameObject playerRoot)
        {
            var issues = new List<Issue>();
            if (playerRoot == null) return issues;

            var hands = playerRoot.GetComponentsInChildren<HandsPhysics>(true);
            foreach (var hand in hands)
            {
                if (new SerializedObject(hand).FindProperty("target")?.objectReferenceValue == null)
                    issues.Add(new Issue { Message = $"HandsPhysics on '{hand.name}' has no Target — that hand has nothing to follow." });
            }

            foreach (var target in ControllerTargets(playerRoot))
            {
                for (var t = target; t != null && t.gameObject != playerRoot; t = t.parent)
                {
                    if (t.gameObject.activeSelf) continue;
                    issues.Add(new Issue { InactiveObject = t.gameObject, Message = $"'{t.name}' is inactive — the tracked pose driver under it never runs, so '{target.name}' (and the hand following it) stays still." });
                }

                var driver = target.GetComponent<TrackedPoseDriver>();
                if (driver == null)
                {
                    issues.Add(new Issue { Message = $"'{target.name}' has no TrackedPoseDriver — the hand following it can never move." });
                    continue;
                }
                if (!driver.enabled)
                    issues.Add(new Issue { DisabledDriver = driver, Message = $"TrackedPoseDriver on '{target.name}' is disabled — that hand stays still." });
                if (driver.positionInput.action == null || driver.rotationInput.action == null)
                    issues.Add(new Issue { Message = $"TrackedPoseDriver on '{target.name}' has no position or rotation action assigned." });
            }
            return issues;
        }

        public static SetupValidator.CheckResult Check(GameObject playerRoot)
        {
            var targets = ControllerTargets(playerRoot);
            if (targets.Count == 0)
                return new SetupValidator.CheckResult(SceneCheck, SetupValidator.Severity.Fail,
                    "No tracked controller found on the player (no HandsPhysics target, no TrackedPoseDriver besides the camera) — the VR hands have nothing to follow.",
                    "The Left/Right Controller objects ship on the Player prefab under CameraOffset/Hands; restore them on your variant.");

            var issues = FindIssues(playerRoot);
            if (issues.Count == 0)
                return new SetupValidator.CheckResult(SceneCheck, SetupValidator.Severity.Pass,
                    $"Tracked controllers active with enabled pose drivers: {string.Join(", ", targets.Select(t => t.name))}.");

            var mechanical = issues.Count(i => i.InactiveObject != null || i.DisabledDriver != null);
            return new SetupValidator.CheckResult(SceneCheck, SetupValidator.Severity.Fail,
                string.Join(" ", issues.Select(i => i.Message)),
                mechanical > 0
                    ? "Press Fix (or Tools/Jeanf/UniversalPlayer/Activate Tracked Controllers): inactive controller objects are activated and disabled pose drivers enabled, on the scene player and on its prefab variant. Usually an old variant override."
                    : "Assign the missing target/actions on your Player variant (the package prefab ships them wired).");
        }

        [MenuItem("Tools/Jeanf/UniversalPlayer/Activate Tracked Controllers")]
        public static void RepairScene() => Repair(ProjectSetupChecks.ScenePlayerRoot());

        public static int Repair(GameObject playerRoot)
        {
            var repaired = 0;
            foreach (var issue in FindIssues(playerRoot))
            {
                if (issue.InactiveObject != null)
                {
                    issue.InactiveObject.SetActive(true);
                    ApplyToVariant(issue.InactiveObject, "m_IsActive");
                    repaired++;
                }
                else if (issue.DisabledDriver != null)
                {
                    issue.DisabledDriver.enabled = true;
                    ApplyToVariant(issue.DisabledDriver, "m_Enabled");
                    repaired++;
                }
            }
            if (repaired > 0 && playerRoot != null && playerRoot.scene.IsValid()) EditorSceneManager.MarkSceneDirty(playerRoot.scene);
            Debug.Log($"{LogPrefix} {repaired} tracked-controller issue(s) repaired on '{(playerRoot != null ? playerRoot.name : "<no player>")}'.");
            return repaired;
        }

        private static void ApplyToVariant(Object target, string propertyPath)
        {
            EditorUtility.SetDirty(target);
            if (!PrefabUtility.IsPartOfPrefabInstance(target)) return;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(target);
            if (source == null) return;
            var variantPath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(variantPath) || !variantPath.StartsWith("Assets/")) return;
            var packageRoot = ProjectSetupChecks.PackageRoot();
            if (packageRoot != null && variantPath.StartsWith(packageRoot + "/")) return;
            var property = new SerializedObject(target).FindProperty(propertyPath);
            if (property != null) PrefabUtility.ApplyPropertyOverride(property, variantPath, InteractionMode.AutomatedAction);
        }
    }
}
