using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// FadeMask drives ColorAdjustments through reflection and assumes the
    /// saturation parameter sits at parameters[4]. These tests trip when a Unity
    /// or render-pipeline upgrade breaks either assumption, instead of the fade
    /// silently stopping to work.
    /// </summary>
    public class FadeProfileTests
    {
        [TestCase("URP FadeGlobalVolume Profile")]
        [TestCase("HDRP FadeGlobalVolume Profile")]
        public void FadeProfile_HasColorAdjustmentsWithExpectedParameterLayout(string profileName)
        {
            var guids = AssetDatabase.FindAssets($"{profileName} t:VolumeProfile", new[] { PackagePaths.Runtime });
            Assert.That(guids, Is.Not.Empty,
                $"'{profileName}.asset' not found under {PackagePaths.Runtime}. " +
                "FadeMask needs it assigned on Player.prefab; if it was renamed/moved, update the prefab and this test.");

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.That(profile, Is.Not.Null, $"Could not load VolumeProfile at {path}.");

            Assert.That(profile.components.Any(c => c == null), Is.False,
                $"{path} contains a component whose script is missing. " +
                "Usual cause: the matching render pipeline package (URP/HDRP) is not installed in this project, " +
                "or the profile was serialized against a version whose type moved. Re-add ColorAdjustments to the profile.");

            var colorAdjustments = profile.components.FirstOrDefault(c => c.GetType().Name == "ColorAdjustments");
            Assert.That(colorAdjustments, Is.Not.Null,
                $"{path} has no ColorAdjustments override. FadeMask fades by driving " +
                "ColorAdjustments.colorFilter/saturation — add the override back to the profile.");

            var parameters = colorAdjustments.parameters;

            Assert.That(parameters.Any(p => p.GetType().Name == "ColorParameter"), Is.True,
                $"No ColorParameter found on ColorAdjustments in {path}. " +
                "FadeMask locates colorFilter by looking for the first ColorParameter " +
                "(FadeMask.SetColorAdjustmentProperty) — the pipeline's parameter set changed; update FadeMask.");

            Assert.That(parameters.Count, Is.GreaterThan(4),
                $"ColorAdjustments in {path} exposes only {parameters.Count} parameters. " +
                "FadeMask assumes saturation is parameters[4] (FadeMask.cs, SetColorAdjustmentProperty).");

            Assert.That(parameters[4].GetType().Name, Is.EqualTo("ClampedFloatParameter"),
                $"ColorAdjustments.parameters[4] in {path} is a {parameters[4].GetType().Name}, " +
                "not the ClampedFloatParameter FadeMask expects for saturation. A render-pipeline upgrade " +
                "reordered the parameters — fix the hardcoded index in FadeMask.SetColorAdjustmentProperty " +
                "(better: look the parameter up by field name instead of position).");
        }
    }
}
