using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace jeanf.universalplayer
{
    public static class TeleportListenerFixer
    {
        [MenuItem("Tools/Jeanf/UniversalPlayer/Wire Teleport Listeners")]
        public static void WireSceneListeners()
        {
            var playerRoot = ProjectSetupChecks.ScenePlayerRoot();
            var listeners = Object.FindObjectsByType<TeleportOnEvent>(FindObjectsInactive.Include);
            var wired = 0;
            foreach (var listener in listeners)
                if (Wire(listener, playerRoot)) wired++;

            if (wired == 0 && listeners.Length > 0)
            {
                Selection.activeObject = listeners[0];
                EditorGUIUtility.PingObject(listeners[0]);
            }
            Debug.Log($"[UniversalPlayer.Fix] {wired} of {listeners.Length} TeleportOnEvent listener(s) needed wiring.");
        }

        public static bool Wire(TeleportOnEvent listener, GameObject playerRoot)
        {
            var changed = false;
            var serialized = new SerializedObject(listener);
            if (!ProjectSetupChecks.PersistentCallsReach(serialized.FindProperty("OnEventRaised"), nameof(TeleportOnEvent.Teleport)))
            {
                UnityEventTools.AddPersistentListener(listener.OnEventRaised, listener.Teleport);
                serialized.Update();
                changed = true;
            }

            var teleportsPlayer = serialized.FindProperty("teleportsPlayer");
            var player = serialized.FindProperty("player");
            if (teleportsPlayer != null && teleportsPlayer.boolValue && player != null
                && player.objectReferenceValue == null && playerRoot != null)
            {
                player.objectReferenceValue = playerRoot;
                serialized.ApplyModifiedProperties();
                changed = true;
            }

            if (!changed) return false;
            EditorUtility.SetDirty(listener);
            if (listener.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(listener.gameObject.scene);
            return true;
        }
    }
}
