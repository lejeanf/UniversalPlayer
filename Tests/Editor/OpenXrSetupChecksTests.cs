using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace jeanf.universalplayer.tests.editor
{
    /// <summary>
    /// OpenXrSetupChecks are advisory: they must name the divergence from the reference
    /// setup, say why it matters and where to change it — and stay quiet (Pass) on the
    /// reference configuration so nobody learns to ignore them. Pure evaluators are
    /// tested directly; the settings-reading wrappers are covered by
    /// SetupValidatorTests.RunProjectConfigChecks_RunsWithoutThrowing.
    /// </summary>
    public class OpenXrSetupChecksTests
    {
        [Test]
        public void ProfileChoice_WarnsOnMetaQuestTouchPlus_WithTheHandsSymptom()
        {
            var result = OpenXrSetupChecks.EvaluateProfileChoice(new[]
            {
                typeof(OculusTouchControllerProfile), typeof(MetaQuestTouchPlusControllerProfile),
            });

            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Warning));
            Assert.That(result.Message, Does.Contain("MetaQuestTouchPlusControllerProfile"));
            Assert.That(result.Message, Does.Contain("hands"), "The warning must name the visible symptom.");
            Assert.That(result.Hint, Does.Contain("Oculus Touch"), "The hint must say what to keep, not only what to remove.");
        }

        [Test]
        public void ProfileChoice_WarnsOnMetaQuestTouchPro_Too()
        {
            var result = OpenXrSetupChecks.EvaluateProfileChoice(new[] { typeof(MetaQuestTouchProControllerProfile) });

            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Warning));
            Assert.That(result.Message, Does.Contain("MetaQuestTouchProControllerProfile"));
        }

        [Test]
        public void ProfileChoice_PassesOnOculusTouchAlone()
        {
            var result = OpenXrSetupChecks.EvaluateProfileChoice(new[] { typeof(OculusTouchControllerProfile) });

            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Pass));
        }

        [Test]
        public void ProfileChoice_WarnsWhenOculusTouchIsMissing_ButDoesNotEnforceIt()
        {
            var result = OpenXrSetupChecks.EvaluateProfileChoice(new[] { typeof(HTCViveControllerProfile) });

            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Warning), "Advisory, never a Fail.");
            Assert.That(result.Message, Does.Contain("HTCViveControllerProfile"));
            Assert.That(result.Hint, Does.Contain("fine to skip"));
        }

        [Test]
        public void ProfileChoice_SkipsWhenNoProfileIsEnabled_TheBaseCheckOwnsThatFailure()
        {
            var result = OpenXrSetupChecks.EvaluateProfileChoice(Array.Empty<Type>());

            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Warning));
            Assert.That(result.Message, Does.Contain("skipped"));
        }

        [Test]
        public void RenderSettings_PassOnTheReferenceValues()
        {
            var result = OpenXrSetupChecks.EvaluateRenderSettings(
                OpenXRSettings.RenderMode.SinglePassInstanced, OpenXRSettings.LatencyOptimization.PrioritizeRendering);

            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Pass));
        }

        [Test]
        public void RenderSettings_NameEachDivergence()
        {
            var result = OpenXrSetupChecks.EvaluateRenderSettings(
                OpenXRSettings.RenderMode.MultiPass, OpenXRSettings.LatencyOptimization.PrioritizeInputPolling);

            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Warning));
            Assert.That(result.Message, Does.Contain("MultiPass").And.Contain("PrioritizeInputPolling"));
            Assert.That(result.Hint, Does.Contain("Single Pass Instanced").And.Contain("Prioritize Rendering"));
        }

        [Test]
        public void RenderSettings_OnlyReportTheFieldThatDiffers()
        {
            var result = OpenXrSetupChecks.EvaluateRenderSettings(
                OpenXRSettings.RenderMode.SinglePassInstanced, OpenXRSettings.LatencyOptimization.PrioritizeInputPolling);

            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Warning));
            Assert.That(result.Message, Does.Not.Contain("Render Mode ="));
            Assert.That(result.Message, Does.Contain("Latency Optimization ="));
        }

        [TestCase("1.17.1", SetupValidator.Severity.Warning)]
        [TestCase("1.16.1", SetupValidator.Severity.Warning)]
        [TestCase("1.18.0", SetupValidator.Severity.Pass)]
        [TestCase("1.18.0-pre.2", SetupValidator.Severity.Pass)]
        [TestCase("1.19.3", SetupValidator.Severity.Pass)]
        public void PackageVersion_WarnsBelowTheFirstVersionWithoutTheUsageRegression(string version,
            SetupValidator.Severity expected)
        {
            var result = OpenXrSetupChecks.EvaluatePackageVersion(version);

            Assert.That(result.Severity, Is.EqualTo(expected), result.Message);
            if (expected == SetupValidator.Severity.Warning)
                Assert.That(result.Hint, Does.Contain("RESTART"), "Swapping the XR package needs an editor restart — say so.");
        }

        [Test]
        public void PackageVersion_SkipsWhenOpenXrIsAbsent()
        {
            var result = OpenXrSetupChecks.EvaluatePackageVersion(null);

            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Warning));
            Assert.That(result.Message, Does.Contain("skipped"));
        }

        [Test]
        public void ProjectConfigChecks_IncludeTheThreeOpenXrChecks()
        {
            var names = SetupValidator.RunProjectConfigChecks().Select(r => r.Name).ToList();

            Assert.That(names, Does.Contain(OpenXrSetupChecks.ProfileChoiceCheck));
            Assert.That(names, Does.Contain(OpenXrSetupChecks.RenderSettingsCheck));
            Assert.That(names, Does.Contain(OpenXrSetupChecks.PackageVersionCheck));
        }
    }
}
