using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
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
            public Transform DriverlessTarget;
            public TrackedPoseDriver UnwiredDriver;
            public Object Target;
            public string Message;

            public bool IsMechanical =>
                InactiveObject != null || DisabledDriver != null || DriverlessTarget != null || UnwiredDriver != null;
        }

        private enum Side { Unknown, Left, Right }

        public static List<Transform> ControllerTargets(GameObject playerRoot)
        {
            var targets = new List<Transform>();
            if (playerRoot == null) return targets;
            foreach (var hand in playerRoot.GetComponentsInChildren<HandsPhysics>(true))
            {
                var target = HandTarget(hand);
                if (target != null && !targets.Contains(target)) targets.Add(target);
            }
            if (targets.Count == 0)
                foreach (var driver in playerRoot.GetComponentsInChildren<TrackedPoseDriver>(true))
                    if (driver.GetComponent<Camera>() == null && !targets.Contains(driver.transform)) targets.Add(driver.transform);
            return targets;
        }

        private static Transform HandTarget(HandsPhysics hand) =>
            new SerializedObject(hand).FindProperty("target")?.objectReferenceValue as Transform;

        public static List<Issue> FindIssues(GameObject playerRoot)
        {
            var issues = new List<Issue>();
            if (playerRoot == null) return issues;

            foreach (var hand in playerRoot.GetComponentsInChildren<HandsPhysics>(true))
            {
                if (HandTarget(hand) == null)
                    issues.Add(new Issue { Target = hand, Message = $"HandsPhysics on '{hand.name}' has no Target — that hand has nothing to follow." });
            }

            foreach (var target in ControllerTargets(playerRoot))
            {
                for (var t = target; t != null && t.gameObject != playerRoot; t = t.parent)
                {
                    if (t.gameObject.activeSelf) continue;
                    issues.Add(new Issue { InactiveObject = t.gameObject, Target = t.gameObject, Message = $"'{t.name}' is inactive — the tracked pose driver under it never runs, so '{target.name}' (and the hand following it) stays still." });
                }

                var driver = target.GetComponent<TrackedPoseDriver>();
                if (driver == null)
                {
                    issues.Add(new Issue { DriverlessTarget = target, Target = target.gameObject, Message = $"'{target.name}' has no TrackedPoseDriver — the hand following it can never move." });
                    continue;
                }
                if (!driver.enabled)
                    issues.Add(new Issue { DisabledDriver = driver, Target = driver, Message = $"TrackedPoseDriver on '{target.name}' is disabled — that hand stays still." });
                if (driver.positionInput.action == null || driver.rotationInput.action == null)
                    issues.Add(new Issue { UnwiredDriver = driver, Target = driver, Message = $"TrackedPoseDriver on '{target.name}' has no position or rotation action assigned." });
            }
            return issues;
        }

        public static SetupValidator.CheckResult Check(GameObject playerRoot)
        {
            var targets = ControllerTargets(playerRoot);
            if (targets.Count == 0)
                return new SetupValidator.CheckResult(SceneCheck, SetupValidator.Severity.Fail,
                    "No tracked controller found on the player (no HandsPhysics target, no TrackedPoseDriver besides the camera) — the VR hands have nothing to follow.",
                    "The Left/Right Controller objects ship on the Player prefab under CameraOffset/Hands; restore them on your variant.",
                    playerRoot.GetComponentsInChildren<HandsPhysics>(true).Select(hand => (Object)hand.gameObject).ToArray());

            var issues = FindIssues(playerRoot);
            if (issues.Count == 0)
                return new SetupValidator.CheckResult(SceneCheck, SetupValidator.Severity.Pass,
                    $"Tracked controllers active with enabled pose drivers: {string.Join(", ", targets.Select(t => t.name))}.");

            return new SetupValidator.CheckResult(SceneCheck, SetupValidator.Severity.Fail,
                string.Join(" ", issues.Select(i => i.Message)),
                issues.Any(i => i.IsMechanical)
                    ? "Press Fix (or Tools/Jeanf/UniversalPlayer/Repair Tracked Controllers): inactive controller objects are activated, disabled pose drivers enabled, and missing or unwired pose drivers set up like the package prefab's — on the scene player, applied to your Player variant (never to the package prefab)."
                    : "Assign the missing target on the HandsPhysics of your Player variant (the package prefab ships it wired).",
                issues.Select(i => i.Target).Where(t => t != null).Distinct().ToArray());
        }

        [MenuItem("Tools/Jeanf/UniversalPlayer/Repair Tracked Controllers")]
        public static void RepairScene() => Repair(ProjectSetupChecks.ScenePlayerRoot());

        public static int Repair(GameObject playerRoot)
        {
            var repaired = 0;
            foreach (var issue in FindIssues(playerRoot))
            {
                if (issue.InactiveObject != null)
                {
                    Undo.RecordObject(issue.InactiveObject, "Activate tracked controller");
                    issue.InactiveObject.SetActive(true);
                    LocalPrefabOverrides.ApplyPropertyOverride(issue.InactiveObject, "m_IsActive");
                    repaired++;
                }
                else if (issue.DisabledDriver != null)
                {
                    Undo.RecordObject(issue.DisabledDriver, "Enable tracked pose driver");
                    issue.DisabledDriver.enabled = true;
                    LocalPrefabOverrides.ApplyPropertyOverride(issue.DisabledDriver, "m_Enabled");
                    repaired++;
                }
                else if (issue.DriverlessTarget != null)
                {
                    var driver = Undo.AddComponent<TrackedPoseDriver>(issue.DriverlessTarget.gameObject);
                    CopyPackageDriverSettings(driver);
                    LocalPrefabOverrides.ApplyComponent(driver);
                    repaired++;
                }
                else if (issue.UnwiredDriver != null && CopyPackageDriverSettings(issue.UnwiredDriver))
                {
                    LocalPrefabOverrides.ApplyComponent(issue.UnwiredDriver);
                    repaired++;
                }
            }
            if (repaired > 0 && playerRoot != null && playerRoot.scene.IsValid()) EditorSceneManager.MarkSceneDirty(playerRoot.scene);
            Debug.Log($"{LogPrefix} {repaired} tracked-controller issue(s) repaired on '{(playerRoot != null ? playerRoot.name : "<no player>")}'.");
            return repaired;
        }

        private static bool CopyPackageDriverSettings(TrackedPoseDriver driver)
        {
            var template = PackageDriverFor(driver.transform);
            if (template == null)
            {
                Debug.LogWarning($"{LogPrefix} No TrackedPoseDriver found on the package Player.prefab to copy onto '{driver.name}' — assign its position/rotation actions by hand.");
                return false;
            }
            ComponentUtility.CopyComponent(template);
            ComponentUtility.PasteComponentValues(driver);
            return true;
        }

        private static TrackedPoseDriver PackageDriverFor(Transform controller)
        {
            var packagePlayerPath = ProjectSetupChecks.PlayerPrefabPath();
            var packagePlayer = packagePlayerPath != null ? AssetDatabase.LoadAssetAtPath<GameObject>(packagePlayerPath) : null;
            if (packagePlayer == null) return null;

            var drivers = packagePlayer.GetComponentsInChildren<TrackedPoseDriver>(true)
                .Where(driver => driver.GetComponent<Camera>() == null)
                .ToArray();
            if (drivers.Length == 0) return null;

            var side = HandSide(controller);
            if (side == Side.Unknown) return drivers[0];
            return drivers.FirstOrDefault(driver => HandSide(driver.transform) == side) ?? drivers[0];
        }

        private static Side HandSide(Transform controller)
        {
            var hand = controller.root.GetComponentsInChildren<HandsPhysics>(true)
                .FirstOrDefault(candidate => HandTarget(candidate) == controller);
            var names = (controller.name + " " + (hand != null ? hand.name : "")).ToLowerInvariant();
            if (Regex.IsMatch(names, @"left|(^|[^a-z])l([^a-z]|$)")) return Side.Left;
            if (Regex.IsMatch(names, @"right|(^|[^a-z])r([^a-z]|$)")) return Side.Right;
            return Side.Unknown;
        }
    }
}
