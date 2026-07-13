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
        public void NoPeeking_CollisionLayerField_Exists()
        {
            // Layers are project-specific, so the packaged prefab legitimately ships with
            // collisionLayer = Nothing; each project sets its wall layer on its Player
            // VARIANT (enforced by Tools/UniversalPlayer/ValidateSetup in the consuming
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
        public void ControllerHandPoseDriver_ShipsOnThePlayer()
        {
            // Pose slots are legitimately empty in the package (poses are project art,
            // authored in the Pose Editor and assigned on the Player variant); only the
            // component's presence is packaged wiring.
            RequireComponent<ControllerHandPoseDriver>();
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
