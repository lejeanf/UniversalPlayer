using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace jeanf.universalplayer
{
    public static class HandPoseDriverFixer
    {
        public static readonly string[] PrefabPoseSlots = { "semiClosedFistPose", "closedFistPose", "pointPose" };

        [MenuItem("Tools/Jeanf/UniversalPlayer/Restore Prefab Hand Poses")]
        public static void RestoreScenePrefabPoses()
        {
            var playerRoot = ProjectSetupChecks.ScenePlayerRoot();
            var driver = playerRoot != null ? playerRoot.GetComponentInChildren<ControllerHandPoseDriver>(true) : null;
            if (driver == null)
            {
                Debug.LogWarning("[UniversalPlayer.Fix] No ControllerHandPoseDriver on the scene player — nothing to restore.");
                if (playerRoot != null) EditorGUIUtility.PingObject(playerRoot);
                return;
            }

            var restored = RestorePrefabPoses(driver);
            if (restored == 0)
            {
                Selection.activeObject = driver;
                EditorGUIUtility.PingObject(driver);
            }
            Debug.Log($"[UniversalPlayer.Fix] {restored} empty pose slot(s) restored from the prefab on '{driver.gameObject.name}'.");
        }

        public static int RestorePrefabPoses(ControllerHandPoseDriver driver)
        {
            var serialized = new SerializedObject(driver);
            var restored = 0;
            foreach (var slot in PrefabPoseSlots)
            {
                var property = serialized.FindProperty(slot);
                if (property == null || property.objectReferenceValue != null || !property.prefabOverride) continue;
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                serialized.Update();
                if (serialized.FindProperty(slot).objectReferenceValue != null) restored++;
            }

            if (restored == 0) return 0;
            EditorUtility.SetDirty(driver);
            if (driver.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(driver.gameObject.scene);
            return restored;
        }
    }
}
