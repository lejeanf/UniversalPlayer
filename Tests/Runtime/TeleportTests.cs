using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using jeanf.EventSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Behavior tests for the event-driven teleport chain:
    /// SendTeleportTarget.Teleport() → TeleportEventChannelSO → TeleportOnEvent moves
    /// the player (with optional FadeMask fade and camera reset event).
    /// </summary>
    public class TeleportTests
    {
        private GameObject _player;
        private GameObject _listenerGo;
        private GameObject _destinationGo;
        private TeleportOnEvent _teleportOnEvent;
        private SendTeleportTarget _sender;
        private TeleportEventChannelSO _teleportChannel;
        private int _cameraResetCount;

        private static readonly Vector3 Destination = new Vector3(5f, 0f, 3f);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _teleportChannel = ScriptableObject.CreateInstance<TeleportEventChannelSO>();
            _cameraResetCount = 0;
            PlayerEvents.CameraResetRequested += CountCameraReset;

            _player = new GameObject("TestPlayer");
            _player.transform.position = Vector3.zero;

            // listener: built inactive so all fields are wired before OnEnable subscribes
            _listenerGo = new GameObject("TeleportOnEvent");
            _listenerGo.SetActive(false);
            _teleportOnEvent = _listenerGo.AddComponent<TeleportOnEvent>();
            SetField(_teleportOnEvent, "player", _player);
            SetField(_teleportOnEvent, "fadeInDuration", 0.05f);
            SetField(_teleportOnEvent, "fadeOutDuration", 0.05f);
            SetField(_teleportOnEvent, "listOfFilters", new List<FilterSO>());
            SetFieldOn(typeof(TeleportEventListener), _teleportOnEvent, "_channel", _teleportChannel);
            if (_teleportOnEvent.OnEventRaised == null)
                _teleportOnEvent.OnEventRaised = new UnityEvent<TeleportInformation>();
            _teleportOnEvent.OnEventRaised.AddListener(_teleportOnEvent.Teleport);
            _listenerGo.SetActive(true);

            _destinationGo = new GameObject("TeleportDestination");
            _destinationGo.transform.SetPositionAndRotation(Destination, Quaternion.Euler(0f, 90f, 0f));
            _sender = _destinationGo.AddComponent<SendTeleportTarget>();
            _sender.isTeleportPlayer = true;
            _sender.isUsingFilter = false;
            SetField(_sender, "_teleportChannel", _teleportChannel);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerEvents.CameraResetRequested -= CountCameraReset;
            Object.Destroy(_player);
            Object.Destroy(_listenerGo);
            Object.Destroy(_destinationGo);
            Object.Destroy(_teleportChannel);
            yield return null;
        }

        private void CountCameraReset() => _cameraResetCount++;

        [UnityTest]
        public IEnumerator Teleport_NoFade_MovesPlayerToDestination_AndResetsCamera()
        {
            _sender.Teleport(shouldFade: false);
            yield return null;

            Assert.That(Vector3.Distance(_player.transform.position, Destination), Is.LessThan(0.01f),
                "SendTeleportTarget.Teleport(false) did not move the player. The chain " +
                "SendTeleportTarget → TeleportEventChannelSO → TeleportEventListener.OnEventRaised → TeleportOnEvent.Teleport " +
                "is broken — check the channel asset and the OnEventRaised wiring on the Player prefab.");
            Assert.That(Quaternion.Angle(_player.transform.rotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(1f),
                "Player moved but did not take the destination's rotation.");
            Assert.That(_cameraResetCount, Is.EqualTo(1),
                "PlayerEvents.CameraResetRequested was not raised after a player teleport — the camera would keep its stale orientation.");
        }

        [UnityTest]
        public IEnumerator Teleport_WithFade_GoesLoadingThenClear_AndMoves()
        {
            using var rig = new FadeTestRig();
            FadeMask.SetStateClear();
            yield return new WaitForSeconds(FadeTestRig.SettleSeconds);

            _sender.Teleport(); // default overload → shouldFade = true
            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("Loading"),
                "Teleport with fade must fade to black (Loading) before moving the player.");

            // fadeInDuration (0.05) + 0.1 post-move delay + tween settle
            yield return new WaitForSeconds(0.05f + 0.1f + FadeTestRig.SettleSeconds);

            Assert.That(Vector3.Distance(_player.transform.position, Destination), Is.LessThan(0.01f),
                "Player did not arrive at the destination after the fade window.");
            Assert.That(FadeTestRig.CurrentFadeMaskState(), Is.EqualTo("Clear"),
                "The screen did not clear after a fading teleport — players would be stuck on black.");
        }

        [UnityTest]
        public IEnumerator Teleport_WithNonMatchingFilter_DoesNotMove()
        {
            var accepted = ScriptableObject.CreateInstance<FilterSO>();
            var other = ScriptableObject.CreateInstance<FilterSO>();
            SetField(_teleportOnEvent, "listOfFilters", new List<FilterSO> { accepted });
            _sender.isUsingFilter = true;
            _sender._filter = other;

            _sender.Teleport(shouldFade: false);
            yield return null;

            Assert.That(_player.transform.position, Is.EqualTo(Vector3.zero),
                "TeleportOnEvent executed a teleport whose filter is NOT in its listOfFilters — " +
                "filtering is broken, every listener would react to every teleport event.");

            Object.Destroy(accepted);
            Object.Destroy(other);
        }

        [UnityTest]
        public IEnumerator Teleport_OfNonPlayerObject_MovesThatObjectOnly()
        {
            var crate = new GameObject("TestCrate");
            crate.transform.position = Vector3.zero;
            _sender.isTeleportPlayer = false;
            _sender.ObjectToTeleport = crate.transform;

            _sender.Teleport(shouldFade: false);
            yield return null;

            Assert.That(Vector3.Distance(crate.transform.position, Destination), Is.LessThan(0.01f),
                "A non-player object teleport did not move the target object.");
            Assert.That(_player.transform.position, Is.EqualTo(Vector3.zero),
                "A non-player teleport moved the player — objectIsPlayer routing is broken.");
            Assert.That(_cameraResetCount, Is.EqualTo(0),
                "CameraResetRequested must only be raised for player teleports.");

            Object.Destroy(crate);
        }

        private static void SetField(object target, string fieldName, object value) =>
            SetFieldOn(target.GetType(), target, fieldName, value);

        private static void SetFieldOn(System.Type declaringType, object target, string fieldName, object value)
        {
            FieldInfo field = null;
            for (var type = declaringType; type != null && field == null; type = type.BaseType)
                field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                $"Field '{fieldName}' not found on {declaringType.Name} — it was renamed; update TeleportTests alongside the refactor.");
            field.SetValue(target, value);
        }
    }
}
