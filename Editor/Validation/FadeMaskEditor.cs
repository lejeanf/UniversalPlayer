using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace jeanf.universalplayer
{
    /// <summary>
    /// FadeMask inspector with a ONE-CLICK guardrail for the classic mistake:
    /// a URP fade profile assigned in an HDRP project (or vice-versa) makes
    /// every fade — including the black loading screen — a silent no-op.
    /// When the profile mismatches the active pipeline, an orange banner and a
    /// button appear; the button assigns the bundled profile for the ACTIVE
    /// pipeline on the serialized field (undo-able, permanent, build-safe).
    /// </summary>
    [CustomEditor(typeof(FadeMask)), CanEditMultipleObjects]
    public class FadeMaskEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var fadeMask = (FadeMask)target;

            if (!fadeMask.IsValid)
            {
                var prefix = FadeMask.ActivePipelinePrefix();
                var volumeProperty = serializedObject.FindProperty("postProcessVolume");
                var volumeMissing = volumeProperty != null && volumeProperty.objectReferenceValue == null;

                var message = volumeMissing
                    ? "The Volume is not assigned — every fade (loading black screen, head-in-wall) silently no-ops."
                    : fadeMask.EffectiveProfile == null
                        ? "No volume profile assigned (neither on FadeMask nor on the Volume) — every fade silently no-ops."
                        : $"The assigned profile has no {prefix} ColorAdjustments — it belongs to the OTHER render pipeline, " +
                          "so every fade (loading black screen, head-in-wall, menu) silently no-ops.";
                EditorGUILayout.HelpBox(message, MessageType.Warning);

                if (!volumeMissing && prefix != null && GUILayout.Button($"Fix: assign the bundled {prefix} FadeGlobalVolume Profile"))
                    AssignBundledProfile(prefix);

                EditorGUILayout.Space(4f);
            }

            DrawCameraMaskGuard(fadeMask);
            DrawDefaultInspector();
        }

        // The OTHER silent killer: the camera's SRP Volume Mask excluding the
        // fade volume's layer — the volume is ignored entirely (edit mode AND
        // play mode), with a perfectly valid profile.
        private void DrawCameraMaskGuard(FadeMask fadeMask)
        {
            var volume = serializedObject.FindProperty("postProcessVolume")?.objectReferenceValue as Volume;
            if (volume == null) return;

            var camera = fadeMask.GetComponentInParent<Camera>(true);
            if (camera == null) camera = Camera.main;
            if (camera == null || FadeMask.CameraSeesFadeVolume(camera, volume)) return;

            var layerName = LayerMask.LayerToName(volume.gameObject.layer);
            EditorGUILayout.HelpBox(
                $"The camera '{camera.name}' Volume Mask does not include layer '{layerName}' where the fade volume lives — " +
                "the volume is ignored and no fade can ever show (edit mode or play mode).", MessageType.Warning);
            if (GUILayout.Button($"Fix: add '{layerName}' to the camera's Volume Mask"))
            {
                var repaired = FadeMask.RepairCameraVolumeMask(camera, volume);
                if (repaired != null) EditorUtility.SetDirty(repaired);
            }
            EditorGUILayout.Space(4f);
        }

        private void AssignBundledProfile(string prefix)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{prefix} FadeGlobalVolume t:VolumeProfile"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var bundled = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (bundled == null || !FadeMask.ProfileMatchesActivePipeline(bundled)) continue;

                var profileProperty = serializedObject.FindProperty("volumeProfile");
                profileProperty.objectReferenceValue = bundled;
                serializedObject.ApplyModifiedProperties();

                // Also swap the Volume component's own slot: it is overridden at
                // runtime anyway, but a wrong-pipeline profile left visible there
                // reads as a misconfiguration and erodes trust in the validation.
                var volume = serializedObject.FindProperty("postProcessVolume")?.objectReferenceValue as Volume;
                if (volume != null && (!FadeMask.ProfileMatchesActivePipeline(volume.sharedProfile) || !Mathf.Approximately(volume.weight, 1f)))
                {
                    Undo.RecordObject(volume, "Assign fade profile");
                    volume.sharedProfile = bundled;
                    // Weight 1 so the EDIT-MODE preview (volumes evaluate without
                    // play mode) matches what the runtime enforces at Awake.
                    volume.weight = 1f;
                    EditorUtility.SetDirty(volume);
                }
                Debug.Log($"FadeMask: assigned '{bundled.name}' ({path}) — fades now match the active pipeline. " +
                    "Apply the override to your Player variant if you want it saved on the prefab.", target);
                return;
            }

            Debug.LogError($"FadeMask: no bundled '{prefix} FadeGlobalVolume Profile' found in the project — " +
                "is the UniversalPlayer package imported correctly (Runtime/scripts/Fade/)?", target);
        }
    }
}
