using System.Linq;
using NUnit.Framework;

namespace jeanf.universalplayer.tests.editor
{
    /// <summary>
    /// Guards the setup validator itself: every check must run without throwing,
    /// and every failure/warning it can produce must carry an actionable fix hint —
    /// the whole point is that nothing breaks silently.
    /// </summary>
    public class SetupValidatorTests
    {
        [Test]
        public void RunProjectConfigChecks_RunsWithoutThrowing_AndCoversAllAreas()
        {
            var results = SetupValidator.RunProjectConfigChecks();

            Assert.That(results, Is.Not.Empty, "The validator returned no checks — its check list was emptied out.");

            string[] expectedAreas = { "Input System", "Render pipeline", "Run in background" };
            foreach (var area in expectedAreas)
            {
                Assert.That(results.Any(r => r.Name == area), Is.True,
                    $"Validator no longer runs the '{area}' check — it was removed or renamed; " +
                    "if intentional, update SetupValidatorTests alongside it.");
            }

            Assert.That(results.Any(r => r.Name.StartsWith("XR")), Is.True,
                "Validator no longer runs any XR Plug-in Management check — VR misconfiguration would go unnoticed.");
        }

        [Test]
        public void AssetAndSceneChecks_RunWithoutThrowing()
        {
            var assetResults = ProjectSetupChecks.RunAssetChecks();
            Assert.That(assetResults, Is.Not.Empty,
                "ProjectSetupChecks.RunAssetChecks returned nothing — the variant/samples checks were emptied out.");
            Assert.That(assetResults.Any(r => r.Name.Contains("variant")), Is.True,
                "The prefab-variant workflow check disappeared — it guards against losing customizations on package updates.");

            var sceneResults = ProjectSetupChecks.RunOpenSceneChecks();
            Assert.That(sceneResults, Is.Not.Empty,
                "ProjectSetupChecks.RunOpenSceneChecks returned nothing — scene wiring checks were emptied out.");
        }

        [Test]
        public void EveryFailedOrWarnedCheck_HasAFixHint()
        {
            var results = SetupValidator.RunProjectConfigChecks();
            results.AddRange(ProjectSetupChecks.RunAssetChecks());
            results.AddRange(ProjectSetupChecks.RunOpenSceneChecks());

            foreach (var result in results.Where(r => r.Severity != SetupValidator.Severity.Pass))
            {
                // 'skipped' warnings may legitimately have no hint, but real problems must say where to fix them
                if (result.Message.Contains("skipped")) continue;
                Assert.That(result.Hint, Is.Not.Empty,
                    $"Check '{result.Name}' reported '{result.Message}' without a fix hint — " +
                    "every failure must tell the user where to fix it (SetupValidator contract).");
            }
        }

        [Test]
        public void CheckNames_AreUnique_SoConsoleOutputIsUnambiguous()
        {
            var results = SetupValidator.RunProjectConfigChecks();
            var duplicates = results.GroupBy(r => r.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();

            Assert.That(duplicates, Is.Empty,
                $"Duplicate check names: {string.Join(", ", duplicates)} — rename them so console feedback is unambiguous.");
        }
    }
}
