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
            foreach (var fadeMask in Object.FindObjectsByType<FadeMask>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var serialized = new SerializedObject(fadeMask);
                ResetProfile(serialized.FindProperty("volumeProfile")?.objectReferenceValue as VolumeProfile);
                var volume = serialized.FindProperty("postProcessVolume")?.objectReferenceValue as Volume;
                if (volume != null) ResetProfile(volume.sharedProfile);
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
            //Debug.Log($"FadeMask: fade profile '{profile.name}' reset to CLEAR after the play session — a black fade left " +
            //    "behind makes the world invisible in edit mode. (Edit-mode tweaks are kept; only STOP triggers this.)", profile);
        }
    }
}
