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

        // The classic Seat and the ECS seat proxy both feed SitController through SeatData.
        // If GetSeatData stops mapping the anchors, both worlds break — lock it down.
        [Test]
        public void Seat_GetSeatData_RoundTripsAnchors()
        {
            var sitAnchor = new GameObject("Sit").transform;
            sitAnchor.SetParent(_chair.transform, false);
            sitAnchor.SetPositionAndRotation(new Vector3(3f, 0.4f, 2f), Quaternion.Euler(0f, 45f, 0f));
            var exitAnchor = new GameObject("Exit").transform;
            exitAnchor.SetParent(_chair.transform, false);
            exitAnchor.SetPositionAndRotation(new Vector3(4f, 0f, 2f), Quaternion.Euler(0f, 120f, 0f));
            var handAnchor = new GameObject("Hand").transform;
            handAnchor.SetParent(_chair.transform, false);
            handAnchor.position = new Vector3(3.2f, 0.6f, 2.1f);

            SetField(_seat, "sitAnchor", sitAnchor);
            SetField(_seat, "exitAnchor", exitAnchor);
            SetField(_seat, "eyeHeightAboveSeat", 0.65f);
            SetField(_seat, "handSupportAnchor", handAnchor);

            var data = _seat.GetSeatData();

            Assert.That(Vector3.Distance(data.SitPosition, sitAnchor.position), Is.LessThan(0.001f), "SitPosition");
            Assert.That(data.SitFacingYaw, Is.EqualTo(45f).Within(0.01f), "SitFacingYaw");
            Assert.That(data.EyeHeightAboveSeat, Is.EqualTo(0.65f).Within(0.001f), "EyeHeightAboveSeat");
            Assert.That(data.HasExit, Is.True, "HasExit");
            Assert.That(Vector3.Distance(data.ExitPosition, exitAnchor.position), Is.LessThan(0.001f), "ExitPosition");
            Assert.That(data.ExitFacingYaw, Is.EqualTo(120f).Within(0.01f), "ExitFacingYaw");
            Assert.That(data.HasHandSupport, Is.True, "HasHandSupport");
            Assert.That(Vector3.Distance(data.HandSupportWorldPos, handAnchor.position), Is.LessThan(0.001f), "HandSupportWorldPos");
        }

        // Sitting must never spin the long way round: the body slerp + free-look unwind must
        // combine into the shortest turn to the seat, whatever the (unbounded) accumulated yaw.
        [Test]
        public void ShortestBlendLookYaw_MakesCombinedTurnTakeShortestPath()
        {
            AssertShortest(0f, 170f, -170f);   // body +170 while look reads -170 → naive 340, shortest -20
            AssertShortest(0f, 0f, 280f);      // seat faces the body; a 280° stored look → shortest -80
            AssertShortest(30f, 200f, 765f);   // multi-turn accumulation (720 + 45)
            AssertShortest(-90f, 90f, -350f);
        }

        private static void AssertShortest(float startYaw, float seatYaw, float lookYaw)
        {
            var blended = SitController.ShortestBlendLookYaw(startYaw, seatYaw, lookYaw);

            // No pop: same camera orientation as the raw look (equal mod 360).
            Assert.That(Mathf.DeltaAngle(lookYaw, blended), Is.EqualTo(0f).Within(0.01f),
                $"blended look changed the orientation (start {startYaw}, seat {seatYaw}, look {lookYaw}).");

            // Combined view turn = body slerp (rootDelta) + look unwind (-blended); it must equal the
            // shortest view delta and never exceed 180°.
            var combined = Mathf.DeltaAngle(startYaw, seatYaw) - blended;
            var shortest = Mathf.DeltaAngle(startYaw + lookYaw, seatYaw);
            Assert.That(Mathf.DeltaAngle(combined, shortest), Is.EqualTo(0f).Within(0.01f),
                $"combined turn is not the shortest path (start {startYaw}, seat {seatYaw}, look {lookYaw}).");
            Assert.That(Mathf.Abs(combined), Is.LessThanOrEqualTo(180.01f),
                $"combined turn goes the long way (start {startYaw}, seat {seatYaw}, look {lookYaw}).");
        }

        // The entity-world path: SitController driven by a raw SeatData with NO Seat GameObject —
        // exactly what the baked-seat proxy does. Proves sit/stand work without a live Seat.
        [UnityTest]
        public IEnumerator SitOn_WithRawSeatData_SitsAndExits()
        {
            var preSit = _player.transform.position;
            var sitPos = new Vector3(3f, 0.5f, 2f);
            var data = new SeatData(
                seatId: 123, name: "RawSeat",
                sitPosition: sitPos, sitFacingYaw: 90f,
                eyeHeightAboveSeat: 0.7f,
                hasExit: false, exitPosition: Vector3.zero, exitFacingYaw: 0f,
                hasHandSupport: false, handSupportWorldPos: Vector3.zero, handSupportWorldRot: Quaternion.identity);

            _sit.SitOn(data, true); // instant: teleport, no glide
            yield return null;

            Assert.That(_sit.IsSeated, Is.True, "SitOn(SeatData) did not seat the player.");
            Assert.That(Vector3.Distance(_player.transform.position, sitPos), Is.LessThan(0.01f),
                "SitOn(SeatData) did not teleport to the sit position.");
            Assert.That(_cameraOffset.localPosition.y, Is.EqualTo(0.7f).Within(0.001f),
                "SitOn(SeatData) did not set the seated eye height.");

            _sit.Exit(true);
            yield return null;

            Assert.That(_sit.IsSeated, Is.False, "Exit did not stand the player up.");
            Assert.That(Vector3.Distance(_player.transform.position, preSit), Is.LessThan(0.05f),
                "Exit did not restore the pre-sit position (no exitAnchor was set).");
        }

        // ---- VR stand-up: left stick held past standUpHoldSeconds (v1.6.0) ----
        // In VR the seat interactable is SIT-ONLY; standing is exclusively the left stick,
        // debounced two ways: a fresh push is required (a stick already held when sitting
        // down must be released first) and the push must be HELD for standUpHoldSeconds.

        private void EnterXrSeated(out float holdSeconds)
        {
            holdSeconds = 0.2f;
            SetField(_sit, "standUpHoldSeconds", holdSeconds);
            SetField(_sit, "exitGraceSeconds", 0.15f);
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;
            _seat.SitOnly(); // XR sit is instant (teleport, no glide)
            Assert.That(_sit.IsSeated, Is.True, "SitOnly did not seat the player in XR mode.");
        }

        [UnityTest]
        public IEnumerator VrStick_HeldPastThreshold_StandsUp()
        {
            EnterXrSeated(out var holdSeconds);
            yield return new WaitForSeconds(0.3f); // past the exit grace, stick released -> armed

            _movement.SetIsMoving(true);
            yield return new WaitForSeconds(holdSeconds * 0.4f);
            Assert.That(_sit.IsSeated, Is.True,
                "The player stood up BEFORE the stick was held for standUpHoldSeconds — the hold debounce is gone.");

            yield return new WaitForSeconds(holdSeconds * 1.5f);
            Assert.That(_sit.IsSeated, Is.False,
                "Holding the left stick past standUpHoldSeconds did not stand the player up in VR.");
            _movement.SetIsMoving(false);
        }

        [UnityTest]
        public IEnumerator VrStick_ShortFlick_DoesNotStand()
        {
            EnterXrSeated(out var holdSeconds);
            yield return new WaitForSeconds(0.3f);

            _movement.SetIsMoving(true);
            yield return new WaitForSeconds(holdSeconds * 0.4f); // released before the threshold
            _movement.SetIsMoving(false);
            yield return new WaitForSeconds(holdSeconds * 2f);

            Assert.That(_sit.IsSeated, Is.True,
                "A short stick flick (released before standUpHoldSeconds) stood the player up — it must not.");
        }

        [UnityTest]
        public IEnumerator VrStick_HeldThroughSit_NeedsAFreshPush()
        {
            // Walked INTO the chair holding the stick: that same hold must never pop the player
            // back up — the stick has to be released and pushed again.
            _movement.SetIsMoving(true);
            EnterXrSeated(out var holdSeconds);

            yield return new WaitForSeconds(0.3f + holdSeconds * 2f);
            Assert.That(_sit.IsSeated, Is.True,
                "A stick held from BEFORE sitting down stood the player up — the fresh-push debounce is gone.");

            _movement.SetIsMoving(false); // release -> re-arm
            yield return null;
            _movement.SetIsMoving(true);  // fresh push
            yield return new WaitForSeconds(holdSeconds * 1.5f);
            Assert.That(_sit.IsSeated, Is.False,
                "After releasing and re-pushing the stick, the player must stand up.");
            _movement.SetIsMoving(false);
        }

        [UnityTest]
        public IEnumerator SeatInteractable_IsSitOnly_WhileSeated()
        {
            EnterXrSeated(out _);
            var seatedPosition = _player.transform.position;

            // Grabbing/triggering the chair again must NOT stand the player up (no toggle).
            _seat.SitOnly();
            _sit.SitOn(_seat);
            yield return null;

            Assert.That(_sit.IsSeated, Is.True,
                "Selecting the seat while seated stood the player up — the interactable must be sit-only.");
            Assert.That(Vector3.Distance(_player.transform.position, seatedPosition), Is.LessThan(0.001f),
                "A repeated select while seated moved the player.");
        }
    }
}
