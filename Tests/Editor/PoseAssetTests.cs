using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Guards the hand-pose data: a Pose asset whose per-hand rotation count does not
    /// match the packaged hand rig's joint count is silently ignored by
    /// BaseHand.ApplyPose — the hand just keeps its previous pose with no error.
    /// These tests catch that at edit time for every pose the package ships.
    /// </summary>
    public class PoseAssetTests
    {
        private static int JointCount(string handPrefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(handPrefabPath);
            Assert.That(prefab, Is.Not.Null, $"Hand prefab not found at '{handPrefabPath}' — was it moved or renamed?");

            var hand = prefab.GetComponentsInChildren<BaseHand>(true).FirstOrDefault();
            Assert.That(hand, Is.Not.Null, $"No BaseHand component on '{handPrefabPath}' — the pose system has nothing to drive.");

            // replicate BaseHand.CollectJoints (runs in Awake at runtime, so not available on the asset)
            var so = new SerializedObject(hand);
            var roots = so.FindProperty("fingerRoots");
            Assert.That(roots, Is.Not.Null, "BaseHand.fingerRoots was renamed — update PoseAssetTests alongside the refactor.");

            var count = 0;
            for (var i = 0; i < roots.arraySize; i++)
            {
                var root = roots.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                if (root != null) count += root.GetComponentsInChildren<Transform>().Length;
            }
            Assert.That(count, Is.GreaterThan(0), $"'{handPrefabPath}' has no finger joints — fingerRoots is empty or unassigned.");
            return count;
        }

        private static IEnumerable<Pose> PackagePoses()
        {
            return AssetDatabase.FindAssets("t:Pose", new[] { PackagePaths.Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Pose>)
                .Where(pose => pose != null);
        }

        [Test]
        public void EveryPackagedPose_MatchesTheHandRigs_JointForJoint()
        {
            var leftJoints = JointCount($"{PackagePaths.Root}/Runtime/Hands/Prefabs/CC_LeftHand_Controller.prefab");
            var rightJoints = JointCount($"{PackagePaths.Root}/Runtime/Hands/Prefabs/CC_RightHand_Controller.prefab");

            var failures = new List<string>();
            foreach (var pose in PackagePoses())
            {
                CheckHand(pose, pose.leftHandInfo?.fingerRotations?.Count ?? 0, leftJoints, "left", failures);
                CheckHand(pose, pose.rightHandInfo?.fingerRotations?.Count ?? 0, rightJoints, "right", failures);
            }

            Assert.That(failures, Is.Empty,
                "Poses that will be SILENTLY IGNORED at runtime (rotation count != hand joint count):\n" +
                string.Join("\n", failures) + "\n\n" +
                "HINT: these poses were saved against a different hand rig. Open each in " +
                "Tools/UniversalPlayer/Pose Editor (pose browser), adjust and re-save. " +
                "Poses with 0 rotations for a hand are treated as 'no data for this hand' and skipped.");
        }

        private static void CheckHand(Pose pose, int rotations, int expected, string side, List<string> failures)
        {
            if (rotations == 0) return; // no data for this hand — legitimate for one-handed poses
            if (rotations == expected) return;
            failures.Add($"'{pose.name}' ({AssetDatabase.GetAssetPath(pose)}): {side} hand has {rotations} rotations, rig has {expected} joints.");
        }
    }
}
