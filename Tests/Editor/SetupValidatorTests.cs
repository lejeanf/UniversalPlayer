using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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

            var handResults = HandSetupChecks.RunHandChecks();
            Assert.That(handResults, Is.Not.Empty,
                "HandSetupChecks.RunHandChecks returned nothing — the hand checks were emptied out.");
        }

        [Test]
        public void EveryFailedOrWarnedCheck_HasAFixHint()
        {
            var results = SetupValidator.RunProjectConfigChecks();
            results.AddRange(ProjectSetupChecks.RunAssetChecks());
            results.AddRange(ProjectSetupChecks.RunOpenSceneChecks());
            results.AddRange(HandSetupChecks.RunHandChecks());

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

        // --- Seat scene checks -------------------------------------------------
        // The scene checks only run once a Player is in the scene, so these build a minimal one
        // plus the Seat(s) under test and assert the specific seat results. (Closed SubScenes hide
        // their Seats at edit time, so the SeatDataBridge warning path can't be faked here — its
        // no-SubScene pass path is covered, the warning path is left to manual ValidateSetup.)

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void DestroySpawned()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private Seat NewSeat(string name) => Spawn(name).AddComponent<Seat>();

        // Run the open-scene checks and return one by name (with a clear failure if it didn't run).
        private static SetupValidator.CheckResult SceneCheck(string name)
        {
            var result = ProjectSetupChecks.RunOpenSceneChecks().FirstOrDefault(r => r.Name == name);
            Assert.That(result.Name, Is.EqualTo(name),
                $"Check '{name}' did not run — is a Player (BroadcastControlsStatus) present in the scene?");
            return result;
        }

        private static void SetPrivate(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"Field '{field}' not found on {target.GetType().Name} — was it renamed?");
            info.SetValue(target, value);
        }

        [Test]
        public void SeatColliderCheck_FlagsMissingAndOffRootColliders()
        {
            Spawn("Player").AddComponent<BroadcastControlsStatus>(); // lets the scene checks run

            var seat = NewSeat("Seat_NoCollider");
            Assert.That(SceneCheck("Scene: seat colliders").Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "A Seat with no collider must be flagged (cannot be aimed at / cannot bake a proxy).");
            Object.DestroyImmediate(seat.gameObject);

            seat = NewSeat("Seat_BoxOnRoot");
            seat.gameObject.AddComponent<BoxCollider>();
            Assert.That(SceneCheck("Scene: seat colliders").Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                "A Seat with a BoxCollider on its root must pass.");
            Object.DestroyImmediate(seat.gameObject);

            seat = NewSeat("Seat_ChildCollider");
            var child = new GameObject("Mesh");
            child.transform.SetParent(seat.transform, false);
            child.AddComponent<BoxCollider>();
            Assert.That(SceneCheck("Scene: seat colliders").Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "A Seat whose only collider is on a child must be flagged — the baked proxy is placed at the root.");
        }

        [Test]
        public void SeatHeightCheck_FlagsSeatedEyesAtOrAboveStanding()
        {
            Spawn("Player").AddComponent<BroadcastControlsStatus>();

            var seat = NewSeat("Seat");               // at origin, no exit anchor -> standing est. uses 1.7m
            SetPrivate(seat, "eyeHeightAboveSeat", 2.0f); // seated eyes 2.0m are ABOVE standing -> flagged
            Assert.That(SceneCheck("Scene: seat heights").Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "A seat whose seated eyes land at/above the standing eyes must be flagged.");

            SetPrivate(seat, "eyeHeightAboveSeat", 0.5f); // seated eyes well below standing -> pass
            Assert.That(SceneCheck("Scene: seat heights").Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                "A seat that lowers the view must pass.");
        }

        [Test]
        public void SeatDataBridgeCheck_PassesWhenNoSubScenes()
        {
            Spawn("Player").AddComponent<BroadcastControlsStatus>();
            NewSeat("Seat");
            Assert.That(SceneCheck("Scene: seat data bridge").Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                "With no SubScenes present, the SeatDataBridge check must pass (it only matters for baked seats).");
        }

        [Test]
        public void HandChecks_CoverEveryHandFailureArea()
        {
            Spawn("Player").AddComponent<BroadcastControlsStatus>(); // lets the hand checks run

            var names = HandSetupChecks.RunHandChecks().Select(r => r.Name).ToList();
            foreach (var expected in new[]
                     {
                         "Scene: hand visibility", "Scene: hand visibility authority", "Scene: hand pose managers",
                         "Scene: hand rigs", "Scene: hand poses vs rig", "Scene: hand pose bone names",
                         "Scene: hand pose driver", "Scene: finger pointing ray", "Scene: hand colliders",
                     })
                Assert.That(names, Does.Contain(expected),
                    $"'{expected}' is no longer in the hand checks — that hand failure would only surface as a " +
                    "runtime warning in the headset again.");
        }

        [Test]
        public void HandChecks_FlagAPlayerWithNoHands()
        {
            // A player with nothing hand-related must fail loudly: this is exactly the
            // "the hands disappeared after a package update" situation.
            var bare = Spawn("Player");
            bare.AddComponent<BroadcastControlsStatus>();

            // The checks inspect the FIRST player in the open scene; if the test scene
            // already holds a real one, they describe that player, not this bare stand-in.
            var found = Object.FindFirstObjectByType<BroadcastControlsStatus>(FindObjectsInactive.Include);
            if (found == null || found.transform.root.gameObject != bare)
                Assert.Ignore("The open scene already contains a Player — this test needs the bare stand-in.");

            var results = HandSetupChecks.RunHandChecks();
            foreach (var area in new[] { "Scene: hand visibility", "Scene: hand pose managers", "Scene: hand rigs" })
            {
                var result = results.First(r => r.Name == area);
                Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Fail),
                    $"'{area}' must FAIL on a player carrying no hands — it reported {result.Severity} instead.");
            }
        }

        [Test]
        public void OpenSceneChecks_IncludeTheSeatChecks()
        {
            Spawn("Player").AddComponent<BroadcastControlsStatus>();
            var names = ProjectSetupChecks.RunOpenSceneChecks().Select(r => r.Name).ToList();
            foreach (var expected in new[] { "Scene: seat heights", "Scene: seat colliders", "Scene: seat data bridge" })
                Assert.That(names, Does.Contain(expected),
                    $"'{expected}' is no longer in the open-scene checks — a seat setup regression would go unvalidated.");
        }
    }
}
