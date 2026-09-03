using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// One click to do the recommended pointer-colour setup: duplicate the packaged
    /// CursorPalette into Assets/ (the packaged one is immutable in consumer projects and
    /// package updates overwrite it) and assign the copy to the CursorStateController in
    /// the open scene. The interaction ray reads the same asset through that component.
    /// Also prompts when a Player variant is opened in prefab mode while still on the
    /// packaged palette, and backs the CursorStateController inspector's fix button.
    /// </summary>
    public static class CreateLocalCursorPalette
    {
        private const string LogPrefix = "[UniversalPlayer]";
        private const string PromptSessionKey = "UniversalPlayer.CursorPalettePrompted.";

        [MenuItem("Tools/Jeanf/UniversalPlayer/Create Local Cursor Palette")]
        public static void Run()
        {
            var copy = CreateLocalCopy();
            if (copy == null) return;

            var cursor = Object.FindAnyObjectByType<CursorStateController>(FindObjectsInactive.Include);
            if (cursor == null)
            {
                Debug.LogWarning($"{LogPrefix} No CursorStateController in the open scene — assign '{AssetDatabase.GetAssetPath(copy)}' on your " +
                    "Player variant's CursorStateController manually.");
                return;
            }
            AssignTo(cursor, copy);
        }

        [InitializeOnLoadMethod]
        private static void HookPrefabStage()
        {
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
        }

        private static void OnPrefabStageOpened(PrefabStage stage)
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var cursor = FindCursorNeedingLocalPalette(stage.prefabContentsRoot, stage.assetPath);
            if (cursor == null) return;

            var sessionKey = PromptSessionKey + stage.assetPath;
            if (SessionState.GetBool(sessionKey, false)) return;
            SessionState.SetBool(sessionKey, true);

            var accepted = EditorUtility.DisplayDialog(
                "UniversalPlayer — cursor palette",
                $"'{stage.prefabContentsRoot.name}' still uses the PACKAGED cursor palette. That asset cannot be edited in " +
                "consumer projects and package updates overwrite it, so the cursor and the interaction ray cannot be restyled.\n\n" +
                "Create a project-local copy in Assets/ and assign it to this prefab now?",
                "Create local copy", "Not now");
            if (!accepted) return;

            var copy = CreateLocalCopy();
            if (copy != null) AssignTo(cursor, copy);
        }

        public static CursorStateController FindCursorNeedingLocalPalette(GameObject root, string prefabPath)
        {
            if (root == null) return null;
            var packageRoot = ProjectSetupChecks.PackageRoot();
            if (!string.IsNullOrEmpty(packageRoot) && !string.IsNullOrEmpty(prefabPath) && prefabPath.StartsWith(packageRoot)) return null;
            var cursor = root.GetComponentInChildren<CursorStateController>(true);
            return cursor != null && IsPackaged(cursor.Palette) ? cursor : null;
        }

        public static bool IsPackaged(CursorPaletteSO palette)
        {
            if (palette == null) return false;
            var packageRoot = ProjectSetupChecks.PackageRoot();
            return !string.IsNullOrEmpty(packageRoot) && AssetDatabase.GetAssetPath(palette).StartsWith(packageRoot);
        }

        public static CursorPaletteSO CreateLocalCopy()
        {
            var sourcePath = AssetDatabase.FindAssets("t:CursorPaletteSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p.StartsWith("Assets/") ? 1 : 0)
                .FirstOrDefault();
            if (sourcePath == null)
            {
                Debug.LogError($"{LogPrefix} No CursorPaletteSO asset found anywhere — is the UniversalPlayer package intact?");
                return null;
            }

            var destination = AssetDatabase.GenerateUniqueAssetPath("Assets/CursorPalette.asset");
            if (!AssetDatabase.CopyAsset(sourcePath, destination))
            {
                Debug.LogError($"{LogPrefix} Could not copy '{sourcePath}' to '{destination}'.");
                return null;
            }
            var copy = AssetDatabase.LoadAssetAtPath<CursorPaletteSO>(destination);
            Debug.Log($"{LogPrefix} Created '{destination}' from '{sourcePath}'. Restyle resting / hover / click / invalid there — cursor and ray follow.");
            return copy;
        }

        public static void AssignTo(CursorStateController cursor, CursorPaletteSO palette)
        {
            var serialized = new SerializedObject(cursor);
            serialized.FindProperty("palette").objectReferenceValue = palette;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (cursor.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(cursor.gameObject.scene);
            EditorGUIUtility.PingObject(palette);

            var inPrefabStage = PrefabStageUtility.GetPrefabStage(cursor.gameObject) != null;
            Debug.Log(inPrefabStage
                ? $"{LogPrefix} Assigned '{palette.name}' to '{cursor.name}' in prefab mode — save the prefab to keep it."
                : $"{LogPrefix} Assigned '{palette.name}' to '{cursor.name}' in the open scene. " +
                  "APPLY the override to your Player variant so every scene gets it (Overrides ▸ Apply).");
        }
    }
}
