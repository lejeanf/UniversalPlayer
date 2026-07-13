using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// One click to do the recommended channel setup: duplicate the packaged
    /// UniversalPlayerChannels into Assets/ (the packaged one is immutable in consumer
    /// projects and package updates overwrite it) and assign the copy to the
    /// PlayerEventBridge in the open scene.
    /// </summary>
    public static class CreateLocalPlayerChannels
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [MenuItem("Tools/UniversalPlayer/Create Local Player Channels")]
        public static void Run()
        {
            var sourcePath = AssetDatabase.FindAssets("t:PlayerChannelsSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p.StartsWith("Assets/") ? 1 : 0) // prefer the packaged one as the template
                .FirstOrDefault();
            if (sourcePath == null)
            {
                Debug.LogError($"{LogPrefix} No PlayerChannelsSO asset found anywhere — is the UniversalPlayer package intact?");
                return;
            }

            var destination = AssetDatabase.GenerateUniqueAssetPath("Assets/PlayerChannels.asset");
            if (!AssetDatabase.CopyAsset(sourcePath, destination))
            {
                Debug.LogError($"{LogPrefix} Could not copy '{sourcePath}' to '{destination}'.");
                return;
            }
            var copy = AssetDatabase.LoadAssetAtPath<PlayerChannelsSO>(destination);
            Debug.Log($"{LogPrefix} Created '{destination}' from '{sourcePath}'. Point its slots at your project's channels.");

            var bridge = Object.FindFirstObjectByType<PlayerEventBridge>(FindObjectsInactive.Include);
            if (bridge == null)
            {
                Debug.LogWarning($"{LogPrefix} No PlayerEventBridge in the open scene — assign '{destination}' on your " +
                    "Player variant's bridge manually.");
                return;
            }

            var serialized = new SerializedObject(bridge);
            serialized.FindProperty("channels").objectReferenceValue = copy;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(bridge.gameObject.scene);
            EditorGUIUtility.PingObject(copy);
            Debug.Log($"{LogPrefix} Assigned the local copy to '{bridge.name}' in the open scene. " +
                "APPLY the override to your Player variant so every scene gets it (Overrides ▸ Apply).");
        }
    }
}
