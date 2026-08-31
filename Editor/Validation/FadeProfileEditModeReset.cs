using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Team ergonomics guard: when a play session ends (STOP), the fade profile
    /// ASSET is reset to CLEAR (white color filter, 0 saturation). A profile
    /// left black makes the whole world invisible in edit mode, which reads as
    /// a broken scene to anyone opening the project.
    /// The fade Volume component is also DISABLED: the fade only exists in play
    /// mode (the prefab ships it disabled and FadeMask enables it at Awake), so
    /// a legacy variant/scene instance still serialized as enabled is migrated
    /// here the first time a play session ends.
    /// Manual tweaks made in edit mode are deliberately preserved — the reset
    /// only fires on the play -> edit transition, never while editing.
    /// </summary>
    [InitializeOnLoad]
    public static class FadeProfileEditModeReset
    {
        static FadeProfileEditModeReset()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) ResetAllFadeProfiles();
            };
        }

        private static void ResetAllFadeProfiles()
        {
            // The player is usually SPAWNED at runtime (launcher/boot scene flow):
            // after STOP the edit-mode scenes hold no FadeMask at all, so a sweep
            // over scene objects alone never sees the profile that was blackened
            // during play (e.g. ESC opened the menu -> fade to black -> stop).
            // Reset every fade profile ASSET by name, scene contents or not.
            foreach (var guid in AssetDatabase.FindAssets("FadeGlobalVolume t:VolumeProfile"))
                ResetProfile(AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(guid)));

            foreach (var fadeMask in Object.FindObjectsByType<FadeMask>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(fadeMask);
                ResetProfile(serialized.FindProperty("volumeProfile")?.objectReferenceValue as VolumeProfile);
                var volume = serialized.FindProperty("postProcessVolume")?.objectReferenceValue as Volume;
                if (volume == null) continue;
                ResetProfile(volume.sharedProfile);

                // The fade exists only in play mode: FadeMask enables the Volume
                // at Awake, and outside play mode it must stay off or the profile
                // tints the world in edit mode. Migrates pre-1.16.1 variants and
                // scene instances that still serialize the Volume as enabled.
                if (!volume.enabled) continue;
                volume.enabled = false;
                EditorUtility.SetDirty(volume);
                Debug.Log("FadeMask: the fade Volume was serialized ENABLED — it now ships disabled (play mode enables it). " +
                    "Disabled it; save/apply so edit mode never renders the fade again.", volume);
            }
        }

        private static void ResetProfile(VolumeProfile profile)
        {
            if (profile == null) return;

            var changed = false;
            foreach (var component in profile.components)
            {
                if (component == null || component.GetType().Name != "ColorAdjustments") continue;
                var type = component.GetType();

                var colorParameter = type.GetField("colorFilter")?.GetValue(component);
                var colorValue = colorParameter?.GetType().GetProperty("value");
                if (colorValue != null && (Color)colorValue.GetValue(colorParameter) != Color.white)
                {
                    colorValue.SetValue(colorParameter, Color.white);
                    changed = true;
                }

                var saturationParameter = type.GetField("saturation")?.GetValue(component);
                var saturationValue = saturationParameter?.GetType().GetProperty("value");
                if (saturationValue != null && Mathf.Abs((float)saturationValue.GetValue(saturationParameter)) > 0.01f)
                {
                    saturationValue.SetValue(saturationParameter, 0f);
                    changed = true;
                }
            }

            if (!changed) return;
            EditorUtility.SetDirty(profile);
            // Persist immediately: an in-memory reset alone leaves the BLACK
            // values on disk, and the next editor session (or a teammate's pull,
            // if the black file gets committed) starts with an invisible world.
            // Project assets only — the packaged copy sits in the immutable
            // package cache and cannot (and need not) be written.
            var path = AssetDatabase.GetAssetPath(profile);
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/"))
                AssetDatabase.SaveAssetIfDirty(profile);
            //Debug.Log($"FadeMask: fade profile '{profile.name}' reset to CLEAR after the play session — a black fade left " +
            //    "behind makes the world invisible in edit mode. (Edit-mode tweaks are kept; only STOP triggers this.)", profile);
        }
    }
}
