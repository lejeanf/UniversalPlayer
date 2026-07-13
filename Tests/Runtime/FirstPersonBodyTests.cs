using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// True first-person body: the placeholder mannequin must exist and animate in the
    /// M&amp;K / gamepad modes, stay hidden in XR/FreeCam, and follow the camera yaw.
    /// When a project animator is assigned instead, a missing parameter contract must
    /// complain loudly instead of failing silently.
    /// </summary>
    public class FirstPersonBodyTests
    {
        private GameObject _floor;
        private GameObject _player;
        private PlayerMovement _movement;
        private Transform _cameraOffset;
        private FirstPersonBody _body;
        private bool _prevIgnoreDefaultCollision;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _prevIgnoreDefaultCollision = Physics.GetIgnoreLayerCollision(0, 0);
            Physics.IgnoreLayerCollision(0, 0, false);
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;

            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.transform.localScale = new Vector3(20f, 1f, 20f);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);

            _player = new GameObject("Player");
            _player.SetActive(false);
            _player.transform.position = new Vector3(0f, 1.1f, 0f);
            var controller = _player.AddComponent<CharacterController>();
            _movement = _player.AddComponent<PlayerMovement>();
            SetField(_movement, "controller", controller);
            SetField(_movement, "speed", 4f);
            SetField(_movement, "speedChangeRate", 8f);

            _cameraOffset = new GameObject("CameraOffset").transform;
            _cameraOffset.SetParent(_player.transform);
            _cameraOffset.localPosition = new Vector3(0f, 1.65f, 0f);

            var bodyNode = new GameObject("Body");
            bodyNode.transform.SetParent(_player.transform, false);
            _body = bodyNode.AddComponent<FirstPersonBody>();
            SetField(_body, "playerMovement", _movement);
            SetField(_body, "cameraOffset", _cameraOffset);

            _player.SetActive(true);
            for (var i = 0; i < 10; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Physics.IgnoreLayerCollision(0, 0, _prevIgnoreDefaultCollision);
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            Object.Destroy(_player);
            Object.Destroy(_floor);
            yield return null;
        }

        private static void SetField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"Field '{field}' not found on {target.GetType().Name} — was it renamed? Update FirstPersonBodyTests alongside the refactor.");
            info.SetValue(target, value);
        }

        private Transform PlaceholderRoot() => _body.transform.Find("PlaceholderBody");

        [UnityTest]
        public IEnumerator Placeholder_IsBuiltAndVisible_InKeyboardMouseMode()
        {
            var root = PlaceholderRoot();
            Assert.That(root, Is.Not.Null, "Placeholder mannequin was never built (no PlaceholderBody child).");
            Assert.That(root.gameObject.activeInHierarchy, Is.True, "Placeholder body is hidden in M&K mode — it should be visible.");
            Assert.That(root.GetComponentsInChildren<Renderer>(true).Length, Is.GreaterThanOrEqualTo(5),
                "Mannequin is missing parts (expected torso + 2 legs + 2 arms).");
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty,
                "Placeholder body has colliders — they will fight the CharacterController and the crouch headroom SphereCast.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator XrScheme_HidesTheBody_AndComingBackShowsIt()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;
            yield return null;
            yield return null;
            Assert.That(PlaceholderRoot().gameObject.activeInHierarchy, Is.False,
                "Body still visible in XR — a mismatched fake body in VR is worse than none.");

            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            yield return null;
            yield return null;
            Assert.That(PlaceholderRoot().gameObject.activeInHierarchy, Is.True,
                "Body did not come back when switching from XR to keyboard — the scheme switch must be seamless.");
        }

        [UnityTest]
        public IEnumerator Walking_SwingsTheLegs_AndIdleReturnsToNeutral()
        {
            var hipL = _body.transform.Find("PlaceholderBody/Hip_L");
            Assert.That(hipL, Is.Not.Null);

            _movement.SetMoveValue(Vector2.up);
            _movement.SetIsMoving(true);
            var maxSwing = 0f;
            for (var i = 0; i < 90; i++)
            {
                yield return null;
                maxSwing = Mathf.Max(maxSwing, Quaternion.Angle(hipL.localRotation, Quaternion.identity));
            }
            Assert.That(maxSwing, Is.GreaterThan(2f), "Legs never swung while walking.");

            _movement.SetMoveValue(Vector2.zero);
            _movement.SetIsMoving(false);
            yield return new WaitForSeconds(1f);
            Assert.That(Quaternion.Angle(hipL.localRotation, Quaternion.identity), Is.LessThan(1f),
                "Legs did not settle back to neutral after stopping.");
        }

        [UnityTest]
        public IEnumerator BodyYaw_FollowsCameraWhileMoving_ButIgnoresSmallIdleTwists()
        {
            // Idle small twist: inside the dead zone, the body must not fidget.
            _cameraOffset.localRotation = Quaternion.Euler(0f, 20f, 0f);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Mathf.DeltaAngle(_body.transform.localEulerAngles.y, 0f), Is.EqualTo(0f).Within(0.5f),
                "Body twitched on a small idle head turn (inside the yaw dead zone).");

            // Walking: the body must align with the camera.
            _cameraOffset.localRotation = Quaternion.Euler(0f, 90f, 0f);
            _movement.SetMoveValue(Vector2.up);
            _movement.SetIsMoving(true);
            yield return new WaitForSeconds(1f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(_body.transform.localEulerAngles.y, 90f)), Is.LessThan(5f),
                "Body never aligned with the camera direction while walking.");
            _movement.SetIsMoving(false);
            _movement.SetMoveValue(Vector2.zero);
        }

        [UnityTest]
        public IEnumerator AssignedAnimatorWithoutController_WarnsLoudly_InsteadOfFailingSilently()
        {
            var rig = new GameObject("ProjectBodyRig");
            rig.transform.SetParent(_player.transform, false);
            var animator = rig.AddComponent<Animator>(); // deliberately no controller

            var bodyNode = new GameObject("Body2");
            bodyNode.transform.SetParent(_player.transform, false);
            bodyNode.SetActive(false);
            var body = bodyNode.AddComponent<FirstPersonBody>();
            SetField(body, "playerMovement", _movement);
            SetField(body, "cameraOffset", _cameraOffset);
            SetField(body, "bodyAnimator", animator);
            bodyNode.SetActive(true);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("NO AnimatorController"));
            yield return null;
            yield return null;

            Object.Destroy(bodyNode);
            Object.Destroy(rig);
        }
    }
}
