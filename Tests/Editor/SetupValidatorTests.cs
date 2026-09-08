using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using jeanf.EventSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.InputSystem;

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

        private readonly List<ScriptableObject> _spawnedAssets = new List<ScriptableObject>();

        [TearDown]
        public void DestroySpawned()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var asset in _spawnedAssets) if (asset != null) Object.DestroyImmediate(asset);
            _spawnedAssets.Clear();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private T NewAsset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _spawnedAssets.Add(asset);
            return asset;
        }

        // Sets a serialized object reference the way the Inspector would — reaches private
        // fields declared on base classes too (reflection on the concrete type does not).
        private static void SetSerialized(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            Assert.That(prop, Is.Not.Null, $"Serialized property '{property}' not found on {target.GetType().Name} — was it renamed?");
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedBool(Object target, string property, bool value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            Assert.That(prop, Is.Not.Null, $"Serialized property '{property}' not found on {target.GetType().Name} — was it renamed?");
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Object GetSerialized(Object target, string property) =>
            new SerializedObject(target).FindProperty(property)?.objectReferenceValue;

        private static bool SceneHasTeleportWiring() =>
            Object.FindAnyObjectByType<TeleportOnEvent>(FindObjectsInactive.Include) != null
            || Object.FindAnyObjectByType<SendTeleportTarget>(FindObjectsInactive.Include) != null;

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
        public void CursorPaletteCheck_RequiresAProjectLocalPalette()
        {
            var player = Spawn("Player");
            player.AddComponent<BroadcastControlsStatus>(); // lets the scene checks run
            var cursor = player.AddComponent<CursorStateController>();

            Assert.That(SceneCheck("Scene: cursor palette").Severity, Is.EqualTo(SetupValidator.Severity.Fail),
                "A CursorStateController without a CursorPaletteSO must fail: cursor and ray have no project-owned colours.");

            var packaged = AssetDatabase.FindAssets("t:CursorPaletteSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith(ProjectSetupChecks.PackageRoot() ?? "<none>"))
                .Select(AssetDatabase.LoadAssetAtPath<CursorPaletteSO>)
                .FirstOrDefault();
            Assert.That(packaged, Is.Not.Null, "The package must ship a default CursorPalette asset.");
            SetPrivate(cursor, "palette", packaged);
            Assert.That(SceneCheck("Scene: cursor palette").Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "Using the PACKAGED palette must warn: consumers cannot edit it and updates overwrite it.");

            var localPath = AssetDatabase.GenerateUniqueAssetPath("Assets/CursorPalette_ValidatorTest.asset");
            var local = ScriptableObject.CreateInstance<CursorPaletteSO>();
            AssetDatabase.CreateAsset(local, localPath);
            try
            {
                SetPrivate(cursor, "palette", local);
                Assert.That(SceneCheck("Scene: cursor palette").Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                    "A project-local palette is the recommended setup and must pass.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(localPath);
            }
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
            var found = Object.FindAnyObjectByType<BroadcastControlsStatus>(FindObjectsInactive.Include);
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

        [Test]
        public void OpenSceneChecks_IncludeTheWiringChecksThatUsedToBeRuntimeWarnings()
        {
            Spawn("Player").AddComponent<BroadcastControlsStatus>();
            var names = ProjectSetupChecks.RunOpenSceneChecks().Select(r => r.Name).ToList();
            foreach (var expected in new[]
                     {
                         "Scene: teleport listener", "Scene: XR mode manager",
                         "Scene: player action assets", "Scene: pickable rigidbodies",
                     })
                Assert.That(names, Does.Contain(expected),
                    $"'{expected}' is no longer in the open-scene checks — that failure would only surface as a " +
                    "play-mode warning again.");
        }

        [Test]
        public void TeleportCheck_RequiresAWiredListenerOnTheTargetsChannel()
        {
            if (Object.FindAnyObjectByType<TeleportOnEvent>(FindObjectsInactive.Include) != null
                || Object.FindAnyObjectByType<SendTeleportTarget>(FindObjectsInactive.Include) != null)
                Assert.Ignore("The open scene already contains teleport wiring — this test needs a clean slate.");

            var channelA = NewAsset<TeleportEventChannelSO>();
            var channelB = NewAsset<TeleportEventChannelSO>();

            var listenerGo = Spawn("Listener");
            listenerGo.SetActive(false); // never subscribes to the channel in edit mode
            var listener = listenerGo.AddComponent<TeleportOnEvent>();
            var targetGo = Spawn("Target");
            targetGo.SetActive(false);
            var target = targetGo.AddComponent<SendTeleportTarget>();
            SetSerialized(target, "_teleportChannel", channelA);

            Assert.That(ProjectSetupChecks.CheckTeleportWiring().Severity, Is.EqualTo(SetupValidator.Severity.Fail),
                "A listener with no channel and nothing on OnEventRaised must FAIL — every teleport on it is dropped.");

            SetSerialized(listener, "player", listenerGo);
            SetSerialized(listener, "_channel", channelB);
            UnityEventTools.AddPersistentListener(listener.OnEventRaised, listener.Teleport);
            Assert.That(ProjectSetupChecks.CheckTeleportWiring().Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "A target broadcasting on a channel no listener receives must WARN — that teleport does nothing at runtime.");

            SetSerialized(listener, "_channel", channelA);
            Assert.That(ProjectSetupChecks.CheckTeleportWiring().Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                "A listener on the target's channel with Teleport wired must pass.");
        }

        [Test]
        public void XrModeManagerCheck_NeedsAResolvableCamera()
        {
            var player = Spawn("Player");
            player.AddComponent<BroadcastControlsStatus>();
            Assert.That(ProjectSetupChecks.CheckXrModeManager(player).Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "A player without XrModeManager must WARN — the flat desktop view / instant VR re-entry are gone.");

            var holder = new GameObject("Hmd");
            holder.transform.SetParent(player.transform);
            holder.SetActive(false);
            var manager = holder.AddComponent<XrModeManager>();
            Assert.That(ProjectSetupChecks.CheckXrModeManager(player).Severity, Is.EqualTo(SetupValidator.Severity.Fail),
                "XrModeManager with no override and no FPSCameraMovement camera must FAIL — it has nothing to switch.");

            manager.playerCameraOverride = Spawn("Camera").AddComponent<Camera>();
            Assert.That(ProjectSetupChecks.CheckXrModeManager(player).Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                "With a Player Camera Override and the default Never stop mode the check must pass.");
        }

        [Test]
        public void CameraPipelineDataCheck_FailsOnDuplicates_WarnsOnNone_PassesOnOne()
        {
            var two = ProjectSetupChecks.EvaluateCameraPipelineData("Main Camera",
                new[] { "HDAdditionalCameraData", "HDAdditionalCameraData" }, scriptablePipelineActive: true);
            Assert.That(two.Severity, Is.EqualTo(SetupValidator.Severity.Fail),
                "Two pipeline camera-data components (variant + scene instance) must FAIL — the pipeline and XrModeManager read different ones.");
            Assert.That(two.Hint, Is.Not.Null.And.Not.Empty);

            var none = ProjectSetupChecks.EvaluateCameraPipelineData("Main Camera", new string[0], scriptablePipelineActive: true);
            Assert.That(none.Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "No camera data on an SRP camera must WARN — the runtime adds a default one.");

            var builtIn = ProjectSetupChecks.EvaluateCameraPipelineData("Main Camera", new string[0], scriptablePipelineActive: false);
            Assert.That(builtIn.Severity, Is.EqualTo(SetupValidator.Severity.Pass), "The built-in pipeline needs no camera data.");

            var one = ProjectSetupChecks.EvaluateCameraPipelineData("Main Camera", new[] { "UniversalAdditionalCameraData" }, scriptablePipelineActive: true);
            Assert.That(one.Severity, Is.EqualTo(SetupValidator.Severity.Pass));
        }

        [Test]
        public void PlayerActionAssetsCheck_FlagsMissingReferencesThenMissingAssets()
        {
            var player = Spawn("Player");
            player.AddComponent<BroadcastControlsStatus>();
            Assert.That(ProjectSetupChecks.CheckPlayerActionAssets(player).Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "A player without PlayerActionManager must WARN.");

            var manager = player.AddComponent<PlayerActionManager>();
            Assert.That(ProjectSetupChecks.CheckPlayerActionAssets(player).Severity, Is.EqualTo(SetupValidator.Severity.Fail),
                "A PlayerActionManager with its references unassigned must FAIL — it NullRefs at play.");

            var asset = NewAsset<InputActionAsset>();
            asset.AddActionMap("ValidatorTest").AddAction("Probe", InputActionType.Button);
            SetSerialized(manager, "m_InputActionAsset", asset);
            SetSerialized(manager, "_actionContainer", NewAsset<ActionContainerSO>());
            SetSerialized(manager, "actionRebindedListener", NewAsset<ActionRebindEventChannelSO>());

            var result = ProjectSetupChecks.CheckPlayerActionAssets(player);
            Assert.That(result.Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "An action with no ActionSO under Resources/Player/Actions must WARN (the 'Did not found actionSO' case).");
            Assert.That(result.Message, Does.Contain("ValidatorTest_Probe"),
                "The warning must name the missing ActionSO so it can be created.");
        }

        [Test]
        public void PickableCheck_WantsARigidbody()
        {
            if (Object.FindAnyObjectByType<PickableObject>(FindObjectsInactive.Include) != null)
                Assert.Ignore("The open scene already contains PickableObjects — this test needs a clean slate.");

            var pickable = Spawn("Pickable");
            pickable.AddComponent<PickableObject>();
            Assert.That(ProjectSetupChecks.CheckPickableRigidbodies().Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "A PickableObject without a Rigidbody must WARN — its physics cannot be suspended while held.");

            pickable.AddComponent<Rigidbody>();
            Assert.That(ProjectSetupChecks.CheckPickableRigidbodies().Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                "With a Rigidbody the pickable check must pass.");
        }

        [Test]
        public void TeleportCheck_DistinguishesPlayerTeleportsFromObjectTeleports()
        {
            if (SceneHasTeleportWiring())
                Assert.Ignore("The open scene already contains teleport wiring — this test needs a clean slate.");

            var channel = NewAsset<TeleportEventChannelSO>();
            var listenerGo = Spawn("ObjectListener");
            listenerGo.SetActive(false);
            var listener = listenerGo.AddComponent<TeleportOnEvent>();
            SetSerialized(listener, "_channel", channel);
            SetSerializedBool(listener, "teleportsPlayer", false);
            UnityEventTools.AddPersistentListener(listener.OnEventRaised, listener.Teleport);

            var targetGo = Spawn("Target");
            targetGo.SetActive(false);
            var target = targetGo.AddComponent<SendTeleportTarget>();
            SetSerialized(target, "_teleportChannel", channel);
            SetSerialized(target, "objectToTeleport", targetGo.transform);

            Assert.That(ProjectSetupChecks.CheckTeleportWiring().Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                "An object-only listener (Teleports Player off, no Player) handles object teleports on its channel.");

            SetSerializedBool(target, "isTeleportPlayer", true);
            var playerTargetResult = ProjectSetupChecks.CheckTeleportWiring();
            Assert.That(playerTargetResult.Severity, Is.EqualTo(SetupValidator.Severity.Warning),
                "A PLAYER teleport received only by object-only listeners must WARN — the listener ignores it at runtime.");
            Assert.That(playerTargetResult.Message, Does.Contain("PLAYER teleport"));

            SetSerializedBool(listener, "teleportsPlayer", true);
            var noPlayerResult = ProjectSetupChecks.CheckTeleportWiring();
            Assert.That(noPlayerResult.Severity, Is.EqualTo(SetupValidator.Severity.Fail),
                "A player listener without its Player field must FAIL — it has nothing to move.");
            Assert.That(noPlayerResult.Message, Does.Contain("Player field is empty"));

            SetSerialized(listener, "player", Spawn("Player"));
            Assert.That(ProjectSetupChecks.CheckTeleportWiring().Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                "A player listener with its Player assigned handles both player and object teleports.");
        }

        [Test]
        public void TeleportListenerFixer_WiresTeleportAndAssignsTheScenePlayer()
        {
            if (SceneHasTeleportWiring())
                Assert.Ignore("The open scene already contains teleport wiring — this test needs a clean slate.");

            var channel = NewAsset<TeleportEventChannelSO>();
            var playerRoot = Spawn("Player");
            playerRoot.AddComponent<BroadcastControlsStatus>();
            var listenerGo = Spawn("Listener");
            listenerGo.SetActive(false);
            var listener = listenerGo.AddComponent<TeleportOnEvent>();
            SetSerialized(listener, "_channel", channel);

            Assert.That(ProjectSetupChecks.CheckTeleportWiring().Severity, Is.EqualTo(SetupValidator.Severity.Fail),
                "Precondition: an unwired player listener fails the check.");

            Assert.That(TeleportListenerFixer.Wire(listener, playerRoot), Is.True,
                "The fixer must report a change on an unwired listener.");
            Assert.That(ProjectSetupChecks.CheckTeleportWiring().Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                "After the fix the listener reaches Teleport and has the scene player assigned.");
            Assert.That(GetSerialized(listener, "player"), Is.EqualTo(playerRoot),
                "The fixer must assign the scene player root to a player listener.");
            Assert.That(listener.OnEventRaised.GetPersistentEventCount(), Is.EqualTo(1));

            Assert.That(TeleportListenerFixer.Wire(listener, playerRoot), Is.False,
                "A second run must change nothing.");
            Assert.That(listener.OnEventRaised.GetPersistentEventCount(), Is.EqualTo(1),
                "The fixer must never add a duplicate Teleport call.");
        }

        [Test]
        public void TeleportListenerFixer_LeavesObjectOnlyListenersWithoutAPlayer()
        {
            if (SceneHasTeleportWiring())
                Assert.Ignore("The open scene already contains teleport wiring — this test needs a clean slate.");

            var playerRoot = Spawn("Player");
            var listenerGo = Spawn("ObjectListener");
            listenerGo.SetActive(false);
            var listener = listenerGo.AddComponent<TeleportOnEvent>();
            SetSerialized(listener, "_channel", NewAsset<TeleportEventChannelSO>());
            SetSerializedBool(listener, "teleportsPlayer", false);

            Assert.That(TeleportListenerFixer.Wire(listener, playerRoot), Is.True);
            Assert.That(GetSerialized(listener, "player"), Is.Null,
                "An object-only listener must not receive the player — that field is meaningless for it.");
            Assert.That(ProjectSetupChecks.CheckTeleportWiring().Severity, Is.EqualTo(SetupValidator.Severity.Pass));
        }

        [Test]
        public void PoseDriverCheck_CountsTheTwoFistSlots()
        {
            var player = Spawn("Player");
            player.AddComponent<BroadcastControlsStatus>();
            var driver = player.AddComponent<ControllerHandPoseDriver>();

            Assert.That(HandSetupChecks.CheckPoseDriver(player).Severity, Is.EqualTo(SetupValidator.Severity.Fail),
                "A driver with no fist pose must FAIL — the fingers never close.");

            SetSerialized(driver, "closedFistPose", NewAsset<Pose>());
            var oneSlot = HandSetupChecks.CheckPoseDriver(player);
            Assert.That(oneSlot.Severity, Is.EqualTo(SetupValidator.Severity.Warning));
            Assert.That(oneSlot.Message, Does.Contain("1 of 2 fist poses empty"),
                "The check must count the driver's two real fist slots, not a slot that no longer exists.");

            SetSerialized(driver, "semiClosedFistPose", NewAsset<Pose>());
            Assert.That(HandSetupChecks.CheckPoseDriver(player).Severity, Is.EqualTo(SetupValidator.Severity.Pass),
                "Both fist slots assigned must pass — a permanent warning on a correct setup trains people to ignore the validator.");
        }

        [Test]
        public void HandPoseDriverFixer_RestoresPrefabPosesOnEmptyOverrides()
        {
            const string folder = "Assets/UniversalPlayerFixerTestsTmp";
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            AssetDatabase.CreateFolder("Assets", "UniversalPlayerFixerTestsTmp");
            try
            {
                var closed = ScriptableObject.CreateInstance<Pose>();
                AssetDatabase.CreateAsset(closed, folder + "/Closed.asset");
                var semi = ScriptableObject.CreateInstance<Pose>();
                AssetDatabase.CreateAsset(semi, folder + "/Semi.asset");

                var source = Spawn("DriverSource");
                var sourceDriver = source.AddComponent<ControllerHandPoseDriver>();
                SetSerialized(sourceDriver, "closedFistPose", closed);
                SetSerialized(sourceDriver, "semiClosedFistPose", semi);
                var prefab = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Driver.prefab");

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                _spawned.Add(instance);
                var driver = instance.GetComponent<ControllerHandPoseDriver>();
                SetSerialized(driver, "closedFistPose", null);
                SetSerialized(driver, "semiClosedFistPose", null);
                Assert.That(GetSerialized(driver, "closedFistPose"), Is.Null, "Precondition: the instance overrides the slot to None.");

                Assert.That(HandPoseDriverFixer.RestorePrefabPoses(driver), Is.EqualTo(2),
                    "Both empty overrides must be reverted to the prefab poses.");
                Assert.That(GetSerialized(driver, "closedFistPose"), Is.EqualTo(closed));
                Assert.That(GetSerialized(driver, "semiClosedFistPose"), Is.EqualTo(semi));
                Assert.That(HandSetupChecks.CheckPoseDriver(instance).Severity, Is.EqualTo(SetupValidator.Severity.Pass));

                Assert.That(HandPoseDriverFixer.RestorePrefabPoses(driver), Is.EqualTo(0),
                    "A second run must change nothing.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void HandPoseDriverFixer_DoesNothingWhenThePrefabHasNoPoseEither()
        {
            const string folder = "Assets/UniversalPlayerFixerTestsTmp";
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
            AssetDatabase.CreateFolder("Assets", "UniversalPlayerFixerTestsTmp");
            try
            {
                var source = Spawn("DriverSource");
                source.AddComponent<ControllerHandPoseDriver>();
                var prefab = PrefabUtility.SaveAsPrefabAsset(source, folder + "/Driver.prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                _spawned.Add(instance);

                Assert.That(HandPoseDriverFixer.RestorePrefabPoses(instance.GetComponent<ControllerHandPoseDriver>()), Is.EqualTo(0),
                    "Nothing to restore when the prefab itself has empty slots — that case needs authored poses (ping only).");
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }
}
