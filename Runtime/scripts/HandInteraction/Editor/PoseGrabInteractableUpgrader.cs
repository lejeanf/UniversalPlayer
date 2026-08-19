#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace jeanf.universalplayer
{
    /// <summary>
    /// One-click migration: swaps every grabbable object that carries a hand pose from a
    /// plain <see cref="XRGrabInteractable"/> to <see cref="PoseGrabInteractable"/>, so the
    /// object is grabbed at the offset authored in the Pose Editor. The swap only rewrites
    /// the script reference — every XRI setting on the component (movement type, throw
    /// settings, attach transform, …) is preserved, because PoseGrabInteractable adds no
    /// serialized fields of its own. Runs over all prefabs plus every currently open scene.
    /// </summary>
    public static class PoseGrabInteractableUpgrader
    {
        // GUID of PoseGrabInteractable.cs (see its .meta) — resolves the MonoScript to point
        // each upgraded component at, independent of where the file sits.
        private const string PoseGrabScriptGuid = "7c1e9a4b3d2f60849a5b9c0d1e2f3041";

        [MenuItem("Tools/UniversalPlayer/Upgrade grab objects to PoseGrabInteractable")]
        public static void UpgradeGrabObjects()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("Upgrade grab objects works in Edit Mode — exit Play Mode first.");
                return;
            }

            var newScript = LoadPoseGrabScript();
            if (newScript == null)
            {
                Debug.LogError("Upgrade grab objects: PoseGrabInteractable script not found " +
                    $"(guid {PoseGrabScriptGuid}). Was it moved or its .meta regenerated?");
                return;
            }

            var prefabComponents = 0;
            var prefabAssets = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root;
                try { root = PrefabUtility.LoadPrefabContents(path); }
                catch { continue; } // unreadable / broken prefab — skip, never abort the batch
                try
                {
                    var changed = UpgradeInHierarchy(root, newScript);
                    if (changed > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabComponents += changed;
                        prefabAssets++;
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            var sceneComponents = 0;
            var sceneCount = 0;
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                var changed = 0;
                foreach (var rootObject in scene.GetRootGameObjects())
                    changed += UpgradeInHierarchy(rootObject, newScript);
                if (changed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    sceneComponents += changed;
                    sceneCount++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[UniversalPlayer] Upgrade grab objects: converted {prefabComponents} component(s) " +
                $"in {prefabAssets} prefab(s) and {sceneComponents} component(s) across {sceneCount} open scene(s) " +
                "to PoseGrabInteractable. Open scenes were marked dirty — save them to keep the change.");
        }

        private static int UpgradeInHierarchy(GameObject root, MonoScript newScript)
        {
            var changed = 0;
            foreach (var grab in root.GetComponentsInChildren<XRGrabInteractable>(true))
            {
                // Only plain XRGrabInteractable that carries a hand pose. Anything already a
                // subclass (PoseGrabInteractable or a project-specific one) is left alone.
                if (grab.GetType() != typeof(XRGrabInteractable)) continue;
                if (!CarriesHandPose(grab.gameObject)) continue;

                var serialized = new SerializedObject(grab);
                var scriptProperty = serialized.FindProperty("m_Script");
                if (scriptProperty == null) continue;
                scriptProperty.objectReferenceValue = newScript;
                serialized.ApplyModifiedProperties();
                changed++;
            }
            return changed;
        }

        private static bool CarriesHandPose(GameObject go)
        {
            if (go.TryGetComponent(out PoseContainer container) && container.pose != null) return true;
            if (go.TryGetComponent(out PickableObject pickable) && pickable.HandPose != null) return true;
            return false;
        }

        private static MonoScript LoadPoseGrabScript()
        {
            var path = AssetDatabase.GUIDToAssetPath(PoseGrabScriptGuid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<MonoScript>(path);
        }
    }
}
#endif
