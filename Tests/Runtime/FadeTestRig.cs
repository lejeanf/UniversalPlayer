using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Builds a working FadeMask + Volume setup for the currently active render
    /// pipeline (URP or HDRP — the package supports both, tests adapt to whichever
    /// the project runs). Reads back the ColorAdjustments values FadeMask writes,
    /// so tests assert the actual visual effect data, not internal state alone.
    /// </summary>
    public class FadeTestRig : IDisposable
    {
        public GameObject Root { get; }
        public Volume Volume { get; }

        private const float TestFadeTime = 0.05f;

        public FadeTestRig()
        {
            var profile = LoadProfileForActivePipeline();

            Root = new GameObject("FadeTestRig");
            Root.SetActive(false);

            Volume = Root.AddComponent<Volume>();
            Volume.isGlobal = true;

            var fadeMask = Root.AddComponent<FadeMask>();
            SetPrivateField(fadeMask, "postProcessVolume", Volume);
            SetPrivateField(fadeMask, "volumeProfile", profile);
            SetPrivateField(fadeMask, "_fadeTimeInstance", TestFadeTime);

            Root.SetActive(true); // runs FadeMask.Awake → pipeline detection + volume setup
        }

        public void Dispose()
        {
            if (Root != null) UnityEngine.Object.Destroy(Root);
        }

        /// <summary>Long enough for the LitMotion tween (TestFadeTime) to finish with margin.</summary>
        public static float SettleSeconds => TestFadeTime * 6f + 0.1f;

        public Color ReadColorFilter()
        {
            var parameters = ColorAdjustmentsParameters();
            var colorParam = parameters.FirstOrDefault(p => p.GetType().Name == "ColorParameter");
            Assert.That(colorParam, Is.Not.Null,
                "No ColorParameter on ColorAdjustments — pipeline changed its parameter set; " +
                "FadeMask.SetColorAdjustmentProperty needs updating (see FadeProfileTests).");
            return (Color)ParameterValue(colorParam);
        }

        public float ReadSaturation()
        {
            var parameters = ColorAdjustmentsParameters();
            Assert.That(parameters.Count, Is.GreaterThan(4),
                "ColorAdjustments has fewer than 5 parameters — FadeMask's parameters[4] saturation " +
                "assumption is broken (see FadeProfileTests).");
            return (float)ParameterValue(parameters[4]);
        }

        public static string CurrentFadeMaskState()
        {
            var field = typeof(FadeMask).GetField("_currentState", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(field, Is.Not.Null,
                "FadeMask._currentState field not found — it was renamed; update FadeTestRig alongside the refactor.");
            return field.GetValue(null).ToString();
        }

        private ReadOnlyCollection<VolumeParameter> ColorAdjustmentsParameters()
        {
            Assert.That(Volume.profile, Is.Not.Null, "Volume has no profile — FadeMask.Awake did not run its setup.");
            var colorAdjustments = Volume.profile.components.FirstOrDefault(c => c != null && c.GetType().Name == "ColorAdjustments");
            Assert.That(colorAdjustments, Is.Not.Null,
                "No ColorAdjustments on the instantiated fade profile — check the FadeGlobalVolume profile assets.");
            return colorAdjustments.parameters;
        }

        private static object ParameterValue(VolumeParameter parameter)
        {
            var valueProperty = parameter.GetType().GetProperty("value");
            Assert.That(valueProperty, Is.Not.Null, $"No 'value' property on {parameter.GetType().Name}.");
            return valueProperty.GetValue(parameter);
        }

        private static VolumeProfile LoadProfileForActivePipeline()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null)
                Assert.Ignore("No scriptable render pipeline active (built-in) — FadeMask supports URP/HDRP only.");

            var pipelineTypeName = pipeline.GetType().Name;
            string profileName;
            if (pipelineTypeName.Contains("Universal")) profileName = "URP FadeGlobalVolume Profile";
            else if (pipelineTypeName.Contains("HDRenderPipeline")) profileName = "HDRP FadeGlobalVolume Profile";
            else
            {
                Assert.Ignore($"Unknown render pipeline '{pipelineTypeName}' — cannot pick a fade profile.");
                return null;
            }

#if UNITY_EDITOR
            var guids = UnityEditor.AssetDatabase.FindAssets($"{profileName} t:VolumeProfile");
            Assert.That(guids, Is.Not.Empty,
                $"'{profileName}.asset' not found in the project. It ships in the package under " +
                "Runtime/scripts/Fade/ — was it moved or renamed?");
            return UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
#else
            Assert.Ignore("Fade tests need AssetDatabase to load the profile — run them in the editor.");
            return null;
#endif
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                $"Field '{fieldName}' not found on {target.GetType().Name} — it was renamed; " +
                "update FadeTestRig alongside the refactor.");
            field.SetValue(target, value);
        }
    }
}
