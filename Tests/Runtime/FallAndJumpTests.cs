using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Jump (M&amp;K/gamepad) and the infinite-fall failsafe. Jumping only works grounded
    /// and standing; FallRecovery teleports the player back to the last grounded spot
    /// after a runaway descent, and must never trip on deliberate FreeCam flight.
    /// </summary>
    public class FallAndJumpTests
    {
        private GameObject _floor;
        private GameObject _player;
        private CharacterController _controller;
        private PlayerMovement _movement;
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
            _controller = _player.AddComponent<CharacterController>();
            _movement = _player.AddComponent<PlayerMovement>();
            SetField(_movement, "controller", _controller);
            SetField(_movement, "speed", 4f);
            SetField(_movement, "speedChangeRate", 8f);
            SetField(_movement, "jumpHeight", 1.1f);
            _player.SetActive(true);

            for (var i = 0; i < 15; i++) yield return null;
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
            Assert.That(info, Is.Not.Null, $"Field '{field}' not found on {target.GetType().Name} — was it renamed? Update FallAndJumpTests alongside the refactor.");
            info.SetValue(target, value);
        }

        [UnityTest]
        public IEnumerator Jump_RisesOffTheGround_ThenLandsBack()
        {
            var startY = _player.transform.position.y;

            _movement.RequestJump();
            var peak = startY;
            for (var i = 0; i < 90; i++)
            {
                yield return null;
                peak = Mathf.Max(peak, _player.transform.position.y);
                if (_controller.isGrounded && i > 10) break;
            }

            Assert.That(peak, Is.GreaterThan(startY + 0.4f),
                "Jump never lifted the player meaningfully off the ground.");
            Assert.That(peak, Is.LessThan(startY + 2f),
                "Jump flew way past the configured jumpHeight — gravity integration is off.");

            for (var i = 0; i < 120 && !_controller.isGrounded; i++) yield return null;
            Assert.That(_controller.isGrounded, Is.True, "Player never landed after the jump.");
            Assert.That(_player.transform.position.y, Is.EqualTo(startY).Within(0.1f),
                "Player did not come back down to the starting height.");
        }

        [UnityTest]
        public IEnumerator MidAir_SprintOrInput_CannotAccelerate()
        {
            _movement.SetMoveValue(Vector2.up);
            _movement.SetIsMoving(true);
            yield return new WaitForSeconds(1.5f); // reach walk speed
            var preJumpSpeed = _movement.PlanarVelocity.magnitude;
            Assert.That(preJumpSpeed, Is.GreaterThan(1f), "Test rig sanity: player should be walking before the jump.");

            _movement.RequestJump();
            for (var i = 0; i < 30 && _controller.isGrounded; i++) yield return null;
            Assert.That(_controller.isGrounded, Is.False, "Test rig sanity: player should be airborne after the jump.");

            _movement.SetSprintHeld(true); // pressing Shift mid-air must do NOTHING
            var maxAirSpeed = 0f;
            for (var i = 0; i < 60 && !_controller.isGrounded; i++)
            {
                yield return null;
                maxAirSpeed = Mathf.Max(maxAirSpeed, _movement.PlanarVelocity.magnitude);
            }

            Assert.That(maxAirSpeed, Is.LessThanOrEqualTo(preJumpSpeed + 0.05f),
                "The player ACCELERATED mid-air (sprint pressed after takeoff) — airborne momentum must only " +
                "carry and bleed, never grow.");

            _movement.SetSprintHeld(false);
            _movement.SetIsMoving(false);
            _movement.SetMoveValue(Vector2.zero);
        }

        [UnityTest]
        public IEnumerator Jump_IsIgnoredWhileCrouched()
        {
            _movement.SetCrouchHeld(true);
            yield return new WaitForSeconds(0.5f);
            Assert.That(_movement.IsCrouched, Is.True);

            var startY = _player.transform.position.y;
            _movement.RequestJump();
            var peak = startY;
            for (var i = 0; i < 30; i++)
            {
                yield return null;
                peak = Mathf.Max(peak, _player.transform.position.y);
            }
            Assert.That(peak, Is.LessThan(startY + 0.05f),
                "A crouched player jumped — jump must require standing.");
            _movement.SetCrouchHeld(false);
        }

        [UnityTest]
        public IEnumerator FallRecovery_TeleportsBackToTheLastGroundedSpot()
        {
            var recovery = _player.AddComponent<FallRecovery>();
            SetField(recovery, "controller", _controller);
            SetField(recovery, "playerRoot", _player.transform);
            SetField(recovery, "playerMovement", _movement);
            SetField(recovery, "maxFallSeconds", 0.4f);
            SetField(recovery, "maxFallMeters", 5f);
            SetField(recovery, "recoveryTarget", FallRecovery.RecoveryTarget.LastGroundedPosition);

            // Stand still long enough for a safe position to be recorded.
            yield return new WaitForSeconds(1f);
            var safeY = _player.transform.position.y;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("FallRecovery"));
            _floor.GetComponent<Collider>().enabled = false;

            var recovered = false;
            for (var i = 0; i < 300; i++)
            {
                yield return null;
                if (recovery.RecoveryCount >= 1) { recovered = true; break; }
            }
            Assert.That(recovered, Is.True, "The infinite fall was never detected.");
            Assert.That(_player.transform.position.y, Is.EqualTo(safeY).Within(1f),
                "Recovery did not teleport the player back near the last grounded position.");

            // The floor is STILL gone: the rescued player must hover at the safe spot
            // (ground probe re-armed, velocity zeroed) — not fall again at terminal
            // speed in an endless recover-fall loop.
            var yAfterRecovery = _player.transform.position.y;
            for (var i = 0; i < 90; i++) yield return null;
            Assert.That(recovery.RecoveryCount, Is.EqualTo(1),
                "FallRecovery fired again while the floor was still missing — the player must hover at the safe spot, not loop recover-fall.");
            Assert.That(_player.transform.position.y, Is.EqualTo(yAfterRecovery).Within(0.05f),
                "The rescued player kept sinking although there is nothing below to land on.");

            _floor.GetComponent<Collider>().enabled = true;
            for (var i = 0; i < 60 && !_controller.isGrounded; i++) yield return null;
            Assert.That(_controller.isGrounded, Is.True,
                "Player did not settle back on the (restored) floor after recovery.");
        }

        [UnityTest]
        public IEnumerator SchemeChangeBeforeAnyTeleport_MustNotSnapThePlayerToYZero()
        {
            // Regression: OnReceivedControlSchemeChange used to force position.y to
            // groundLevel.y, which is 0 before the first teleport. With the floor top
            // near y=0 that dropped the capsule INSIDE the floor slab at startup (the
            // scheme broadcast fires ~0.25s into play) and the player fell through the
            // world forever.
            SetField(_movement, "playerInput", _player.AddComponent<UnityEngine.InputSystem.PlayerInput>());
            yield return null;
            var yBefore = _player.transform.position.y;
            Assert.That(yBefore, Is.GreaterThan(0.5f), "Test rig sanity: player should stand well above y=0.");

            var handler = typeof(PlayerMovement).GetMethod("OnReceivedControlSchemeChange",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handler, Is.Not.Null, "OnReceivedControlSchemeChange was renamed — update FallAndJumpTests.");
            handler.Invoke(_movement, new object[] { BroadcastControlsStatus.ControlScheme.KeyboardMouse });
            yield return null;

            Assert.That(_player.transform.position.y, Is.EqualTo(yBefore).Within(0.05f),
                "A control-scheme change with no prior teleport moved the player vertically — " +
                "the y=0 ground snap is back and thin floors will swallow the player at startup.");
        }

        [UnityTest]
        public IEnumerator FallRecovery_WorldOriginMode_RecoversToOrigin()
        {
            // Start away from the origin so the teleport target is unambiguous.
            _controller.enabled = false;
            _player.transform.position = new Vector3(6f, 1.1f, 6f);
            _controller.enabled = true;
            for (var i = 0; i < 10; i++) yield return null;

            var recovery = _player.AddComponent<FallRecovery>();
            SetField(recovery, "controller", _controller);
            SetField(recovery, "playerRoot", _player.transform);
            SetField(recovery, "playerMovement", _movement);
            SetField(recovery, "maxFallSeconds", 0.4f);
            SetField(recovery, "maxFallMeters", 5f);
            SetField(recovery, "recoveryTarget", FallRecovery.RecoveryTarget.WorldOrigin);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("world origin"));
            _floor.GetComponent<Collider>().enabled = false;

            for (var i = 0; i < 300 && recovery.RecoveryCount < 1; i++) yield return null;
            Assert.That(recovery.RecoveryCount, Is.GreaterThanOrEqualTo(1), "The fall was never detected.");

            var position = _player.transform.position;
            Assert.That(new Vector2(position.x, position.z).magnitude, Is.LessThan(0.1f),
                "WorldOrigin recovery did not teleport the player to x=0, z=0.");
            Assert.That(position.y, Is.InRange(0f, 0.5f),
                "WorldOrigin recovery should hover just above y=0.");

            _floor.GetComponent<Collider>().enabled = true;
            for (var i = 0; i < 60 && !_controller.isGrounded; i++) yield return null;
            Assert.That(_controller.isGrounded, Is.True, "Player did not settle at the origin once the floor was back.");
        }

        [UnityTest]
        public IEnumerator NoGroundBelow_GravityHolds_UntilGroundAppears()
        {
            // A player spawned before additive scenes finish loading has NOTHING below —
            // it must hover (with a loud warning), not fall into the void.
            var stranded = new GameObject("StrandedPlayer");
            stranded.SetActive(false);
            stranded.transform.position = new Vector3(500f, 5f, 500f); // far from the test floor
            var strandedController = stranded.AddComponent<CharacterController>();
            var strandedMovement = stranded.AddComponent<PlayerMovement>();
            SetField(strandedMovement, "controller", strandedController);
            SetField(strandedMovement, "speed", 4f);
            SetField(strandedMovement, "speedChangeRate", 8f);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("holding gravity"));
            stranded.SetActive(true);

            var startY = stranded.transform.position.y;
            for (var i = 0; i < 40; i++) yield return null;
            Assert.That(stranded.transform.position.y, Is.EqualTo(startY).Within(0.01f),
                "Player fell although there was nothing below to land on — the ground-probe gravity hold is broken.");

            var lateFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lateFloor.transform.localScale = new Vector3(10f, 1f, 10f);
            lateFloor.transform.position = new Vector3(500f, 0f, 500f);
            for (var i = 0; i < 180 && !strandedController.isGrounded; i++) yield return null;

            Assert.That(strandedController.isGrounded, Is.True,
                "Gravity never resumed after ground appeared below the player.");

            Object.Destroy(stranded);
            Object.Destroy(lateFloor);
        }

        [UnityTest]
        public IEnumerator FloorOnNonCollidingLayer_GravityHolds_AndNamesBothLayers()
        {
            // The floor exists but the collision matrix says the player passes through it:
            // the player must hover and the warning must name the exact layer mismatch.
            const int floorLayer = 26;
            const int playerLayer = 27;
            var prevIgnore = Physics.GetIgnoreLayerCollision(floorLayer, playerLayer);
            var prevIgnoreDefault = Physics.GetIgnoreLayerCollision(playerLayer, 0);
            Physics.IgnoreLayerCollision(floorLayer, playerLayer, true);
            // The player's layer must collide with SOMETHING (Default) so the specific
            // layer-mismatch diagnosis fires — not the collides-with-nothing one.
            Physics.IgnoreLayerCollision(playerLayer, 0, false);

            GameObject stranded = null;
            GameObject ghostFloor = null;
            try
            {
                ghostFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ghostFloor.layer = floorLayer;
                ghostFloor.transform.localScale = new Vector3(10f, 1f, 10f);
                ghostFloor.transform.position = new Vector3(300f, -0.5f, 300f);

                stranded = new GameObject("LayerMismatchPlayer") { layer = playerLayer };
                stranded.SetActive(false);
                stranded.transform.position = new Vector3(300f, 1.1f, 300f);
                var controller = stranded.AddComponent<CharacterController>();
                var movement = stranded.AddComponent<PlayerMovement>();
                SetField(movement, "controller", controller);
                SetField(movement, "speed", 4f);

                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("does NOT collide"));
                stranded.SetActive(true);

                var startY = stranded.transform.position.y;
                for (var i = 0; i < 40; i++) yield return null;
                Assert.That(stranded.transform.position.y, Is.EqualTo(startY).Within(0.01f),
                    "Player fell toward a floor its layer cannot collide with — it would have tunneled through; gravity must hold.");

                Physics.IgnoreLayerCollision(floorLayer, playerLayer, false);
                for (var i = 0; i < 120 && !controller.isGrounded; i++) yield return null;
                Assert.That(controller.isGrounded, Is.True,
                    "Gravity never resumed after the collision matrix was fixed.");
            }
            finally
            {
                Physics.IgnoreLayerCollision(floorLayer, playerLayer, prevIgnore);
                Physics.IgnoreLayerCollision(playerLayer, 0, prevIgnoreDefault);
                if (stranded != null) Object.Destroy(stranded);
                if (ghostFloor != null) Object.Destroy(ghostFloor);
            }
        }

        [UnityTest]
        public IEnumerator SceneIsLoading_FreezesLocomotion_UntilLoadingEnds()
        {
            try
            {
                PlayerEvents.RaiseSceneLoading(true); // scenes started loading (bridge pipes the project channel here)
                _movement.SetMoveValue(Vector2.up);
                _movement.SetIsMoving(true);
                var frozen = _player.transform.position;
                yield return new WaitForSeconds(0.3f);
                Assert.That(Vector3.Distance(_player.transform.position, frozen), Is.LessThan(0.01f),
                    "Player moved while the scene was loading — locomotion must hold during loads.");

                PlayerEvents.RaiseSceneLoading(false); // loading finished
                yield return new WaitForSeconds(0.5f);
                Assert.That(Vector3.Distance(_player.transform.position, frozen), Is.GreaterThan(0.3f),
                    "Player never started moving after loading ended.");
            }
            finally
            {
                PlayerEvents.RaiseSceneLoading(false);
                _movement.SetIsMoving(false);
                _movement.SetMoveValue(Vector2.zero);
            }
        }

        [UnityTest]
        public IEnumerator FallRecovery_IgnoresDeliberateFreeCamDescent()
        {
            var recovery = _player.AddComponent<FallRecovery>();
            SetField(recovery, "controller", _controller);
            SetField(recovery, "playerRoot", _player.transform);
            SetField(recovery, "playerMovement", _movement);
            SetField(recovery, "maxFallSeconds", 0.3f);
            SetField(recovery, "maxFallMeters", 3f);
            yield return new WaitForSeconds(0.6f);

            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.Freecam;
            for (var i = 0; i < 60; i++)
            {
                _player.transform.position += Vector3.down * 0.2f; // flying downward on purpose
                yield return null;
            }

            Assert.That(recovery.RecoveryCount, Is.EqualTo(0),
                "FallRecovery fired during deliberate FreeCam descent — it must ignore FreeCam.");
        }
    }
}
