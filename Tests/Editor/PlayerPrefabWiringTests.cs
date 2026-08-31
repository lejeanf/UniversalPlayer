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
