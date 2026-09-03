using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Verifies the serialized wiring of Player.prefab so an unassigned reference
    /// is caught here instead of silently no-op'ing at runtime.
    /// </summary>
    public class PlayerPrefabWiringTests
    {
        private GameObject _player;

        [OneTimeSetUp]
        public void LoadPrefab()
        {
            _player = AssetDatabase.LoadAssetAtPath<GameObject>(PackagePaths.PlayerPrefab);
            Assert.That(_player, Is.Not.Null,
                $"Player.prefab not found at '{PackagePaths.PlayerPrefab}'. " +
                "If the prefab moved, update PackagePaths.PlayerPrefab and the editor spawn tooling (CreateVrPlayer.cs).");
        }

        [Test]
        public void FadeMask_HasVolumeAndProfileAssigned()
        {
            var fadeMask = RequireComponent<FadeMask>();
            RequireAssigned(fadeMask, "postProcessVolume",
                "FadeMask cannot fade without a Volume. Assign the Volume component from the Player prefab's fade child object.");

            // FadeMask uses volumeProfile when assigned, otherwise falls back to the
            // Volume's own sharedProfile (FadeMask.SetupVolumeProfile). The packaged
            // default ships the URP profile on the Volume; HDRP projects override it
            // on their Player prefab VARIANT.
            var so = new SerializedObject(fadeMask);
            var explicitProfile = so.FindProperty("volumeProfile").objectReferenceValue;
            var volume = (UnityEngine.Rendering.Volume)so.FindProperty("postProcessVolume").objectReferenceValue;
            var fallbackProfile = volume != null ? volume.sharedProfile : null;
            Assert.That(explicitProfile != null || fallbackProfile != null, Is.True,
                "Neither FadeMask.volumeProfile nor the Volume's sharedProfile is assigned — " +
                "the fade has no ColorAdjustments to drive and every fade silently no-ops. " +
                "Assign a FadeGlobalVolume Profile from Runtime/scripts/Fade/.");
        }

        [Test]
        public void FadeVolume_ShipsDisabled_SoEditModeNeverRendersTheFade()
        {
            var fadeMask = RequireComponent<FadeMask>();
            var volume = (UnityEngine.Rendering.Volume)new SerializedObject(fadeMask)
                .FindProperty("postProcessVolume").objectReferenceValue;
            Assert.That(volume, Is.Not.Null,
                "FadeMask has no Volume assigned — see FadeMask_HasVolumeAndProfileAssigned.");
            Assert.That(volume.enabled, Is.False,
                "The fade Volume must ship DISABLED on Player.prefab: enabled, its ColorAdjustments tint " +
                "(or black out) the world in EDIT mode. FadeMask.SetupVolumeProfile enables it when play " +
                "mode starts, and FadeProfileEditModeReset switches it back off after a play session.");
        }

        [Test]
        public void NoPeeking_CollisionLayerField_Exists()
        {
            // Layers are project-specific, so the packaged prefab legitimately ships with
            // collisionLayer = Nothing; each project sets its wall layer on its Player
            // VARIANT (enforced by Tools/Jeanf/UniversalPlayer/ValidateSetup in the consuming
            // project, and NoPeeking logs a one-shot warning at runtime when unset).
            var noPeeking = RequireComponent<NoPeeking>();
            var so = new SerializedObject(noPeeking);
            var layer = so.FindProperty("collisionLayer");
            Assert.That(layer, Is.Not.Null,
                "Field 'collisionLayer' no longer exists on NoPeeking — update this test, " +
                "the ValidateSetup check, and NoPeeking's runtime guard alongside the refactor.");
        }

        [Test]
        public void BroadcastControlsStatus_HasInputAssigned()
        {
            var broadcaster = RequireComponent<BroadcastControlsStatus>();
            RequireAssigned(broadcaster, "playerInput",
                "Without PlayerInput, control scheme changes are never detected (no VR/keyboard switching).");
        }

        [Test]
        public void HandsDisplayer_HasHandsAssigned()
        {
            var displayer = RequireComponent<HandsDisplayer>();
            RequireAssigned(displayer, "leftHand",
                "Without this reference the left hand never appears in VR.");
            RequireAssigned(displayer, "rightHand",
                "Without this reference the right hand never appears in VR.");
        }

        [Test]
        public void HandsDisplayer_IsTheSingleHandVisibilityAuthority()
        {
            // Regression: the Player used to also carry the XRI sample
            // HandsAndControllersManager, which toggles the SAME Left/Right Controller
            // objects as HandsDisplayer but with opposite (hand-tracking) logic — the
            // two fought and the hands ended up hidden. HandsDisplayer (scheme-driven)
            // is the sole authority; nothing else may toggle those objects.
            Assert.That(_player.GetComponentsInChildren<HandsDisplayer>(true).Length, Is.EqualTo(1),
                "Expected exactly one HandsDisplayer on the Player prefab.");

            foreach (var component in _player.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue; // missing script slot
                Assert.That(component.GetType().Name, Is.Not.EqualTo("HandsAndControllersManager"),
                    "The XRI-sample HandsAndControllersManager is back on the Player prefab. It fights " +
                    "HandsDisplayer over the Left/Right Controller objects (and depends on a project-side " +
                    "imported sample). Remove it — HandsDisplayer owns hand visibility.");
            }
        }

        [Test]
        public void XrModeManager_ShipsOnThePlayer()
        {
            // Matches the XR display to the control mode (stereo in VR, flat on
            // desktop) so the monitor never shows a single-eye view outside VR.
            RequireComponent<XrModeManager>();
        }

        [Test]
        public void XrModeManager_ManagesCameraXrRendering_AndNeverStopsTheDisplay()
        {
            var manager = RequireComponent<XrModeManager>();
            var so = new SerializedObject(manager);
            Assert.That(so.FindProperty("manageCameraXrRendering").boolValue, Is.True,
                "manageCameraXrRendering must ship enabled — the camera switch (plus its projection reset) " +
                "is what returns a correct flat desktop view after leaving VR, in URP and HDRP alike.");
            Assert.That(so.FindProperty("stopXrDisplayOnDesktop").intValue,
                Is.EqualTo((int)XrModeManager.DisplayStopMode.Never),
                "stopXrDisplayOnDesktop must ship Never: stopping the display buys only Editor chrome " +
                "(the eye dropdown) but costs a full Link re-handshake — seconds of black headset — on " +
                "every VR re-entry. See XrModeManager's class comment.");
            Assert.That(so.FindProperty("keepXrSessionAlive").boolValue, Is.True,
                "keepXrSessionAlive must ship enabled — without desktop frames the OpenXR runtime idles " +
                "the session and every VR entry (the first included) pays a slow session start over Link.");
        }

        [Test]
        public void XrModeHud_ShipsOnThePlayer_OverlayOffByDefault()
        {
            // Diagnostic HUD for the presence signals + resolved mode. Ships present but
            // with the overlay disabled (toggle at runtime with Ctrl+Alt+H).
            var hud = RequireComponent<XrModeHud>();
            var so = new SerializedObject(hud);
            var overlay = so.FindProperty("showOverlay");
            Assert.That(overlay, Is.Not.Null,
                "Field 'showOverlay' no longer exists on XrModeHud — update this test alongside the refactor.");
            Assert.That(overlay.boolValue, Is.False,
                "The XR mode HUD must ship with its overlay disabled — it is a debug tool, not default UI.");
        }

        [Test]
        public void PlayerEventBridge_IsTheSingleWiringPoint_AndFullyAssigned()
        {
            var bridge = RequireComponent<PlayerEventBridge>();
            RequireAssigned(bridge, "channels",
                "Without the PlayerChannelsSO, EVERY boundary event is silent (teleports in, movement/seated/XR reports out).");

            var channels = (PlayerChannelsSO)new SerializedObject(bridge).FindProperty("channels").objectReferenceValue;
            var so = new SerializedObject(channels);
            // fallRecoveryMessage and pause are legitimately optional; everything else
            // reproduces wiring the prefab had before the bridge existed.
            foreach (var slot in new[]
                     {
                         "controlSchemeChanged", "hmdState", "hmdConnection", "xrIssueMessage",
                         "playerIsMoving", "seatedState", "mouselookState", "sceneIsLoading",
                         "playerTeleport", "objectTeleport", "cameraReset",
                         "toggleMap", "toggleInventory", "mainMenuState",
                     })
            {
                var property = so.FindProperty(slot);
                Assert.That(property, Is.Not.Null,
                    $"Slot '{slot}' no longer exists on PlayerChannelsSO — update this test alongside the refactor.");
                Assert.That(property.objectReferenceValue, Is.Not.Null,
                    $"PlayerChannelsSO slot '{slot}' is empty on the packaged default asset — the matching " +
                    "boundary event goes silent for every consumer. Point it at the sample channel asset " +
                    "the prefab used before the bridge (see the PlayerEventBridge design doc).");
            }
        }

        [Test]
        public void FootstepAudio_ShipsWiredOnThePlayer()
        {
            // Regression guard: the old project-side footstep controller was NEVER part of
            // this prefab, so the 1.0 rewrite shipped without footsteps and nobody noticed
            // until playtesting. The component now lives in the package and must stay wired.
            var footsteps = RequireComponent<FootstepAudio>();
            RequireAssigned(footsteps, "movement",
                "Without PlayerMovement, FootstepAudio has no grounded/velocity state — footsteps are silent.");
            RequireAssigned(footsteps, "footstepSource",
                "Without an AudioSource there is nothing to play footsteps through.");
            RequireAssigned(footsteps, "scuffSource",
                "Without the scuff source, friction sounds steal the footstep source mid-step.");

            // Sound resources are legitimately empty in the package (sounds are project
            // audio, e.g. the AudioSystems sample containers, assigned on the Player
            // variant) — but the surface list must ship pre-seeded so projects only fill
            // in resources instead of rediscovering the tag convention.
            var so = new SerializedObject(footsteps);
            var surfaces = so.FindProperty("surfaces");
            Assert.That(surfaces, Is.Not.Null,
                "Field 'surfaces' no longer exists on FootstepAudio — update this test alongside the refactor.");
            Assert.That(surfaces.arraySize, Is.GreaterThanOrEqualTo(2),
                "The packaged FootstepAudio must ship with the Concrete + Linoleum surface profiles pre-seeded.");
        }

        [Test]
        public void ControllerHandPoseDriver_ShipsOnThePlayer_WithResolvableFistPoses()
        {
            // Regression: the prefab once referenced pose guids that existed in no
            // repository — a DANGLING reference resolves to null in every consumer,
            // so the fingers never closed anywhere (ValidateSetup: 'no fist pose
            // assigned') while the prefab YAML looked wired. The packaged defaults
            // now point at the bundled Runtime/HandPoses assets; projects still
            // override them with their own art on the Player variant.
            // (pointPose is deliberately empty = the hand's own default idle pose.)
            var driver = RequireComponent<ControllerHandPoseDriver>();
            RequireAssigned(driver, "semiClosedFistPose",
                "Grip-touch never curls the fingers. Assign a bundled Runtime/HandPoses asset.");
            RequireAssigned(driver, "closedFistPose",
                "Trigger never closes the fist. Assign a bundled Runtime/HandPoses asset.");
            RequireAssigned(driver, "fullClosedFistPose",
                "Hard grip never closes the fist. Assign a bundled Runtime/HandPoses asset.");
        }

        [Test]
        public void FingerPointingRay_ShipsOnThePlayer()
        {
            RequireComponent<FingerPointingRay>();
        }

        [Test]
        public void StickTeleport_ShipsOnThePlayer()
        {
            RequireComponent<StickTeleport>();
        }

        [Test]
        public void UiToggleInput_HasPlayerInputAssigned()
        {
            var toggles = RequireComponent<UiToggleInput>();
            RequireAssigned(toggles, "playerInput",
                "Without PlayerInput the Map/Inventory bindings (M / I, gamepad dpad left/right) never raise their channels.");
        }

        [Test]
        public void TeleportRays_CastFromAnEditableOriginTransform()
        {
            // Each teleport ray (projectile curve) must cast from its own Ray Origin
            // Transform — the "Left/Right Teleport Ray Origin" child of the controller —
            // so designers place/aim the ray by editing that transform, and StickTeleport
            // never has to rotate the interactor itself (which compounded the aim offset
            // every frame). Same transform for the line visual so the arc starts where it casts.
            var rays = 0;
            foreach (var ray in _player.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true))
            {
                if (ray.lineType != UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor.LineType.ProjectileCurve) continue;
                rays++;
                Assert.That(ray.rayOriginTransform, Is.Not.Null,
                    $"Teleport ray '{ray.name}' has no Ray Origin Transform — assign its controller's 'Teleport Ray Origin' child.");
                Assert.That(ray.rayOriginTransform, Is.Not.EqualTo(ray.transform),
                    $"Teleport ray '{ray.name}' casts from its own transform — the aim must live on a separate, editable origin transform.");
                Assert.That(ray.rayOriginTransform.name, Does.Contain("Teleport Ray Origin"),
                    $"Teleport ray '{ray.name}' origin is '{ray.rayOriginTransform.name}'; expected the controller's 'Teleport Ray Origin' child.");
                var line = ray.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
                if (line != null)
                    Assert.That(line.lineOriginTransform, Is.EqualTo(ray.rayOriginTransform),
                        $"Teleport ray '{ray.name}': the line visual starts somewhere else than the cast origin.");
            }
            Assert.That(rays, Is.EqualTo(2), "Expected one teleport ray per controller on the Player prefab.");
        }

        [Test]
        public void HandsPhysics_DivergenceGhostIsWiredOnBothHands()
        {
            // Each physics hand (Left/RightHandPhysics) follows a tracked target and shows
            // the non-physical "ghost" hand when the world holds the physics hand back
            // (HandsPhysics.showNonPhysicalHandDistance). The ghost must ship hidden and
            // collider-free — it is an indicator, not a second physical hand.
            var sides = new System.Collections.Generic.HashSet<HandType>();
            foreach (var physics in _player.GetComponentsInChildren<HandsPhysics>(true))
            {
                var so = new SerializedObject(physics);
                var target = so.FindProperty("target").objectReferenceValue as Transform;
                var ghost = so.FindProperty("nonPhysicalHand").objectReferenceValue as GameObject;
                var distance = so.FindProperty("showNonPhysicalHandDistance").floatValue;
                var side = (HandType)so.FindProperty("handType").enumValueIndex;
                sides.Add(side);
                Assert.That(target, Is.Not.Null, $"'{physics.name}': no tracked target — the physics hand has nothing to follow.");
                Assert.That(ghost, Is.Not.Null, $"'{physics.name}': no non-physical hand — divergence is never shown.");
                Assert.That(ghost.activeSelf, Is.False, $"'{physics.name}': the ghost hand '{ghost.name}' must ship hidden (HandsPhysics shows it on divergence).");
                Assert.That(ghost.transform.IsChildOf(target), Is.True, $"'{physics.name}': the ghost hand must sit under the tracked target so it follows the real hand.");
                Assert.That(ghost.GetComponentsInChildren<Collider>(true), Is.Empty, $"'{physics.name}': the ghost hand '{ghost.name}' carries colliders — it would collide/push instead of merely indicating.");
                Assert.That(ghost.GetComponentsInChildren<Rigidbody>(true), Is.Empty, $"'{physics.name}': the ghost hand '{ghost.name}' carries a Rigidbody.");
                Assert.That(ghost.GetComponentInChildren<BlendableHand>(true), Is.Not.Null, $"'{physics.name}': the ghost hand '{ghost.name}' has no BlendableHand — HandsAppearanceManager never applies the chosen appearance to it.");
                Assert.That(so.FindProperty("ghostMaterialUrp").objectReferenceValue, Is.Not.Null, $"'{physics.name}': no URP/Built-in ghost material — the divergence hand would keep the opaque skin look.");
                Assert.That(so.FindProperty("ghostMaterialHdrp").objectReferenceValue, Is.Not.Null, $"'{physics.name}': no HDRP ghost material — the divergence hand renders magenta under HDRP.");
                Assert.That(distance, Is.InRange(0.02f, 0.3f), $"'{physics.name}': showNonPhysicalHandDistance = {distance} m is outside a sensible divergence threshold.");
                var body = physics.GetComponent<Rigidbody>();
                Assert.That(body, Is.Not.Null, $"'{physics.name}': no Rigidbody — HandsPhysics drives the hand by velocity.");
                Assert.That(body.isKinematic, Is.False, $"'{physics.name}': a kinematic Rigidbody ignores the velocity HandsPhysics sets.");
                Assert.That(body.useGravity, Is.False, $"'{physics.name}': gravity fights the velocity follower and sags the hand between physics steps.");
            }
            Assert.That(sides, Is.EquivalentTo(new[] { HandType.Left, HandType.Right }), "Expected one HandsPhysics per side on the Player prefab.");
        }

        [Test]
        public void GrabPreview_ShipsAGhostLookForBothPipelines()
        {
            var preview = RequireComponent<GrabPreview>();
            RequireAssigned(preview, "ghostMaterial", "Grab-preview ghost hands keep the opaque skin look under URP.");
            RequireAssigned(preview, "ghostMaterialHdrp", "Grab-preview ghost hands render magenta under HDRP.");
        }

        [Test]
        public void PipelineMaterials_PicksByActivePipeline_AndFallsBack()
        {
            var urp = new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "urp" };
            var hdrp = new Material(Shader.Find("Hidden/InternalErrorShader")) { name = "hdrp" };
            try
            {
                Assert.That(PipelineMaterials.Pick(urp, hdrp, "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset"), Is.SameAs(hdrp));
                Assert.That(PipelineMaterials.Pick(urp, hdrp, "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"), Is.SameAs(urp));
                Assert.That(PipelineMaterials.Pick(urp, hdrp, null), Is.SameAs(urp), "Built-in uses the URP/Built-in material.");
                Assert.That(PipelineMaterials.Pick(null, hdrp, "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"), Is.SameAs(hdrp), "A missing pipeline material falls back to the other one rather than to nothing.");
                Assert.That(PipelineMaterials.Pick(null, null, null), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(urp);
                Object.DestroyImmediate(hdrp);
            }
        }

        [Test]
        public void FarRays_CastAndHitUi_OnBothControllers()
        {
            // The controllers' far ray IS the Near-Far interactor: with far casting off it
            // never raycasts — no distant grab, no UI hover/click, and the curve visual stays
            // hidden ("the ray is gone"). uvs used to re-enable it on its variant; the
            // package must ship it on so no project has to know.
            var found = 0;
            foreach (var interactor in _player.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>(true))
            {
                found++;
                Assert.That(interactor.enableFarCasting, Is.True, $"'{interactor.name}': far casting is off — the ray cannot reach interactables or UI.");
                Assert.That(interactor.enableUIInteraction, Is.True, $"'{interactor.name}': UI interaction is off — the ray cannot hover/click world-space UI.");
                Assert.That(interactor.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.CurveVisualController>(true), Is.Not.Null,
                    $"'{interactor.name}': no CurveVisualController — the ray has no line to show (InteractionRayHoverVisual gates it on hover).");
            }
            Assert.That(found, Is.EqualTo(2), "Expected one Near-Far interactor per controller.");
        }

        [Test]
        public void CursorStateController_HasThePointerPalette()
        {
            var cursor = RequireComponent<CursorStateController>();
            RequireAssigned(cursor, "palette",
                "Without a CursorPaletteSO the cursor AND the interaction ray fall back to code defaults — the shared look is gone.");
        }

        [Test]
        public void InteractionRayHoverVisual_PaintsTheRayFromThePalette()
        {
            var go = new GameObject("curve visual under test");
            try
            {
                go.AddComponent<LineRenderer>();
                var visual = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.CurveVisualController>();
                var palette = ScriptableObject.CreateInstance<CursorPaletteSO>();
                palette.resting = Color.red;
                palette.hover = Color.green;
                palette.click = Color.blue;
                try
                {
                    InteractionRayHoverVisual.ApplyPalette(visual, palette);
                    Assert.That(visual.customizeLinePropertiesForState, Is.True, "Per-state line properties must be on for hover/click colours to apply.");
                    Assert.That(visual.noValidHitProperties.gradient.Evaluate(0.5f), Is.EqualTo(Color.red).Using(ColorComparer), "Pointing at nothing = resting colour.");
                    Assert.That(visual.hoverHitProperties.gradient.Evaluate(0.5f), Is.EqualTo(Color.green).Using(ColorComparer), "Hovering an interactable = hover colour.");
                    Assert.That(visual.uiHitProperties.gradient.Evaluate(0.5f), Is.EqualTo(Color.green).Using(ColorComparer), "Hovering UI = hover colour.");
                    Assert.That(visual.selectHitProperties.gradient.Evaluate(0.5f), Is.EqualTo(Color.blue).Using(ColorComparer), "Selecting = click colour.");
                    Assert.That(visual.uiPressHitProperties.gradient.Evaluate(0.5f), Is.EqualTo(Color.blue).Using(ColorComparer), "Pressing UI = click colour.");
                    Assert.That(visual.hoverHitProperties.adjustGradient, Is.True);
                }
                finally { Object.DestroyImmediate(palette); }
            }
            finally { Object.DestroyImmediate(go); }
        }

        private static readonly System.Collections.Generic.IEqualityComparer<Color> ColorComparer = new ApproxColor();
        private sealed class ApproxColor : System.Collections.Generic.IEqualityComparer<Color>
        {
            public bool Equals(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
            public int GetHashCode(Color c) => 0;
        }

        private T RequireComponent<T>() where T : Component
        {
            var component = _player.GetComponentInChildren<T>(true);
            Assert.That(component, Is.Not.Null,
                $"No {typeof(T).Name} found anywhere on Player.prefab. " +
                $"It was either removed or its script reference broke (see PackageIntegrityTests).");
            return component;
        }

        private static void RequireAssigned(Component component, string fieldName, string consequence)
        {
            var so = new SerializedObject(component);
            var property = so.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null,
                $"Field '{fieldName}' no longer exists on {component.GetType().Name} — " +
                "it was renamed or removed; update this test alongside the refactor.");
            Assert.That(property.objectReferenceValue, Is.Not.Null,
                $"{component.GetType().Name}.{fieldName} is not assigned on Player.prefab " +
                $"(object '{component.gameObject.name}'). {consequence}");
        }
    }
}
