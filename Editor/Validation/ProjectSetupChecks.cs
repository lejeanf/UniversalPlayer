using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Project-level checks for Tools/UniversalPlayer/ValidateSetup: enforces the
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

            results.Add(CheckActiveInputHandling());

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

        private static List<GameObject> FindPlayerVariants(GameObject playerPrefab, string packageRoot)
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
        // The package is NEW Input System only. The editor defines mirror
        // Project Settings > Player > Active Input Handling, so this stays
        // correct without poking at ProjectSettings.asset.
        private static SetupValidator.CheckResult CheckActiveInputHandling()
        {
            const string check = "Project: input handling";
#if !ENABLE_INPUT_SYSTEM
            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Fail,
                "Active Input Handling excludes the new Input System — NOTHING in UniversalPlayer can read input " +
                "(all bindings live in an InputActionAsset).",
                "Project Settings > Player > Other Settings > Active Input Handling -> 'Input System Package (new)' " +
                "(Unity restarts the editor).");
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                "Active Input Handling is 'Both' — the deprecated legacy Input Manager is still enabled (Unity warns about " +
                "it on every load, and legacy UnityEngine.Input calls can silently creep back into project code).",
                "UniversalPlayer only needs the new system. KEEP 'Both' if a third-party asset requires legacy input " +
                "(e.g. Vuplex 3D WebView's hardware keyboard) — otherwise switch to 'Input System Package (new)' after " +
                "checking project scripts for UnityEngine.Input usage ('Input.GetKey', 'Input.GetAxis', 'Input.mousePosition').");
#else
            return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass,
                "Input System (new) only — the legacy Input Manager is off.");
#endif
        }

        private static SetupValidator.CheckResult CheckOrphanedOverrides(GameObject variant)
        {
            var modifications = PrefabUtility.GetPropertyModifications(variant);
            if (modifications == null)
                return new SetupValidator.CheckResult($"Variant overrides: {variant.name}", SetupValidator.Severity.Pass,
                    "No overrides recorded.");

            var orphaned = modifications
                .Where(m => m.target == null)
                .Select(m => m.propertyPath)
                .Distinct()
                .ToArray();

            if (orphaned.Length == 0)
                return new SetupValidator.CheckResult($"Variant overrides: {variant.name}", SetupValidator.Severity.Pass,
                    "All overrides target objects that still exist in the base prefab.");

            var preview = string.Join(", ", orphaned.Take(6)) + (orphaned.Length > 6 ? ", ..." : "");
            return new SetupValidator.CheckResult($"Variant overrides: {variant.name}", SetupValidator.Severity.Fail,
                $"{orphaned.Length} override(s) point at objects that NO LONGER EXIST in the base Player.prefab " +
                $"(e.g. {preview}) — whatever they customized (hand models, channels, ...) is silently gone.",
                $"Open '{AssetDatabase.GetAssetPath(variant)}', re-apply those customizations on the current base objects, " +
                "then remove the dead overrides (Overrides dropdown > Revert the entries showing missing targets).");
        }

        private static SetupValidator.CheckResult CheckStaleImportedSamples()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Samples"))
                return new SetupValidator.CheckResult("Stale imported samples", SetupValidator.Severity.Pass,
                    "No Assets/Samples folder.");

            var stale = AssetDatabase.GetSubFolders("Assets/Samples")
                .Where(folder =>
                {
                    var name = System.IO.Path.GetFileName(folder);
                    return name.Contains("VR Player") || name.Contains("Universal Player") || name.Contains("UniversalPlayer");
                })
                .ToArray();

            if (stale.Length == 0)
                return new SetupValidator.CheckResult("Stale imported samples", SetupValidator.Severity.Pass,
                    "No imported Universal Player samples found.");

            return new SetupValidator.CheckResult("Stale imported samples", SetupValidator.Severity.Warning,
                $"Old imported sample folder(s): {string.Join(", ", stale)} — these are stale copies " +
                "(hands and prefabs now ship inside the package Runtime) and can shadow or confuse references.",
                "Delete the folder(s) after confirming nothing in your scenes references them " +
                "(the broken-reference check in this validator will tell you if something did).");
        }

        public static List<SetupValidator.CheckResult> RunOpenSceneChecks()
        {
            var results = new List<SetupValidator.CheckResult>();

            var broadcaster = Object.FindFirstObjectByType<BroadcastControlsStatus>(FindObjectsInactive.Include);
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

            var noPeeking = Object.FindFirstObjectByType<NoPeeking>(FindObjectsInactive.Include);
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

            if (Object.FindFirstObjectByType<TeleportOnEvent>(FindObjectsInactive.Include) == null)
                results.Add(new SetupValidator.CheckResult("Scene: teleport listener", SetupValidator.Severity.Warning,
                    "No TeleportOnEvent in the scene — SendTeleportTarget events go nowhere (nothing teleports).",
                    "Add a TeleportOnEvent (usually on the Player variant), listening on your TeleportEventChannel, " +
                    "with OnEventRaised wired to its Teleport method."));
            else
                results.Add(new SetupValidator.CheckResult("Scene: teleport listener", SetupValidator.Severity.Pass,
                    "TeleportOnEvent present."));

            if (Object.FindFirstObjectByType<XrHealthMonitor>(FindObjectsInactive.Include) == null)
                results.Add(new SetupValidator.CheckResult("Scene: XR health monitor", SetupValidator.Severity.Warning,
                    "No XrHealthMonitor in the scene — headset/controller disconnects and low battery will not be reported.",
                    "Add XrHealthMonitor to the Player variant and assign its message/HMD channels."));
            else
                results.Add(new SetupValidator.CheckResult("Scene: XR health monitor", SetupValidator.Severity.Pass,
                    "XrHealthMonitor present."));

            results.Add(CheckFadeMaskProfile());
            results.Add(CheckFadeVolumeVisibleToCamera());
            results.Add(CheckSeatHeights());
            results.Add(CheckScenarioSeating());
            results.Add(CheckWorldSpaceCanvases());
            return results;
        }

        // Scenario-driven seating: scenarios raise a Seat's GameObject on the
        // SitController's sit request channel (instant placement while the
        // loading fade is black). An unassigned channel means every scripted
        // sit request goes NOWHERE, silently.
        private static SetupValidator.CheckResult CheckScenarioSeating()
        {
            const string check = "Scene: scenario seating";
            var sitController = Object.FindFirstObjectByType<SitController>(FindObjectsInactive.Include);
            if (sitController == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No SitController in the scene — neither manual nor scenario-driven sitting can work.",
                    "It ships on the Player prefab (Move object); is the player missing or your variant outdated?");

            var bridge = Object.FindFirstObjectByType<PlayerEventBridge>(FindObjectsInactive.Include);
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
            var seats = Object.FindObjectsByType<Seat>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (seats.Length == 0)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Pass, "No Seat in the scene.");

            var sitController = Object.FindFirstObjectByType<SitController>(FindObjectsInactive.Include);
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

        // A camera only evaluates volumes on layers in its Volume Mask (HDRP:
        // HDAdditionalCameraData.volumeLayerMask, URP: m_VolumeLayerMask). The
        // fade volume typically sits on a custom layer (e.g. "Player") — if the
        // mask excludes it, EVERY fade silently no-ops even with a perfect
        // profile. Read through SerializedObject so this works on both
        // pipelines without assembly references.
        private static SetupValidator.CheckResult CheckFadeVolumeVisibleToCamera()
        {
            const string check = "Scene: fade volume vs camera mask";
            var fadeMask = Object.FindFirstObjectByType<FadeMask>(FindObjectsInactive.Include);
            if (fadeMask == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "No FadeMask in the scene — nothing to check.", "See the 'Scene: fade profile' result.");

            var volume = new SerializedObject(fadeMask).FindProperty("postProcessVolume")?.objectReferenceValue
                as UnityEngine.Rendering.Volume;
            if (volume == null)
                return new SetupValidator.CheckResult(check, SetupValidator.Severity.Warning,
                    "FadeMask has no Volume assigned — nothing to check.", "See the 'Scene: fade profile' result.");

            var camera = fadeMask.GetComponentInParent<Camera>(true);
            if (camera == null) camera = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
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

        private static SetupValidator.CheckResult CheckFadeMaskProfile()
        {
            const string check = "Scene: fade profile";
            var fadeMask = Object.FindFirstObjectByType<FadeMask>(FindObjectsInactive.Include);
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
                    "The package prefab ships one on the Player root since 0.10.0 — update the package, or re-add the " +
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
                    "Run Tools/UniversalPlayer/Create Local Player Channels (duplicates it into Assets/ and assigns " +
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
            var worldCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(canvas => canvas.renderMode == RenderMode.WorldSpace)
                .ToArray();
            if (worldCanvases.Length == 0)
                return new SetupValidator.CheckResult("Scene: XR-clickable UI", SetupValidator.Severity.Pass,
                    "No world-space canvases in the scene.");

            var notClickable = worldCanvases
                .Where(canvas => canvas.GetComponent("TrackedDeviceGraphicRaycaster") == null)
                .Select(canvas => canvas.name)
                .ToArray();

            if (notClickable.Length == 0)
                return new SetupValidator.CheckResult("Scene: XR-clickable UI", SetupValidator.Severity.Pass,
                    $"All {worldCanvases.Length} world-space canvas(es) have a TrackedDeviceGraphicRaycaster.");

            return new SetupValidator.CheckResult("Scene: XR-clickable UI", SetupValidator.Severity.Warning,
                $"World-space canvas(es) without TrackedDeviceGraphicRaycaster: {string.Join(", ", notClickable)} — " +
                "the VR finger ray cannot click them (mouse still can).",
                "Add a TrackedDeviceGraphicRaycaster component to each canvas meant to be used in VR.");
        }
    }
}
