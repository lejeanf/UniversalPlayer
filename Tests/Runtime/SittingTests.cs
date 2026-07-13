using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Sitting on chairs, all modes. M&amp;K/gamepad: sit teleports to the anchor, locks
    /// locomotion, lowers the camera and folds the placeholder body; moving stands you
    /// back up; exit restores everything. VR: teleport + root lowered so the real head
    /// lands at the seat's eye height.
    /// </summary>
    public class SittingTests
    {
        private GameObject _floor;
        private GameObject _player;
        private GameObject _chair;
        private CharacterController _controller;
        private PlayerMovement _movement;
        private SitController _sit;
        private Seat _seat;
        private Transform _cameraOffset;
        private bool _prevIgnoreDefaultCollision;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _prevIgnoreDefaultCollision = Physics.GetIgnoreLayerCollision(0, 0);
            Physics.IgnoreLayerCollision(0, 0, false);
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;

            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.localScale = new Vector3(30f, 1f, 30f);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);

            _player = new GameObject("Player");
            _player.SetActive(false);
            _player.transform.position = new Vector3(0f, 1.1f, 0f);
            _controller = _player.AddComponent<CharacterController>();
            _movement = _player.AddComponent<PlayerMovement>();
            SetField(_movement, "controller", _controller);
            SetField(_movement, "speed", 4f);
            SetField(_movement, "speedChangeRate", 8f);

            _cameraOffset = new GameObject("CameraOffset").transform;
            _cameraOffset.SetParent(_player.transform);
            _cameraOffset.localPosition = new Vector3(0f, 1.65f, 0f);

            _sit = _player.AddComponent<SitController>();
            SetField(_sit, "playerMovement", _movement);
            SetField(_sit, "controller", _controller);
            SetField(_sit, "playerRoot", _player.transform);
            SetField(_sit, "cameraOffset", _cameraOffset);
            SetField(_sit, "exitOnMoveInput", false);

            _chair = new GameObject("Chair");
            _chair.transform.SetPositionAndRotation(new Vector3(3f, 0.5f, 2f), Quaternion.Euler(0f, 90f, 0f));
            _seat = _chair.AddComponent<Seat>();

            _player.SetActive(true);
            for (var i = 0; i < 10; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Physics.IgnoreLayerCollision(0, 0, _prevIgnoreDefaultCollision);
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            Object.Destroy(_player);
            Object.Destroy(_chair);
            Object.Destroy(_floor);
            yield return null;
        }

        private static void SetField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"Field '{field}' not found on {target.GetType().Name} — was it renamed? Update SittingTests alongside the refactor.");
            info.SetValue(target, value);
        }

        [UnityTest]
        public IEnumerator Sit_TeleportsToSeat_SetsCameraHeight_LocksMovement_AndExitRestores()
        {
            var preSitPosition = _player.transform.position;

            _seat.ToggleSit();
            yield return null;

            Assert.That(Vector3.Distance(_player.transform.position, _chair.transform.position), Is.LessThan(0.01f),
                "Sitting did not teleport the player to the seat anchor.");
            Assert.That(_cameraOffset.localPosition.y, Is.EqualTo(0.7f).Within(0.001f),
                "Camera did not drop to the seated eye height (eyeHeightAboveSeat).");
            Assert.That(_controller.enabled, Is.False, "CharacterController stayed enabled while seated — gravity/collisions will fight the seat.");
            Assert.That(_sit.IsSeated, Is.True);

            // Locked: movement input must not move a seated player.
            _movement.SetMoveValue(Vector2.up);
            _movement.SetIsMoving(true);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Vector3.Distance(_player.transform.position, _chair.transform.position), Is.LessThan(0.01f),
                "A seated player moved when movement input was pressed — LocomotionLocked is not respected.");
            _movement.SetIsMoving(false);
            _movement.SetMoveValue(Vector2.zero);

            _seat.ToggleSit();
            yield return null;

            Assert.That(_sit.IsSeated, Is.False);
            Assert.That(_cameraOffset.localPosition.y, Is.EqualTo(1.65f).Within(0.001f),
                "Camera height was not restored after standing up.");
            Assert.That(Vector3.Distance(_player.transform.position, preSitPosition), Is.LessThan(0.05f),
                "Player did not return to the pre-sit position (no exitAnchor was set).");
            Assert.That(_controller.enabled, Is.True, "CharacterController was not re-enabled after standing up.");
        }

        [UnityTest]
        public IEnumerator MoveInput_StandsThePlayerBackUp()
        {
            SetField(_sit, "exitOnMoveInput", true);

            _seat.ToggleSit();
            yield return new WaitForSeconds(0.5f); // past the exit grace period
            Assert.That(_sit.IsSeated, Is.True);

            _movement.SetMoveValue(Vector2.up);
            _movement.SetIsMoving(true);
            yield return new WaitForSeconds(0.2f);

            Assert.That(_sit.IsSeated, Is.False, "Pressing a move input did not stand the player up.");
            _movement.SetIsMoving(false);
            _movement.SetMoveValue(Vector2.zero);
        }

        [UnityTest]
        public IEnumerator PlaceholderBody_FoldsIntoSitPose_AndUnfoldsOnExit()
        {
            var bodyNode = new GameObject("Body");
            bodyNode.transform.SetParent(_player.transform, false);
            bodyNode.SetActive(false);
            var body = bodyNode.AddComponent<FirstPersonBody>();
            SetField(body, "playerMovement", _movement);
            SetField(body, "cameraOffset", _cameraOffset);
            SetField(_sit, "body", body);
            bodyNode.SetActive(true);
            yield return null;

            var hipL = bodyNode.transform.Find("PlaceholderBody/Hip_L");
            Assert.That(hipL, Is.Not.Null);

            _seat.ToggleSit();
            yield return new WaitForSeconds(1f);

            Assert.That(Quaternion.Angle(hipL.localRotation, Quaternion.identity), Is.GreaterThan(60f),
                "Placeholder legs did not fold forward into the sit pose.");
            var placeholderRoot = bodyNode.transform.Find("PlaceholderBody");
            Assert.That(placeholderRoot.localPosition.y, Is.LessThan(-0.4f),
                "Placeholder body did not sink so its hips meet the seat.");

            _seat.ToggleSit();
            yield return new WaitForSeconds(1f);
            Assert.That(Quaternion.Angle(hipL.localRotation, Quaternion.identity), Is.LessThan(5f),
                "Placeholder legs did not unfold after standing up.");
        }

        [UnityTest]
        public IEnumerator XrMode_LowersTheRoot_SoTheRealHeadLandsAtSeatEyeHeight()
        {
            // A tracked camera: in XR the HMD height comes from the device, so the sit
            // logic must offset the ROOT, not the camera.
            var camGo = new GameObject("MainCamera") { tag = "MainCamera" };
            camGo.AddComponent<Camera>();
            camGo.transform.SetParent(_cameraOffset, false);

            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;
            yield return null;

            var cameraHeightAboveRoot = camGo.transform.position.y - _player.transform.position.y;
            var preSitPosition = _player.transform.position;

            _seat.ToggleSit();
            yield return null;

            var expectedRootY = _chair.transform.position.y + 0.7f - cameraHeightAboveRoot;
            Assert.That(_player.transform.position.y, Is.EqualTo(expectedRootY).Within(0.02f),
                "In XR the root must be lowered so the user's real head ends up at the seat's eye height.");
            Assert.That(camGo.transform.position.y, Is.EqualTo(_chair.transform.position.y + 0.7f).Within(0.02f),
                "The head did not land at seat height + eyeHeightAboveSeat.");

            _seat.ToggleSit();
            yield return null;
            Assert.That(Vector3.Distance(_player.transform.position, preSitPosition), Is.LessThan(0.05f),
                "Exiting the seat in XR did not restore the pre-sit position.");

            Object.Destroy(camGo);
        }
    }
}
