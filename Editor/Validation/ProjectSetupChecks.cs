using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Project-level checks for Tools/Jeanf/UniversalPlayer/ValidateSetup: enforces the
    /// prefab-VARIANT workflow (projects must use a variant of the package
    /// Player.prefab so custom SO/event links survive package updates), detects
    /// orphaned variant overrides (the failure mode that silently removed the VR
    /// hands), and inspects the open scene's wiring.
    /// </summary>
    public static class ProjectSetupChecks
    {
        private const string PlayerAsmdefSuffix = "Runtime/scripts/jeanf.universalplayer.asmdef";

        /// <summary>"Assets/UniversalPlayer" (package development) or "Packages/fr.jeanf.universal.player" (consumers).</summary>
        public static string PackageRoot()
        {
            foreach (var guid in AssetDatabase.FindAssets("jeanf.universalplayer t:AssemblyDefinitionAsset"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(PlayerAsmdefSuffix)) continue;
                return path.Substring(0, path.Length - PlayerAsmdefSuffix.Length).TrimEnd('/');
            }
            return null;
        }

        public static string PlayerPrefabPath()
        {
            var root = PackageRoot();
            return root == null ? null : $"{root}/Runtime/Prefabs/Player.prefab";
        }

        public static List<SetupValidator.CheckResult> RunAssetChecks()
        {
            var results = new List<SetupValidator.CheckResult>();

            var packageRoot = PackageRoot();
            var playerPrefab = packageRoot != null ? AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath()) : null;
            if (playerPrefab == null)
            {
                results.Add(new SetupValidator.CheckResult("Player prefab variant", SetupValidator.Severity.Fail,
                    "The package Player.prefab could not be located — is the Universal Player package installed correctly?",
                    "Check Runtime/Prefabs/Player.prefab exists in the package."));
                return results;
            }

            var variants = FindPlayerVariants(playerPrefab, packageRoot);
            var developingThePackage = packageRoot.StartsWith("Assets");

            if (variants.Count == 0)
            {
                results.Add(new SetupValidator.CheckResult("Player prefab variant",
                    developingThePackage ? SetupValidator.Severity.Warning : SetupValidator.Severity.Fail,
                    "No project prefab VARIANT of the package Player.prefab exists" +
                    (developingThePackage ? " (fine while developing the package itself)." :
                        " — custom SO/event links made on scene instances will be lost on every package update."),
                    "Right-click the package Player.prefab > Create > Prefab Variant, put the variant in Assets/, " +
                    "use IT in your scenes, and do all custom wiring on the variant."));
            }
            else
            {
                results.Add(new SetupValidator.CheckResult("Player prefab variant", SetupValidator.Severity.Pass,
                    $"Found {variants.Count} project variant(s): {string.Join(", ", variants.Select(v => v.name))}."));

                foreach (var variant in variants)
                    results.Add(CheckOrphanedOverrides(variant));
            }

            results.Add(CheckStaleImportedSamples());
            return results;
        }

        internal static List<GameObject> FindPlayerVariants(GameObject playerPrefab, string packageRoot)
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !path.StartsWith(packageRoot + "/"))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(go => go != null && PrefabUtility.GetCorrespondingObjectFromOriginalSource(go) == playerPrefab)
                .ToList();
        }

        /// <summary>
        /// An override whose target object no longer exists in the base prefab is dead
        /// weight at best — and at worst it WAS the customization (this is exactly how
        /// the VR hands vanished: the variant's hand-model overrides pointed at objects
        /// the updated base prefab no longer contained).
        /// </summary>
        private static SetupValidator.CheckResult CheckOrphanedOverrides(GameObject variant)
        {
            var modifications = PrefabUtility.GetPropertyModifications(variant);
            if (modifications == null)
                return new SetupValidator.CheckResult($"Variant overrides: {variant.name}", SetupValidator.Severity.Pass,
                    "No overrides recorded.");

            var orphaned = modifications.Where(m => m.target == null).ToArray();

            if (orphaned.Length == 0)
                return new SetupValidator.CheckResult($"Variant overrides: {variant.name}", SetupValidator.Severity.Pass,
                    "All overrides target objects that still exist in the base prefab.");

            // Count ENTRIES (that is what VariantOverrideFixer strips) but summarise them
            // BY PROPERTY, with multiplicity: the same dead property usually repeats over
            // many objects (m_Layer on every collider), so listing each one says nothing —
            // while counting distinct properties, as this check used to, under-reported
            // the removal by several times.
            var byProperty = orphaned
                .GroupBy(m => m.propertyPath)
                .OrderByDescending(group => group.Count())
                .Select(group => group.Count() > 1 ? $"{group.Key} x{group.Count()}" : group.Key)
                .ToArray();

            var preview = string.Join(", ", byProperty.Take(6)) + (byProperty.Length > 6 ? ", ..." : "");
            return new SetupValidator.CheckResult($"Variant overrides: {variant.name}", SetupValidator.Severity.Fail,
                $"{orphaned.Length} override(s) on {byProperty.Length} propert{(byProperty.Length == 1 ? "y" : "ies")} " +
                $"point at objects that NO LONGER EXIST in the base Player.prefab ({preview}) — whatever they " +
                "customized (hand models, channels, ...) is silently gone.",
                $"Open '{AssetDatabase.GetAssetPath(variant)}', re-apply those customizations on the current base objects, " +
                "then run Tools/Jeanf/UniversalPlayer/Remove Dead Variant Overrides (it logs every entry it strips) — " +
                "or remove them by hand via the Overrides dropdown > Revert the entries showing missing targets.");
        }

        private static SetupValidator.CheckResult CheckStaleImportedSamples()
        {
            const string check = "Stale imported samples";
            if (!AssetDatabase.IsValidFolder("Assets/Samples"))
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass, "No Assets/Samples folder.");

            var stale = AssetDatabase.GetSubFolders("Assets/Samples")
                .Where(folder =>
                {
                    var name = System.IO.Path.GetFileName(folder);
                    return name.Contains("VR Player") || name.Contains("Universal Player") || name.Contains("UniversalPlayer");
                })
                .ToArray();
            var outdated = OutdatedPackageSampleFolders();

            if (stale.Length == 0 && outdated.Count == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    "No imported Universal Player samples, and every XR sample folder matches its installed package version.");

            var problems = new List<string>();
            var hints = new List<string>();
            if (stale.Length > 0)
            {
                problems.Add($"Old imported Universal Player sample folder(s): {string.Join(", ", stale)} — stale copies " +
                             "(hands and prefabs now ship inside the package Runtime) that can shadow or confuse references.");
                hints.Add("Delete the Universal Player sample folder(s) after confirming nothing in your scenes references them.");
            }
            if (outdated.Count > 0)
            {
                problems.Add($"XR sample folder(s) from ANOTHER version than the installed package: {string.Join(", ", outdated)} — " +
                             "their scripts (GazeInputManager, hand visualizers, ...) keep compiling against the new package: " +
                             "obsolete-API warnings, duplicate types, demo scenes that no longer load.");
                hints.Add("Delete the old version folder(s); if the project still needs that sample, re-import it from " +
                          "Package Manager > <package> > Samples (it lands under the installed version).");
            }

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                string.Join(" ", problems), string.Join(" ", hints));
        }

        // Package samples import as versioned copies: Assets/Samples/<Display Name>/<version>/.
        // After a package update the old folder stays behind and keeps compiling.
        private static readonly (string packageName, string displayFolder)[] VersionedSamplePackages =
        {
            ("com.unity.xr.interaction.toolkit", "XR Interaction Toolkit"),
            ("com.unity.xr.hands", "XR Hands"),
        };

        internal static List<string> OutdatedPackageSampleFolders()
        {
            var outdated = new List<string>();
            var installed = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                .ToDictionary(package => package.name, package => package.version);

            foreach (var (packageName, displayFolder) in VersionedSamplePackages)
            {
                var folder = $"Assets/Samples/{displayFolder}";
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                if (!installed.TryGetValue(packageName, out var version))
                {
                    outdated.Add($"{folder} ({packageName} is not installed)");
                    continue;
                }
                foreach (var versionFolder in AssetDatabase.GetSubFolders(folder))
                {
                    if (System.IO.Path.GetFileName(versionFolder) != version)
                        outdated.Add($"{versionFolder} (installed: {version})");
                }
            }
            return outdated;
        }

        public static List<SetupValidator.CheckResult> RunOpenSceneChecks()
        {
            var results = new List<SetupValidator.CheckResult>();

            var broadcaster = Object.FindAnyObjectByType<BroadcastControlsStatus>(FindObjectsInactive.Include);
            if (broadcaster == null)
            {
                results.Add(new SetupValidator.CheckResult("Scene: player", SetupValidator.Severity.Warning,
                    "No player (BroadcastControlsStatus) in the open scene — scene wiring checks skipped.",
                    "Open a scene containing the Player variant and validate again."));
                return results;
            }

            results.Add(CheckSceneUsesVariant(broadcaster.transform.root.gameObject));
            results.Add(CheckPlayerCamera(broadcaster.transform.root.gameObject));
            results.Add(CheckSingleGravitySystem(broadcaster.transform.root.gameObject));
            results.Add(CheckPlayerGroundCollision(broadcaster.transform.root.gameObject));
            results.Add(CheckPlayerEventBridge(broadcaster.transform.root.gameObject));
            results.Add(CheckCursorPalette(broadcaster.transform.root.gameObject));

            var noPeeking = Object.FindAnyObjectByType<NoPeeking>(FindObjectsInactive.Include);
            if (noPeeking == null)
            {
                results.Add(new SetupValidator.CheckResult("Scene: NoPeeking", SetupValidator.Severity.Warning,
                    "No NoPeeking in the scene — head-in-wall desaturation is absent.",
                    "It normally sits on the Player prefab; was it removed on the variant?"));
            }
            else
            {
                var layer = new SerializedObject(noPeeking).FindProperty("collisionLayer");
                results.Add(layer != null && layer.intValue == 0
                    ? new SetupValidator.CheckResult("Scene: NoPeeking", SetupValidator.Severity.Fail,
                        "NoPeeking.collisionLayer is Nothing on the scene player — head-in-wall detection is disabled.",
                        "Set the walls' layer on the NoPeeking component of your Player VARIANT (not the package prefab).")
                    : new SetupValidator.CheckResult("Scene: NoPeeking", SetupValidator.Severity.Pass,
                        "Collision layer is configured."));
            }

            results.Add(CheckTeleportWiring());
            results.Add(CheckXrModeManager(broadcaster.transform.root.gameObject));
            results.Add(CheckPlayerActionAssets(broadcaster.transform.root.gameObject));

            if (Object.FindAnyObjectByType<XrHealthMonitor>(FindObjectsInactive.Include) == null)
                results.Add(new SetupValidator.CheckResult("Scene: XR health monitor", SetupValidator.Severity.Warning,
                    "No XrHealthMonitor in the scene — headset/controller disconnects and low battery will not be reported.",
                    "Add XrHealthMonitor to the Player variant and assign its message/HMD channels."));
            else
                results.Add(new SetupValidator.CheckResult("Scene: XR health monitor", SetupValidator.Severity.Pass,
                    "XrHealthMonitor present."));

            results.Add(CheckFadeMaskProfile());
            results.Add(CheckFadeVolumeVisibleToCamera());
            results.Add(CheckCameraPostProcessing());
            results.Add(CheckSeatHeights());
            results.Add(CheckSeatColliders());
            results.Add(CheckSeatIds());
            results.Add(CheckSeatDataBridge());
            results.Add(CheckScenarioSeating());
            results.Add(CheckWorldSpaceCanvases());
            results.Add(CheckPickableRigidbodies());
            return results;
        }

        // The runtime only says "NO TeleportOnEvent accepted it" in play mode, AFTER the
        // teleport already failed. Every fact it lists is visible at edit time: a listener
        // exists, it receives on the SAME channel the scene's targets broadcast on, and its
        // OnEventRaised actually reaches Teleport. (Filters can still reject a matching
        // event — that part needs the runtime.) Public so the editor tests can drive it.
        public static SetupValidator.CheckResult CheckTeleportWiring()
        {
            const string check = "Scene: teleport listener";
            var listeners = Object.FindObjectsByType<TeleportOnEvent>(FindObjectsInactive.Include);
            var targets = Object.FindObjectsByType<SendTeleportTarget>(FindObjectsInactive.Include);

            if (listeners.Length == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No TeleportOnEvent in the scene — SendTeleportTarget events go nowhere (nothing teleports).",
                    "Add a TeleportOnEvent (usually on the Player variant), listening on your TeleportEventChannel, " +
                    "with OnEventRaised wired to its Teleport method.");

            var receivedChannels = new HashSet<Object>();
            var broken = new List<string>();
            foreach (var listener in listeners)
            {
                var so = new SerializedObject(listener);
                var channel = so.FindProperty("_channel")?.objectReferenceValue;
                if (channel == null) broken.Add($"'{listener.name}' has no 'Receiving on channel' asset");
                else receivedChannels.Add(channel);

                if (!PersistentCallsReach(so.FindProperty("OnEventRaised"), nameof(TeleportOnEvent.Teleport)))
                    broken.Add($"'{listener.name}': OnEventRaised is not wired to TeleportOnEvent.Teleport");
            }

            var unreceived = new List<string>();
            foreach (var target in targets)
            {
                var channel = new SerializedObject(target).FindProperty("_teleportChannel")?.objectReferenceValue;
                if (channel == null) unreceived.Add($"'{target.name}' broadcasts on NO channel");
                else if (!receivedChannels.Contains(channel))
                    unreceived.Add($"'{target.name}' broadcasts on '{channel.name}', which no listener here receives");
            }

            if (broken.Count > 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Fail,
                    $"TeleportOnEvent misconfigured: {string.Join("; ", broken)} — every teleport on that listener is dropped.",
                    "On the TeleportOnEvent (Player variant): set 'Receiving on channel' to the TeleportEventChannel your " +
                    "targets broadcast on, and add TeleportOnEvent.Teleport to its OnEventRaised (dynamic parameter).");

            if (unreceived.Count > 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    $"SendTeleportTarget(s) nobody in the loaded scenes listens to: {string.Join("; ", unreceived)} — " +
                    "those teleports do nothing (the runtime warns 'NO TeleportOnEvent accepted it'). Ignore if the " +
                    "matching listener lives in a scene that is only loaded at runtime.",
                    $"Point those targets at the channel the listener receives ({string.Join(", ", receivedChannels.Select(c => $"'{c.name}'"))}), " +
                    "or add a TeleportOnEvent for their channel. Still nothing moving? Check the listener's filters.");

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                $"{listeners.Length} TeleportOnEvent(s) wired on {receivedChannels.Count} channel(s); " +
                $"all {targets.Length} SendTeleportTarget(s) in the loaded scenes are received.");
        }

        // True when at least one persistent UnityEvent call targets the named method.
        private static bool PersistentCallsReach(SerializedProperty unityEvent, string methodName)
        {
            var calls = unityEvent?.FindPropertyRelative("m_PersistentCalls.m_Calls");
            if (calls == null) return false;
            for (var i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_Target")?.objectReferenceValue != null
                    && call.FindPropertyRelative("m_MethodName")?.stringValue == methodName)
                    return true;
            }
            return false;
        }

        // XrModeManager (v1.16.0) owns the desktop<->VR rendering switch: flat view after
        // VR, instant re-entry via the session keeper. Without a resolvable camera it does
        // nothing, and the two failure modes it exists to prevent come back silently.
        public static SetupValidator.CheckResult CheckXrModeManager(GameObject playerRoot)
        {
            const string check = "Scene: XR mode manager";
            var manager = playerRoot.GetComponentInChildren<XrModeManager>(true);
            if (manager == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No XrModeManager on the player — after leaving VR the desktop view stays a stereo left-eye " +
                    "mirror, and every VR re-entry restarts the XR display (seconds of black headset over Link).",
                    "It ships on the Player prefab (Settings/XR/XrModeManager) since v1.16.0 — update the package, or re-add it on your Player variant.");

            var so = new SerializedObject(manager);
            var managesCamera = so.FindProperty("manageCameraXrRendering")?.boolValue ?? true;
            var overrideCamera = so.FindProperty("playerCameraOverride")?.objectReferenceValue;
            var look = playerRoot.GetComponentInChildren<FPSCameraMovement>(true);
            var lookCamera = look != null ? new SerializedObject(look).FindProperty("playerCamera")?.objectReferenceValue : null;
            if (managesCamera && overrideCamera == null && lookCamera == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Fail,
                    "XrModeManager cannot resolve the player camera (no Player Camera Override, and FPSCameraMovement." +
                    "playerCamera is unassigned) — the camera never goes stereo in VR nor flat on desktop.",
                    "Assign the player's Camera on FPSCameraMovement (see 'Scene: player camera'), or set XrModeManager's " +
                    "Player Camera Override on your variant.");

            var stopMode = so.FindProperty("stopXrDisplayOnDesktop");
            if (stopMode != null && stopMode.enumValueIndex != (int)XrModeManager.DisplayStopMode.Never)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    $"Stop Xr Display On Desktop is '{stopMode.enumDisplayNames[stopMode.enumValueIndex]}' — every VR " +
                    "re-entry pays a full Link re-handshake (seconds of black headset) and the session keeper is defeated.",
                    "Set it back to Never on your Player variant unless a fully XR-free desktop state matters more than fast re-entry.");

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                "XrModeManager present, camera resolvable, XR display kept warm on desktop.");
        }

        // PlayerActionManager maps every action of the input asset to an ActionSO under
        // Resources/Player/Actions (rebinding + SO forwarding). A missing asset is only a
        // play-mode warning ("Did not found actionSO") — and the generated assets are
        // project files that are easy to forget after adding an action.
        public static SetupValidator.CheckResult CheckPlayerActionAssets(GameObject playerRoot)
        {
            const string check = "Scene: player action assets";
            var manager = playerRoot.GetComponentInChildren<PlayerActionManager>(true);
            if (manager == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No PlayerActionManager on the player — action rebinding and the per-action ScriptableObjects are unavailable.",
                    "It ships on the Player prefab root; re-add it on your variant if it was removed.");

            var so = new SerializedObject(manager);
            var missingRefs = new List<string>();
            if (so.FindProperty("_actionContainer")?.objectReferenceValue == null) missingRefs.Add("ActionContainerSO");
            if (so.FindProperty("actionRebindedListener")?.objectReferenceValue == null) missingRefs.Add("ActionRebindEventChannelSO");
            var asset = so.FindProperty("m_InputActionAsset")?.objectReferenceValue as InputActionAsset;
            if (asset == null) missingRefs.Add("InputActionAsset");
            if (missingRefs.Count > 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Fail,
                    $"PlayerActionManager on '{manager.gameObject.name}' has unassigned: {string.Join(", ", missingRefs)} — " +
                    "it cannot list, forward or rebind any action (NullReference at play).",
                    "Assign them on the PlayerActionManager of your Player variant (the package prefab ships them wired).");

            var missing = new List<string>();
            var total = 0;
            foreach (var action in asset)
            {
                total++;
                var assetName = $"{action.actionMap.name}_{action.name}";
                if (Resources.Load<ActionSO>($"Player/Actions/{assetName}") == null) missing.Add(assetName);
            }

            if (missing.Count > 0)
            {
                var preview = string.Join(", ", missing.Take(6)) + (missing.Count > 6 ? ", ..." : "");
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    $"{missing.Count} of {total} action(s) have no ActionSO under Resources/Player/Actions ({preview}) — " +
                    "the runtime warns 'Did not found actionSO' and those actions cannot be rebound or forwarded to their SO.",
                    "Press 'Create Player Actions' on the PlayerActionManager (or the Fix button in the Project Validation " +
                    "window), then commit Assets/Resources/Player/Actions.");
            }

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                $"All {total} action(s) of '{asset.name}' have their ActionSO under Resources/Player/Actions.");
        }

        // A PickableObject without a Rigidbody still grabs, but its physics cannot be
        // suspended while held or restored on release — the runtime warns once per object,
        // in play mode, with the headset on.
        public static SetupValidator.CheckResult CheckPickableRigidbodies()
        {
            const string check = "Scene: pickable rigidbodies";
            var pickables = Object.FindObjectsByType<PickableObject>(FindObjectsInactive.Include);
            if (pickables.Length == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass, "No PickableObject in the scene.");

            var missing = pickables.Where(p => p.GetComponent<Rigidbody>() == null).Select(p => $"'{p.name}'").ToArray();
            if (missing.Length == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    $"All {pickables.Length} pickable(s) have a Rigidbody.");

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                $"PickableObject without a Rigidbody: {string.Join(", ", missing)} — they can be picked up, but their " +
                "physics cannot be suspended while held or restored on release.",
                "Add a Rigidbody to each (tick Is Kinematic if the object must not fall when placed).");
        }

        // Scenario-driven seating: scenarios raise a Seat's GameObject on the
        // SitController's sit request channel (instant placement while the
        // loading fade is black). An unassigned channel means every scripted
        // sit request goes NOWHERE, silently.
        private static SetupValidator.CheckResult CheckScenarioSeating()
        {
            const string check = "Scene: scenario seating";
            var sitController = Object.FindAnyObjectByType<SitController>(FindObjectsInactive.Include);
            if (sitController == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No SitController in the scene — neither manual nor scenario-driven sitting can work.",
                    "It ships on the Player prefab (Move object); is the player missing or your variant outdated?");

            var bridge = Object.FindAnyObjectByType<PlayerEventBridge>(FindObjectsInactive.Include);
            var channelsAsset = bridge != null ? new SerializedObject(bridge).FindProperty("channels")?.objectReferenceValue : null;
            if (channelsAsset == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No PlayerEventBridge with a channels asset — sit requests (like every other project channel) have no route in.",
                    "See the 'Scene: player event bridge' result.");

            var channel = new SerializedObject(channelsAsset).FindProperty("sitRequest");
            if (channel == null || channel.objectReferenceValue == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    $"The 'sitRequest' slot on '{channelsAsset.name}' is empty — scripted sit requests (scenario loads that " +
                    "seat the player under the black fade) go nowhere. (Ignore if this project never seats the player by script.)",
                    "Assign a GameObjectEventChannelSO (the package ships SitEventChannelSO in Runtime/scripts/Sitting/) on the " +
                    "PlayerChannels asset's sitRequest slot AND raise it from the scenario logic (Seat GameObject = sit, null = stand).");

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                $"Scenario seating listens on '{channel.objectReferenceValue.name}' (via the bridge).");
        }

        // Sitting must LOWER the view: a seat whose seated eye height lands at or
        // above the standing eye height reads as levitating (the runtime clamps
        // it, but the authored setup should be fixed).
        private static SetupValidator.CheckResult CheckSeatHeights()
        {
            const string check = "Scene: seat heights";
            var seats = Object.FindObjectsByType<Seat>(FindObjectsInactive.Include);
            if (seats.Length == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass, "No Seat in the scene.");

            var sitController = Object.FindAnyObjectByType<SitController>(FindObjectsInactive.Include);
            var standingHeight = sitController != null ? sitController.StandingCameraHeight : 1.7f;

            var offenders = new List<string>();
            foreach (var seat in seats)
            {
                var seatedEyeY = seat.SitAnchor.position.y + seat.EyeHeightAboveSeat;
                var groundY = seat.ExitAnchor != null ? seat.ExitAnchor.position.y : seat.SitAnchor.position.y;
                if (seatedEyeY >= groundY + standingHeight - 0.05f)
                    offenders.Add($"'{seat.name}' (seated {seatedEyeY:F2}m vs standing ~{groundY + standingHeight:F2}m)");
            }

            if (offenders.Count == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    $"All {seats.Length} seat(s) put the seated eyes below the standing eyes.");

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                $"Seated eye height is NOT below the standing eye height on: {string.Join(", ", offenders)}. " +
                "The runtime clamps it, but the intent should be authored.",
                "Select the Seat to see the height gizmos (cyan = seated eyes, yellow = standing eyes) and lower the " +
                "sit anchor or 'Eye Height Above Seat'. If a seat has no Exit Anchor the standing estimate uses the sit " +
                "anchor as ground — add an Exit Anchor for an accurate check.");
        }

        // Scenario targeting (SitPlayerOnEnable by Seat Id) resolves seats through the
        // SeatRegistry — a DUPLICATE id makes the target ambiguous (newest registration
        // wins, silently the wrong chair), and a SitPlayerOnEnable with neither a Seat
        // nor an id can never seat anyone. Only loaded scenes can be checked here; ids
        // must be unique across the whole project (door-system convention).
        private static SetupValidator.CheckResult CheckSeatIds()
        {
            const string check = "Scene: seat ids (scenario targeting)";
            var seats = Object.FindObjectsByType<Seat>(FindObjectsInactive.Include);

            var byId = new Dictionary<int, List<string>>();
            foreach (var seat in seats)
            {
                if (seat.AuthoredSeatId == 0) continue;
                if (!byId.TryGetValue(seat.AuthoredSeatId, out var list)) byId[seat.AuthoredSeatId] = list = new List<string>();
                list.Add($"'{seat.name}'");
            }
            var duplicates = new List<string>();
            foreach (var kv in byId)
                if (kv.Value.Count > 1) duplicates.Add($"id {kv.Key}: {string.Join(", ", kv.Value)}");

            var untargeted = new List<string>();
            foreach (var forceSit in Object.FindObjectsByType<SitPlayerOnEnable>(FindObjectsInactive.Include))
            {
                var so = new SerializedObject(forceSit);
                if (so.FindProperty("seat").objectReferenceValue == null && so.FindProperty("seatId").intValue == 0)
                    untargeted.Add($"'{forceSit.name}'");
            }

            if (duplicates.Count == 0 && untargeted.Count == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    $"{byId.Count} targetable seat id(s), no duplicates; every SitPlayerOnEnable has a target.");

            var problems = new List<string>();
            if (duplicates.Count > 0) problems.Add($"duplicate Seat Ids ({string.Join("; ", duplicates)})");
            if (untargeted.Count > 0) problems.Add($"SitPlayerOnEnable with no Seat and no Seat Id ({string.Join(", ", untargeted)})");
            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Fail,
                string.Join(" and ", problems) + ".",
                "Give every scenario-targeted Seat a unique non-zero Seat Id (unique across ALL scenes — only loaded " +
                "ones are checked here) and point each SitPlayerOnEnable at a Seat or a Seat Id.");
        }

        // A Seat needs a collider to be aimed at. For the entity world (baked into a
        // SubScene) the runtime proxy is placed at the Seat ROOT with the root's BoxCollider,
        // so a missing collider (can't aim at all) or a collider only on a CHILD / non-box
        // (proxy misaligned or default-sized) breaks the baked case.
        private static SetupValidator.CheckResult CheckSeatColliders()
        {
            const string check = "Scene: seat colliders";
            var seats = Object.FindObjectsByType<Seat>(FindObjectsInactive.Include);
            if (seats.Length == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass, "No Seat in the scene.");

            var noCollider = new List<string>();
            var notBoxOnRoot = new List<string>();
            foreach (var seat in seats)
            {
                if (seat.GetComponentInChildren<Collider>() == null) { noCollider.Add($"'{seat.name}'"); continue; }
                if (seat.GetComponent<BoxCollider>() == null) notBoxOnRoot.Add($"'{seat.name}'");
            }

            if (noCollider.Count == 0 && notBoxOnRoot.Count == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    $"All {seats.Length} seat(s) have a BoxCollider on the Seat root.");

            var msg = "";
            if (noCollider.Count > 0)
                msg += $"No collider — cannot be aimed at, and cannot bake a proxy in a SubScene: {string.Join(", ", noCollider)}. ";
            if (notBoxOnRoot.Count > 0)
                msg += $"No BoxCollider on the Seat root (it's on a child or a non-box collider) — the baked entity-world " +
                       $"proxy is placed at the root and will be misaligned/default-sized: {string.Join(", ", notBoxOnRoot)}. ";

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning, msg.TrimEnd(),
                "Put a BoxCollider on the Seat's own GameObject (the one with the Seat script) so it works both in the " +
                "additive-scene flow and when baked into a SubScene.");
        }

        // Baked seats live in SubScenes as pure data — the SeatDataBridge spawns the collider
        // proxies that make them hoverable/clickable/VR-selectable. Without it, SubScene seats are
        // dead. Closed SubScenes hide their Seats at edit time, so SubScenes-present is the signal.
        private static SetupValidator.CheckResult CheckSeatDataBridge()
        {
            const string check = "Scene: seat data bridge";

            var usesSubScenes = Object.FindObjectsByType<Unity.Scenes.SubScene>(FindObjectsInactive.Include).Length > 0;
            if (!usesSubScenes)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    "No SubScenes in the scene — a SeatDataBridge only matters for seats baked into a SubScene.");

            if (Object.FindAnyObjectByType<SeatDataBridge>(FindObjectsInactive.Include) != null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    "A SeatDataBridge is present for baked seats.");

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                "SubScenes are in use but there is no SeatDataBridge in the loaded scenes — any seat baked into a " +
                "SubScene will not be hoverable, clickable or VR-selectable.",
                "Add a SeatDataBridge component to an always-loaded GameObject (e.g. a manager in your main scene).");
        }

        // A camera only evaluates volumes on layers in its Volume Mask (HDRP:
        // HDAdditionalCameraData.volumeLayerMask, URP: m_VolumeLayerMask). The
        // fade volume typically sits on a custom layer (e.g. "Player") — if the
        // mask excludes it, EVERY fade silently no-ops even with a perfect
        // profile. Read through SerializedObject so this works on both
        // pipelines without assembly references.
        private static SetupValidator.CheckResult CheckFadeVolumeVisibleToCamera()
        {
            const string check = "Scene: fade volume vs camera mask";
            var fadeMask = Object.FindAnyObjectByType<FadeMask>(FindObjectsInactive.Include);
            if (fadeMask == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No FadeMask in the scene — nothing to check.", "See the 'Scene: fade profile' result.");

            var volume = new SerializedObject(fadeMask).FindProperty("postProcessVolume")?.objectReferenceValue
                as UnityEngine.Rendering.Volume;
            if (volume == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "FadeMask has no Volume assigned — nothing to check.", "See the 'Scene: fade profile' result.");

            var camera = fadeMask.GetComponentInParent<Camera>(true);
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            if (camera == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No Camera found — cannot verify the volume mask.", "Add the Player variant to the scene.");

            var additionalData = camera.GetComponent("HDAdditionalCameraData") ?? camera.GetComponent("UniversalAdditionalCameraData");
            if (additionalData == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    "No SRP camera data (built-in pipeline?) — volume masking does not apply.");

            var so = new SerializedObject(additionalData);
            var maskProperty = so.FindProperty("volumeLayerMask") ?? so.FindProperty("m_VolumeLayerMask");
            if (maskProperty == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    "Camera data exposes no volume mask — nothing to verify.");

            var layer = volume.gameObject.layer;
            if ((maskProperty.intValue & (1 << layer)) == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Fail,
                    $"The camera's Volume Mask does NOT include layer '{LayerMask.LayerToName(layer)}' where the fade volume " +
                    $"('{volume.gameObject.name}') lives — the volume is ignored and EVERY fade silently no-ops.",
                    $"On the camera '{camera.gameObject.name}': add '{LayerMask.LayerToName(layer)}' to the Volume Mask " +
                    "(HDRP: HD Additional Camera Data > Volume Mask; URP: Camera > Rendering > Volume Mask).");

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                $"Camera volume mask includes layer '{LayerMask.LayerToName(layer)}'.");
        }

        // URP applies volume post-processing to a camera ONLY when its "Post Processing"
        // toggle is on (UniversalAdditionalCameraData.m_RenderPostProcessing) — and that
        // toggle defaults to OFF on every new URP camera. With it off, a correctly
        // configured (even global) fade volume renders NOTHING and EVERY fade silently
        // no-ops. HDRP enables post-processing through frame settings (on by default) and
        // has no equivalent per-camera toggle, so this is URP-only. Read through
        // SerializedObject to avoid a hard URP assembly reference.
        private static SetupValidator.CheckResult CheckCameraPostProcessing()
        {
            const string check = "Scene: camera post-processing (URP)";
            var fadeMask = Object.FindAnyObjectByType<FadeMask>(FindObjectsInactive.Include);
            if (fadeMask == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No FadeMask in the scene — nothing to check.", "See the 'Scene: fade profile' result.");

            var camera = fadeMask.GetComponentInParent<Camera>(true);
            if (camera == null) camera = Object.FindAnyObjectByType<Camera>(FindObjectsInactive.Include);
            if (camera == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No Camera found — cannot verify post-processing.", "Add the Player variant to the scene.");

            var urpData = camera.GetComponent("UniversalAdditionalCameraData");
            if (urpData == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    "Not a URP camera (built-in/HDRP) — the per-camera post-processing toggle does not apply.");

            var renderPostProcessing = new SerializedObject(urpData).FindProperty("m_RenderPostProcessing");
            if (renderPostProcessing == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    "URP camera data exposes no post-processing toggle — nothing to verify.");

            if (!renderPostProcessing.boolValue)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Fail,
                    $"'Post Processing' is OFF on camera '{camera.gameObject.name}' — URP applies no volume overrides " +
                    "with it off, so the (global) fade volume renders nothing and EVERY fade (loading black screen, " +
                    "head-in-wall, menu) silently no-ops.",
                    $"Select '{camera.gameObject.name}' (on your Player variant) and tick Camera > Rendering > Post Processing.");

            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                $"Post Processing is enabled on camera '{camera.gameObject.name}'.");
        }

        private static SetupValidator.CheckResult CheckFadeMaskProfile()
        {
            const string check = "Scene: fade profile";
            var fadeMask = Object.FindAnyObjectByType<FadeMask>(FindObjectsInactive.Include);
            if (fadeMask == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No FadeMask in the scene — no black loading screen, no head-in-wall fade, no menu fade.",
                    "It normally sits on the Player prefab; was it removed on the variant?");

            if (fadeMask.IsValid)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                    "FadeMask volume + profile match the active render pipeline.");

            var prefix = FadeMask.ActivePipelinePrefix();
            var detail = fadeMask.EffectiveProfile == null
                ? "FadeMask has no Volume or no profile assigned"
                : $"FadeMask's profile '{fadeMask.EffectiveProfile.name}' has no {prefix} ColorAdjustments (wrong pipeline)";
            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Fail,
                $"{detail} — EVERY fade (loading black screen, head-in-wall, menu) silently no-ops, and the build will fail validation.",
                $"Select the FadeMask on the Player variant and press its 'Fix: assign the bundled {prefix} FadeGlobalVolume Profile' button.");
        }

        private static SetupValidator.CheckResult CheckPlayerCamera(GameObject playerRoot)
        {
            var camera = playerRoot.GetComponentsInChildren<Camera>(true).FirstOrDefault(c => c.enabled);
            if (camera == null)
                return new SetupValidator.CheckResult("Scene: player camera", SetupValidator.Severity.Fail,
                    $"No enabled Camera anywhere under '{playerRoot.name}' — the game view is blank and " +
                    "FPSCameraMovement/TakeObject/PerformAction have nothing to work with.",
                    "The package prefab ships a Camera on CameraOffset/CameraFeel/Main Camera since v0.9.33. " +
                    "Update the package, or if your variant removed/overrode it, re-enable it (and remove any " +
                    "duplicate Camera your variant added earlier — Unity allows only one per GameObject).");

            var look = playerRoot.GetComponentsInChildren<FPSCameraMovement>(true).FirstOrDefault();
            if (look != null)
            {
                var referenced = new SerializedObject(look).FindProperty("playerCamera");
                if (referenced != null && referenced.objectReferenceValue == null)
                    return new SetupValidator.CheckResult("Scene: player camera", SetupValidator.Severity.Fail,
                        $"FPSCameraMovement.playerCamera is unassigned on '{look.gameObject.name}' although " +
                        $"'{camera.gameObject.name}' has a Camera — camera reset/FOV handling will NullRef.",
                        "Assign the player's Camera to the FPSCameraMovement component on your variant.");
            }

            return new SetupValidator.CheckResult("Scene: player camera", SetupValidator.Severity.Pass,
                $"Enabled Camera found on '{camera.gameObject.name}'.");
        }

        private static SetupValidator.CheckResult CheckPlayerEventBridge(GameObject playerRoot)
        {
            var bridge = playerRoot.GetComponentInChildren<PlayerEventBridge>(true);
            if (bridge == null)
                return new SetupValidator.CheckResult("Scene: player event bridge", SetupValidator.Severity.Fail,
                    "No PlayerEventBridge on the player — EVERY event between the player and the project is silent " +
                    "(teleports in, movement/seated/XR reports out).",
                    "The package prefab ships one under Settings/Events/PlayerEventBridge since 0.10.0 — update the package, or re-add the " +
                    "component and assign a PlayerChannelsSO if the variant removed it.");

            var channels = new SerializedObject(bridge).FindProperty("channels").objectReferenceValue as PlayerChannelsSO;
            if (channels == null)
                return new SetupValidator.CheckResult("Scene: player event bridge", SetupValidator.Severity.Fail,
                    "PlayerEventBridge has no PlayerChannelsSO assigned — same silence as having no bridge.",
                    "Assign the packaged UniversalPlayerChannels asset, or your project's duplicate of it.");

            // The packaged default proves the wiring but belongs to the package: consumers
            // cannot edit assets under Packages/, and dev-repo edits ship to everyone.
            // Projects must assign their own local copy.
            var channelsPath = AssetDatabase.GetAssetPath(channels);
            var packageRoot = PackageRoot();
            if (!string.IsNullOrEmpty(packageRoot) && channelsPath.StartsWith(packageRoot))
                return new SetupValidator.CheckResult("Scene: player event bridge", SetupValidator.Severity.Warning,
                    $"The bridge uses the PACKAGED '{channels.name}' — that asset cannot be edited in consumer " +
                    "projects and package updates overwrite it.",
                    "Run Tools/Jeanf/UniversalPlayer/Create Local Player Channels (duplicates it into Assets/ and assigns " +
                    "it to the bridge), then apply the override to your Player variant.");

            // Optional slots: features a project may legitimately not use (no fall-recovery
            // toast, no pause flow, no map/inventory UI).
            var optional = new[] { "fallRecoveryMessage", "pause", "toggleMap", "toggleInventory" };
            var empty = new List<string>();
            var iterator = new SerializedObject(channels).GetIterator();
            for (var enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
            {
                if (iterator.propertyType != SerializedPropertyType.ObjectReference || iterator.name == "m_Script") continue;
                if (iterator.objectReferenceValue == null && !optional.Contains(iterator.name)) empty.Add(iterator.name);
            }

            if (empty.Count > 0)
                return new SetupValidator.CheckResult("Scene: player event bridge", SetupValidator.Severity.Warning,
                    $"PlayerChannelsSO '{channels.name}' has empty slot(s): {string.Join(", ", empty)} — the matching " +
                    "boundary events are silent for the project (internals still work over PlayerEvents).",
                    "Point each slot at the project's channel asset (the packaged UniversalPlayerChannels shows the defaults).");

            return new SetupValidator.CheckResult("Scene: player event bridge", SetupValidator.Severity.Pass,
                $"Bridge present, '{channels.name}' fully wired.");
        }

        private static SetupValidator.CheckResult CheckCursorPalette(GameObject playerRoot)
        {
            const string name = "Scene: cursor palette";
            var cursor = playerRoot.GetComponentInChildren<CursorStateController>(true);
            if (cursor == null)
                return new SetupValidator.CheckResult(name, SetupValidator.Severity.Fail,
                    "No CursorStateController on the player — no cursor/reticle, and the interaction ray has no colours.",
                    "The package prefab ships one under Settings/UI/CursorStateController; re-add it if the variant removed it.");

            var palette = new SerializedObject(cursor).FindProperty("palette").objectReferenceValue as CursorPaletteSO;
            if (palette == null)
                return new SetupValidator.CheckResult(name, SetupValidator.Severity.Fail,
                    "CursorStateController has no CursorPaletteSO — cursor AND interaction ray fall back to code defaults, " +
                    "and the project cannot restyle them.",
                    "Run Tools/Jeanf/UniversalPlayer/Create Local Cursor Palette (duplicates the packaged CursorPalette into " +
                    "Assets/ and assigns it), then apply the override to your Player variant.");

            // The packaged default proves the wiring but belongs to the package: consumers
            // cannot edit assets under Packages/, and dev-repo edits ship to everyone. The
            // project must own its palette so cursor and ray can be restyled together.
            var palettePath = AssetDatabase.GetAssetPath(palette);
            var packageRoot = PackageRoot();
            if (!string.IsNullOrEmpty(packageRoot) && palettePath.StartsWith(packageRoot))
                return new SetupValidator.CheckResult(name, SetupValidator.Severity.Warning,
                    $"The cursor uses the PACKAGED '{palette.name}' — that asset cannot be edited in consumer projects and " +
                    "package updates overwrite it.",
                    "Run Tools/Jeanf/UniversalPlayer/Create Local Cursor Palette (duplicates it into Assets/ and assigns it), " +
                    "then apply the override to your Player variant.");

            return new SetupValidator.CheckResult(name, SetupValidator.Severity.Pass,
                $"Project-local palette '{palette.name}' drives the cursor and the interaction ray.");
        }

        private static SetupValidator.CheckResult CheckPlayerGroundCollision(GameObject playerRoot)
        {
            var controller = playerRoot.GetComponentInChildren<CharacterController>(true);
            if (controller == null)
                return new SetupValidator.CheckResult("Scene: player ground collision", SetupValidator.Severity.Warning,
                    "No CharacterController under the player — ground collision check skipped.",
                    "The package Player prefab ships one; was it removed on the variant?");

            var playerLayer = controller.gameObject.layer;
            var mask = 0;
            for (var i = 0; i < 32; i++)
            {
                if (!Physics.GetIgnoreLayerCollision(playerLayer, i)) mask |= 1 << i;
            }

            var layerName = LayerMask.LayerToName(playerLayer);
            if (mask == 0)
                return new SetupValidator.CheckResult("Scene: player ground collision", SetupValidator.Severity.Fail,
                    $"The player's layer '{layerName}' ({playerLayer}) collides with NOTHING in the Physics Layer " +
                    "Collision Matrix — the capsule cannot stand on any floor and falls through the world.",
                    "Project Settings > Physics > Layer Collision Matrix: enable collisions for that layer, or put the " +
                    "Player variant root on a layer that collides with your floor layers.");

            var origin = controller.transform.TransformPoint(controller.center);
            if (Physics.Raycast(origin, Vector3.down, out var hit, 1000f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var hitLayer = hit.collider.gameObject.layer;
                if ((mask & (1 << hitLayer)) == 0)
                    return new SetupValidator.CheckResult("Scene: player ground collision", SetupValidator.Severity.Fail,
                        $"The floor below the player ('{hit.collider.name}' on layer '{LayerMask.LayerToName(hitLayer)}') " +
                        $"does NOT collide with the player's layer '{layerName}' — the capsule falls straight through it.",
                        "Enable that pair in Project Settings > Physics > Layer Collision Matrix, or move the player/floor " +
                        "to layers that collide.");

                return new SetupValidator.CheckResult("Scene: player ground collision", SetupValidator.Severity.Pass,
                    $"Player (layer '{layerName}') can land on '{hit.collider.name}' below.");
            }

            return new SetupValidator.CheckResult("Scene: player ground collision", SetupValidator.Severity.Warning,
                "Nothing found below the player in this scene — fine if floors load additively at runtime " +
                "(the runtime gravity hold covers that), wrong if this scene should contain the floor.",
                "PlayerMovement holds gravity until landable ground exists below, and logs the reason.");
        }

        private static SetupValidator.CheckResult CheckSingleGravitySystem(GameObject playerRoot)
        {
            // PlayerMovement applies its own (swept, constant-velocity) gravity in every mode.
            // XRI's gravity ACCELERATES and can move the origin transform without collision
            // sweeping - with both enabled the player falls straight through the floor.
            var offenders = new List<string>();

            foreach (var move in playerRoot.GetComponentsInChildren<
                         UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>(true))
            {
#pragma warning disable CS0618 // deprecated on newer XRI, still serialized and migrated at runtime
                if (move.useGravity) offenders.Add($"{move.GetType().Name} on '{move.gameObject.name}'");
#pragma warning restore CS0618
            }

            foreach (var gravity in playerRoot.GetComponentsInChildren<
                         UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity.GravityProvider>(true))
            {
                if (gravity.useGravity) offenders.Add($"GravityProvider on '{gravity.gameObject.name}'");
            }

            if (offenders.Count == 0)
                return new SetupValidator.CheckResult("Scene: single gravity system", SetupValidator.Severity.Pass,
                    "No XRI gravity competing with PlayerMovement.");

            return new SetupValidator.CheckResult("Scene: single gravity system", SetupValidator.Severity.Fail,
                $"XRI gravity is enabled on: {string.Join(", ", offenders)} — combined with PlayerMovement's " +
                "own gravity the player falls through the floor the moment the XR Origin has a camera.",
                "Disable 'Use Gravity' on those components (PlayerMovement handles gravity in all modes), " +
                "or if you want XRI to own gravity instead, say so and disable it in PlayerMovement.");
        }

        private static SetupValidator.CheckResult CheckSceneUsesVariant(GameObject playerRoot)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(playerRoot);
            if (source == null)
                return new SetupValidator.CheckResult("Scene: player is a variant", SetupValidator.Severity.Warning,
                    $"'{playerRoot.name}' is not a prefab instance — customizations live only in this scene.",
                    "Use an instance of your project's Player prefab VARIANT instead.");

            var sourcePath = AssetDatabase.GetAssetPath(source);
            var packagePlayerPath = PlayerPrefabPath();
            if (sourcePath == packagePlayerPath)
                return new SetupValidator.CheckResult("Scene: player is a variant", SetupValidator.Severity.Fail,
                    $"'{playerRoot.name}' instantiates the PACKAGE Player.prefab directly — " +
                    "every custom link on it will be lost or orphaned on package updates.",
                    "Create a prefab variant of Player.prefab in Assets/, move your customizations there, " +
                    "and replace the scene instance with the variant.");

            return new SetupValidator.CheckResult("Scene: player is a variant", SetupValidator.Severity.Pass,
                $"Player instance comes from '{sourcePath}'.");
        }

        private static SetupValidator.CheckResult CheckWorldSpaceCanvases()
        {
            var interactiveCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include)
                .Where(canvas => canvas.renderMode == RenderMode.WorldSpace && HasInteractiveUi(canvas))
                .ToArray();
            if (interactiveCanvases.Length == 0)
                return new SetupValidator.CheckResult("Scene: XR-clickable UI", SetupValidator.Severity.Pass,
                    "No interactive world-space canvases in the scene (display-only ones need no raycaster).");

            var notClickable = interactiveCanvases
                .Where(canvas => canvas.GetComponent("TrackedDeviceGraphicRaycaster") == null)
                .Select(canvas => canvas.name)
                .ToArray();

            if (notClickable.Length == 0)
                return new SetupValidator.CheckResult("Scene: XR-clickable UI", SetupValidator.Severity.Pass,
                    $"All {interactiveCanvases.Length} interactive world-space canvas(es) have a TrackedDeviceGraphicRaycaster.");

            return new SetupValidator.CheckResult("Scene: XR-clickable UI", SetupValidator.Severity.Warning,
                $"Interactive world-space canvas(es) without TrackedDeviceGraphicRaycaster: {string.Join(", ", notClickable)} — " +
                "the VR finger ray cannot click them (mouse still can).",
                "Add a TrackedDeviceGraphicRaycaster component to each canvas meant to be used in VR.");
        }

        // Display-only canvases (tooltips, labels, HUDs) legitimately have no
        // raycaster at all — only flag a canvas if something on it can actually
        // receive clicks: a Selectable (Button/Toggle/...), a custom pointer
        // handler, or a GraphicRaycaster already declaring click intent.
        private static bool HasInteractiveUi(Canvas canvas)
        {
            if (canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null) return true;
            if (canvas.GetComponentInChildren<UnityEngine.UI.Selectable>(true) != null) return true;

            return canvas.GetComponentsInChildren<UnityEngine.EventSystems.IEventSystemHandler>(true).Any(handler =>
                handler is UnityEngine.EventSystems.IPointerClickHandler
                    or UnityEngine.EventSystems.IPointerDownHandler
                    or UnityEngine.EventSystems.IPointerUpHandler
                    or UnityEngine.EventSystems.IDragHandler);
        }
    }
}
