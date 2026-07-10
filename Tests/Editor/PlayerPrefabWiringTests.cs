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
            RequireAssigned(fadeMask, "volumeProfile",
                "Assign the URP or HDRP FadeGlobalVolume Profile from Runtime/scripts/Fade/.");
        }

        [Test]
        public void NoPeeking_HasCollisionLayerConfigured()
        {
            var noPeeking = RequireComponent<NoPeeking>();
            var so = new SerializedObject(noPeeking);
            var layer = so.FindProperty("collisionLayer");
            Assert.That(layer, Is.Not.Null,
                "Field 'collisionLayer' no longer exists on NoPeeking — update this test alongside the refactor.");
            Assert.That(layer.intValue, Is.Not.EqualTo(0),
                "NoPeeking.collisionLayer is set to Nothing: the head-in-wall fade will never trigger. " +
                "Set it to the layer(s) your walls use (on the NoPeeking component of Player.prefab).");
        }

        [Test]
        public void BroadcastControlsStatus_HasInputAndChannelsAssigned()
        {
            var broadcaster = RequireComponent<BroadcastControlsStatus>();
            RequireAssigned(broadcaster, "playerInput",
                "Without PlayerInput, control scheme changes are never detected (no VR/keyboard switching).");
            RequireAssigned(broadcaster, "hmdStateChannel",
                "Listeners (hands, cursor, ...) rely on this channel to react to HMD state.");
            RequireAssigned(broadcaster, "activeControlScheme",
                "HandsDisplayer listens on this channel to show/hide hands on scheme change.");
        }

        [Test]
        public void HandsDisplayer_HasHandsAndChannelAssigned()
        {
            var displayer = RequireComponent<HandsDisplayer>();
            RequireAssigned(displayer, "leftHand",
                "Without this reference the left hand never appears in VR.");
            RequireAssigned(displayer, "rightHand",
                "Without this reference the right hand never appears in VR.");
            RequireAssigned(displayer, "changedControlSchemeChannel",
                "Without this channel, hands don't react when the control scheme changes to/from XR.");
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
