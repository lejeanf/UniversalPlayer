using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.XR.CoreUtils.Editor;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer.tests.editor
{
    /// <summary>
    /// Guards the Project Validation window integration: every console check area must
    /// have a rule, every rule must be well-formed (category, predicate, fix action),
    /// and the rules must agree with the console validator they wrap — a silent drift
    /// between the two surfaces would defeat the single-source-of-truth design.
    /// </summary>
    public class ProjectValidationRuleTests
    {
        private static List<BuildValidationRule> Rules() =>
            UniversalPlayerProjectValidation.BuildRules(BuildTargetGroup.Standalone);

        [Test]
        public void EveryRule_IsWellFormed()
        {
            var rules = Rules();
            Assert.That(rules, Is.Not.Empty, "BuildRules returned nothing — the rule list was emptied out.");

            foreach (var rule in rules)
            {
                Assert.That(rule.Category, Is.EqualTo("Universal Player"),
                    $"Rule '{rule.Message}' has category '{rule.Category}' — all rules must group under 'Universal Player'.");
                Assert.That(rule.CheckPredicate, Is.Not.Null,
                    $"Rule '{rule.Message}' has no CheckPredicate — it can never report anything.");
                Assert.That(rule.FixIt, Is.Not.Null,
                    $"Rule '{rule.Message}' has no FixIt — every issue must offer a Fix/Edit action (validator contract).");
                Assert.That(rule.Message, Is.Not.Empty, "A rule has an empty initial Message.");
            }
        }

        [Test]
        public void RuleNames_AreUnique()
        {
            // Before any predicate runs, Message holds the plain check name.
            var duplicates = Rules().GroupBy(r => r.Message).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Assert.That(duplicates, Is.Empty,
                $"Duplicate rule names: {string.Join(", ", duplicates)} — each check must map to exactly one rule.");
        }

        [Test]
        public void Rules_CoverEveryConsoleCheckArea()
        {
            var names = Rules().Select(r => r.Message).ToList();

            // config + assets
            foreach (var expected in new[]
                     {
                         "Input System", "Render pipeline", "XR provider", "XR init on startup",
                         "OpenXR interaction profiles", "Run in background", "HDRP diffusion profiles",
                         "Player prefab variant", "Variant overrides", "Stale imported samples",
                     })
                Assert.That(names, Does.Contain(expected),
                    $"No rule for '{expected}' — that console check has no Project Validation counterpart anymore.");

            // scene + hands: every named check the batch runners can emit (skip markers excluded)
            var player = new GameObject("Player", typeof(BroadcastControlsStatus));
            try
            {
                var batchNames = ProjectSetupChecks.RunOpenSceneChecks()
                    .Concat(HandSetupChecks.RunHandChecks())
                    .Select(r => r.Name)
                    .Where(n => !n.StartsWith("Variant overrides"));
                foreach (var expected in batchNames)
                    Assert.That(names, Does.Contain(expected),
                        $"No rule for scene check '{expected}' — it runs in the console validator but is invisible " +
                        "in the Project Validation window. Add it to SceneRuleTargets().");
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void EveryPredicate_RunsWithoutThrowing_AndMirrorsSeverityOntoTheRule()
        {
            foreach (var rule in Rules())
            {
                var name = rule.Message;
                var passed = rule.CheckPredicate();
                if (passed)
                    Assert.That(rule.Error, Is.False,
                        $"Rule '{name}' passed its predicate but still flags Error — the icon would lie.");
                Assert.That(rule.Message, Does.Contain("—"),
                    $"Rule '{name}': the predicate did not write the live check message onto the rule.");
                Assert.That(rule.FixItMessage, Is.Not.Empty,
                    $"Rule '{name}': no fix hint was written — the Fix button tooltip would be blank.");
            }
        }

        [Test]
        public void ConfigRules_AgreeWithTheConsoleValidator()
        {
            var consoleResults = SetupValidator.RunProjectConfigChecks();
            foreach (var rule in Rules())
            {
                var name = rule.Message;
                // Console names differ slightly for the settings-missing XR case
                // ("XR Plug-in Management" vs the rule's "XR provider") — compare only exact matches.
                var console = consoleResults.Where(r => r.Name == name).ToArray();
                if (console.Length != 1) continue;

                var passed = rule.CheckPredicate();
                Assert.That(passed, Is.EqualTo(console[0].Severity == SetupValidator.Severity.Pass),
                    $"Rule '{name}' disagrees with the console validator: predicate says " +
                    $"{(passed ? "pass" : "issue")}, console says {console[0].Severity}.");
                Assert.That(rule.Error, Is.EqualTo(console[0].Severity == SetupValidator.Severity.Fail),
                    $"Rule '{name}' maps severity {console[0].Severity} to Error={rule.Error} — " +
                    "Fail must be an error, Warning must not.");
            }
        }

        [Test]
        public void SceneRules_AreSceneOnly_SoTheyDoNotBlockBuilds()
        {
            foreach (var rule in Rules().Where(r => r.Message.StartsWith("Scene:")))
                Assert.That(rule.SceneOnlyValidation, Is.True,
                    $"Scene rule '{rule.Message}' is not SceneOnlyValidation — it would fail command-line builds " +
                    "made from scenes that legitimately have no player.");
        }
    }
}
