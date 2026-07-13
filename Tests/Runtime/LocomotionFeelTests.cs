using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Locomotion feel for the M&amp;K / gamepad modes: weight &amp; momentum (velocity ramps
    /// instead of snapping), sprint, crouch (capsule shrink + blocked stand-up), and the
    /// FpsCameraFeel head bob / camera roll — including the hard rule that none of it
    /// runs in XR (artificial camera motion in VR causes motion sickness).
    /// </summary>
    public class LocomotionFeelTests
    {
        private GameObject _floor;
        private GameObject _player;
        private CharacterController _controller;
        private PlayerMovement _movement;
        private bool _prevIgnoreDefaultCollision;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // The rigs live on the Default layer; projects (like this one) may disable
            // Default<->Default in the collision matrix. Pin it on for the test.
            _prevIgnoreDefaultCollision = Physics.GetIgnoreLayerCollision(0, 0);
            Physics.IgnoreLayerCollision(0, 0, false);
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;

            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Floor";
            _floor.transform.localScale = new Vector3(20f, 1f, 20f);
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);

            _player = new GameObject("Player");
            _player.SetActive(false);
            _player.transform.position = new Vector3(0f, 1.1f, 0f);
            _controller = _player.AddComponent<CharacterController>();
            _movement = _player.AddComponent<PlayerMovement>();
            SetField(_movement, "controller", _controller);
            SetField(_movement, "speed", 4f);
            // Gentle ramp so even a slow editor frame cannot reach top speed in one step
            // (keeps the momentum assertions deterministic).
            SetField(_movement, "speedChangeRate", 4f);
            _player.SetActive(true);

            // Let gravity settle the controller onto the floor.
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
            Assert.That(info, Is.Not.Null, $"Field '{field}' not found on {target.GetType().Name} — was it renamed? Update LocomotionFeelTests alongside the refactor.");
            info.SetValue(target, value);
        }

        private void StartMoving(Vector2 input)
        {
            _movement.SetMoveValue(input);
            _movement.SetIsMoving(true);
        }

        private void StopMoving()
        {
            _movement.SetMoveValue(Vector2.zero);
            _movement.SetIsMoving(false);
        }

        private IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator Momentum_VelocityRampsUp_AndDecaysAfterRelease()
        {
            StartMoving(Vector2.up);
            yield return null;
            yield return null;
            var earlySpeed = _movement.PlanarVelocity.magnitude;
            Assert.That(earlySpeed, Is.GreaterThan(0f), "No movement at all — momentum move is not running.");
            Assert.That(earlySpeed, Is.LessThan(3.9f),
                "Velocity snapped (almost) straight to top speed — the acceleration ramp (speedChangeRate) is not being applied.");

            yield return Frames(120);
            Assert.That(_movement.PlanarVelocity.magnitude, Is.EqualTo(4f).Within(0.2f),
                "Walk speed never reached its target.");

            StopMoving();
            yield return null;
            Assert.That(_movement.PlanarVelocity.magnitude, Is.GreaterThan(0.5f),
                "Velocity vanished instantly on release — deceleration (weight) is not being applied.");

            yield return Frames(120);
            Assert.That(_movement.PlanarVelocity.magnitude, Is.LessThan(0.05f),
                "Velocity never decayed back to zero after input stopped.");
        }

        [UnityTest]
        public IEnumerator Sprint_TopSpeedIsHigherThanWalk()
        {
            StartMoving(Vector2.up);
            _movement.SetSprintHeld(true);
            yield return Frames(90);
            Assert.That(_movement.PlanarVelocity.magnitude, Is.GreaterThan(5f),
                "Sprint did not raise the top speed above walking speed.");
            Assert.That(_movement.IsSprinting, Is.True);
            _movement.SetSprintHeld(false);
        }

        [UnityTest]
        public IEnumerator Crouch_ShrinksCapsule_AndRefusesToStandUnderCeiling()
        {
            var standingHeight = _controller.height;

            _movement.SetCrouchHeld(true);
            yield return Frames(30);
            Assert.That(_controller.height, Is.LessThan(standingHeight * 0.7f),
                "Crouching did not shrink the CharacterController capsule.");
            Assert.That(_movement.IsCrouched, Is.True);

            // Ceiling above the crouched player: standing must be refused until it is gone.
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.localScale = new Vector3(4f, 0.5f, 4f);
            ceiling.transform.position = new Vector3(0f, 1.75f, 0f);
            yield return Frames(2); // let physics register the new collider

            _movement.SetCrouchHeld(false);
            yield return Frames(30);
            Assert.That(_movement.IsCrouched, Is.True,
                "Player stood up INTO a ceiling — the headroom check (CanStandUp SphereCast) is broken.");

            Object.Destroy(ceiling);
            yield return Frames(2);
            yield return Frames(30);
            Assert.That(_controller.height, Is.EqualTo(standingHeight).Within(0.05f),
                "Player never stood back up after the ceiling was removed.");
        }

        [UnityTest]
        public IEnumerator XrScheme_LocomotionFeelDoesNotMoveThePlayer()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;
            var before = _player.transform.position;

            StartMoving(Vector2.up);
            _movement.SetSprintHeld(true);
            yield return Frames(30);

            var moved = _player.transform.position - before;
            moved.y = 0f;
            Assert.That(moved.magnitude, Is.LessThan(0.01f),
                "PlayerMovement moved the player horizontally while in XR — locomotion feel must be inert there (XRI owns XR locomotion).");
        }

        [UnityTest]
        public IEnumerator HeadBob_OscillatesWhileMoving_AndSettlesWhenStopped()
        {
            var feel = BuildCameraFeel();
            yield return null;

            var maxIdle = 0f;
            for (var i = 0; i < 20; i++)
            {
                yield return null;
                maxIdle = Mathf.Max(maxIdle, Mathf.Abs(feel.CurrentOffset.y));
            }
            Assert.That(maxIdle, Is.LessThan(0.001f), "Head bob while standing still.");

            StartMoving(Vector2.up);
            var maxMoving = 0f;
            for (var i = 0; i < 90; i++)
            {
                yield return null;
                maxMoving = Mathf.Max(maxMoving, Mathf.Abs(feel.CurrentOffset.y));
            }
            Assert.That(maxMoving, Is.GreaterThan(0.005f), "No head bob while walking.");

            StopMoving();
            yield return Frames(60);
            Assert.That(feel.CurrentOffset.magnitude, Is.LessThan(0.002f),
                "Head bob offset never settled back after movement stopped.");
        }

        [UnityTest]
        public IEnumerator CameraRoll_FollowsStrafe_AndReturnsToZero()
        {
            var feel = BuildCameraFeel();
            yield return null;

            StartMoving(Vector2.right);
            yield return Frames(45);
            Assert.That(feel.CurrentRoll, Is.LessThan(-0.4f),
                "Strafing right did not roll the camera (expected a negative z tilt, leaning into the movement).");

            StopMoving();
            yield return Frames(90);
            Assert.That(Mathf.Abs(feel.CurrentRoll), Is.LessThan(0.05f),
                "Camera roll never returned to level after the strafe ended.");
        }

        [UnityTest]
        public IEnumerator XrScheme_CameraFeelStaysInert()
        {
            var feel = BuildCameraFeel();
            yield return null;

            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;
            StartMoving(Vector2.right);
            _movement.SetSprintHeld(true);
            yield return Frames(45);

            Assert.That(feel.CurrentOffset.magnitude, Is.LessThan(0.001f),
                "FpsCameraFeel applied a position offset in XR — artificial camera motion in VR causes motion sickness.");
            Assert.That(Mathf.Abs(feel.CurrentRoll), Is.LessThan(0.05f),
                "FpsCameraFeel applied camera roll in XR.");
        }

        [UnityTest]
        public IEnumerator LookSmoothing_EasesTowardTarget_AndSnapsWhenDisabled()
        {
            // Unit-level: the component is never activated (no Awake), we only exercise
            // the private smoothing step against a bare transform.
            var rig = new GameObject("LookSmoothingRig");
            rig.SetActive(false);
            var offset = new GameObject("CameraOffset").transform;
            offset.SetParent(rig.transform);
            var look = rig.AddComponent<FPSCameraMovement>();
            SetField(look, "cameraOffset", offset);
            SetField(look, "_rotation", new Vector2(0f, 40f));

            var apply = typeof(FPSCameraMovement).GetMethod("ApplySmoothedRotation", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(apply, Is.Not.Null, "FPSCameraMovement.ApplySmoothedRotation was renamed — update LocomotionFeelTests.");

            SetField(look, "lookSmoothingSeconds", 0.1f);
            apply.Invoke(look, new object[] { 0.016f });
            var afterOneStep = offset.localEulerAngles.y;
            Assert.That(afterOneStep, Is.GreaterThan(0.5f).And.LessThan(30f),
                "With smoothing on, one step should move the camera partway toward the target — not leave it still, not snap it.");

            for (var i = 0; i < 300; i++) apply.Invoke(look, new object[] { 0.016f });
            Assert.That(Mathf.DeltaAngle(offset.localEulerAngles.y, 40f), Is.EqualTo(0f).Within(0.5f),
                "Smoothed look rotation never converged on the target.");

            SetField(look, "lookSmoothingSeconds", 0f);
            SetField(look, "_rotation", new Vector2(0f, 80f));
            apply.Invoke(look, new object[] { 0.016f });
            Assert.That(Mathf.DeltaAngle(offset.localEulerAngles.y, 80f), Is.EqualTo(0f).Within(0.01f),
                "With smoothing at 0 the rotation must snap straight to the target (raw input).");

            Object.DestroyImmediate(rig);
            yield return null;
        }

        private FpsCameraFeel BuildCameraFeel()
        {
            var cameraOffset = new GameObject("CameraOffset").transform;
            cameraOffset.SetParent(_player.transform);
            cameraOffset.localPosition = new Vector3(0f, 1.65f, 0f);

            var feelNode = new GameObject("CameraFeel");
            feelNode.SetActive(false);
            feelNode.transform.SetParent(cameraOffset);
            feelNode.transform.localPosition = Vector3.zero;
            var feel = feelNode.AddComponent<FpsCameraFeel>();
            SetField(feel, "playerMovement", _movement);
            SetField(feel, "lookTransform", cameraOffset);
            feelNode.SetActive(true);
            return feel;
        }
    }
}
