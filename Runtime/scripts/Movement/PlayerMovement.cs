using jeanf.EventSystem;
using jeanf.validationTools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    public class PlayerMovement : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Validation("FPS/Move action is required — desktop & gamepad locomotion is dead without it.")]
        [SerializeField] InputActionReference fpsMoveAction;
        [Validation("XR/Move action is required — VR stick locomotion is dead without it.")]
        [SerializeField] InputActionReference xrMoveAction;
        [Tooltip("Optional — auto-resolved from the FPS map ('Elevate') when left empty.")]
        [SerializeField] InputActionReference fpsElevateAction;
        [Tooltip("Optional — auto-resolved from the FPS map ('ActivateFreeCam') when left empty.")]
        [SerializeField] InputActionReference freeCamAction;
        [Validation("PlayerInput is required for control-scheme switching.")]
        [SerializeField] PlayerInput playerInput;
        [Validation("CharacterController is required — the player cannot move or collide without it.")]
        [SerializeField] CharacterController controller;
        [Validation("FPSCameraMovement is required for look direction while walking.")]
        [SerializeField] FPSCameraMovement mouseLook;
        Vector3 groundLevel = Vector3.zero;
        bool hasGroundLevel; // only true after a teleport supplied a real ground height
        bool isFreeCamOn = false;
        [SerializeField] private float speed;
        BroadcastControlsStatus.ControlScheme controlScheme;
        public float Speed
        {
            get => speed;
            set => speed = value;
        }
        float gravity = 9.81f;
        [SerializeField] float distToGround;

        [Header("Weight & momentum")]
        [Tooltip("m/s² used to accelerate toward and brake away from the wanted velocity. Lower feels heavier.")]
        [SerializeField] float speedChangeRate = 8f;
        [Tooltip("Horizontal drag (m/s²) while airborne. Momentum carries through a jump with a slight bleed — there is NO mid-air acceleration or steering.")]
        [SerializeField] float airDrag = 0.5f;

        [Header("Sprint (FPS/Sprint action, e.g. Left Shift / left stick press)")]
        [SerializeField] float sprintSpeedMultiplier = 1.8f;

        [Header("Jump (FPS/Jump action, e.g. Space / gamepad south)")]
        [Tooltip("Peak height of a jump in meters. Only available grounded and not crouched; XR has no jump.")]
        [SerializeField] float jumpHeight = 1.1f;

        [Header("Crouch (FPS/Crouch action, e.g. Left Ctrl / gamepad east)")]
        [SerializeField] bool crouchIsToggle = true;
        [Tooltip("Crouched capsule height as a fraction of the standing height. The camera drops by the same fraction.")]
        [Range(0.3f, 0.9f)][SerializeField] float crouchHeightRatio = 0.55f;
        [SerializeField] float crouchTransitionSeconds = 0.25f;
        [SerializeField] float crouchSpeedMultiplier = 0.5f;
        [Tooltip("Layers that can block standing up. The player's own colliders are ignored because the check starts inside them.")]
        [SerializeField] LayerMask standUpObstructionMask = ~0;

        [Header("Scene loading")]
        [Tooltip("Even without the SceneIsLoading channel (wired on the PlayerEventBridge): hold gravity until something exists below the player to land on. Protects additive scene loading and spawning over the void.")]
        [SerializeField] bool waitForGroundBeforeFalling = true;

        [Header("Debug")]
        [Tooltip("Log every sprint/crouch/jump input and the crouch state machine — turn on when an input seems dead.")]
        [SerializeField] bool debugInput = false;

        bool isMoving;
        Vector2 moveValue;
        Vector2 verticalMoveValue;
        private Transform cameraTransform;

        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction jumpAction;
        private InputAction _elevateAction;
        private InputAction _freeCamToggleAction;
        private bool sprintHeld;
        private bool crouchHeld;
        private bool jumpRequested;
        private float verticalVelocity;
        private bool sceneIsLoading;
        private bool menuOpen;
        private bool paused;
        private bool groundSeen;
        private bool waitingForGroundWarned;
        private bool crouchToggled;
        private float crouchBlend; // 0 = standing, 1 = fully crouched
        private float standingHeight;
        private Vector3 standingCenter;
        private float standingCameraY;
        private Vector3 planarVelocity = Vector3.zero;
        private bool missingCameraWarned;

        /// <summary>Current horizontal velocity applied to the CharacterController (m/s). This is the COMMANDED velocity — it stays high while pushing against a wall.</summary>
        public Vector3 PlanarVelocity => planarVelocity;
        /// <summary>Horizontal velocity the controller ACTUALLY achieved last frame (m/s) — zero when blocked by a wall. Use this to drive animation and camera feel.</summary>
        public Vector3 ActualPlanarVelocity
        {
            get
            {
                if (controller == null || !controller.enabled) return Vector3.zero;
                var velocity = controller.velocity;
                velocity.y = 0f;
                return velocity;
            }
        }
        /// <summary>0..1 fraction of the maximum (sprint) speed — drives head bob / body animation.</summary>
        public float NormalizedSpeed => speed <= 0f ? 0f : planarVelocity.magnitude / (speed * Mathf.Max(1f, sprintSpeedMultiplier));
        public bool IsSprinting => sprintHeld && !IsCrouched && isMoving;
        /// <summary>0 = standing, 1 = fully crouched (mid-transition values in between).</summary>
        public float CrouchBlend => crouchBlend;
        public bool IsCrouched => crouchBlend > 0.5f;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public Vector2 MoveInput => moveValue;
        /// <summary>Diagnostics: is the movement input currently registering as "moving".</summary>
        public bool IsMoving => isMoving;
        /// <summary>Diagnostics: is the CharacterController present and enabled (walking is dead when it is not).</summary>
        public bool ControllerEnabled => controller != null && controller.enabled;

        /// <summary>
        /// Diagnostics: full movement + input state dump for logging. Reveals which stage
        /// of the walk pipeline is failing — no input (moveInput 0 while a key is down),
        /// a frozen gate (locomotionLocked / menu / pause / sceneLoading), a disabled or
        /// un-grounded controller, or input present but the controller not actually moving.
        /// </summary>
        public string DiagnosticSnapshot()
        {
            var kb = Keyboard.current;
            var wPressed = kb != null && kb.wKey.isPressed;
            var action = fpsMoveAction != null ? fpsMoveAction.action : null;
            var actionEnabled = action != null && action.enabled;
            var controlsCount = action != null ? action.controls.Count : -1;
            var boundDevices = "n/a";
            if (action != null && action.controls.Count > 0)
            {
                var set = new System.Collections.Generic.HashSet<string>();
                for (var i = 0; i < action.controls.Count; i++) set.Add(action.controls[i].device.displayName);
                boundDevices = string.Join(",", set);
            }
            var actualVel = controller != null && controller.enabled ? controller.velocity : Vector3.zero;
            var groundedRobust = controller != null && controller.enabled && IsGroundedForMovement();
            return
                $"movement: scheme={BroadcastControlsStatus.controlScheme} ctrlEnabled={ControllerEnabled} grounded={IsGrounded} groundedRobust={groundedRobust} " +
                $"locomotionLocked={LocomotionLocked} menuOpen={menuOpen} paused={paused} sceneLoading={sceneIsLoading} isFreeCamOn={isFreeCamOn}\n" +
                $"    moveInput={moveValue} isMoving={isMoving} planarVel={planarVelocity} actualVel={actualVel} rootPos={transform.position}\n" +
                $"    Keyboard={(kb != null ? "Yes" : "NULL")} wPressed={wPressed}  fpsMoveAction(legacy route): enabled={actionEnabled} controls={controlsCount} devices=[{boundDevices}]";
        }
        /// <summary>Current vertical velocity in m/s (M&K/gamepad modes; negative while falling).</summary>
        public float VerticalVelocity => verticalVelocity;
        /// <summary>When true (e.g. while seated) movement, crouch and gravity are suspended. Input is still read.</summary>
        public bool LocomotionLocked { get; set; }

        private void Awake()
        {
            var mainCamera = Camera.main;
            if (mainCamera != null) cameraTransform = mainCamera.transform;

            if (controller != null)
            {
                standingHeight = controller.height;
                standingCenter = controller.center;

                if (CollidableLayersMask() == 0)
                {
                    Debug.LogWarning($"{LogPrefix} PlayerMovement on '{name}': the player's layer " +
                        $"'{LayerMask.LayerToName(controller.gameObject.layer)}' ({controller.gameObject.layer}) collides " +
                        "with NOTHING in Project Settings > Physics > Layer Collision Matrix — the capsule cannot stand " +
                        "on any floor. Put the Player variant root on a layer that collides with your floors.", this);
                }
            }
            if (mouseLook != null && mouseLook.CameraOffset != null)
                standingCameraY = mouseLook.CameraOffset.localPosition.y;
        }

        private void OnEnable()
        {
            BroadcastControlsStatus.SendControlScheme += OnReceivedControlSchemeChange;

            if (fpsMoveAction != null && fpsMoveAction.action != null)
            {
                fpsMoveAction.action.performed += OnFpsMovePerformed;
                fpsMoveAction.action.canceled += OnFpsMoveCanceled;
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} PlayerMovement on '{name}': fpsMoveAction is not assigned — " +
                    "keyboard/gamepad movement is disabled. Assign the FPS/Move action on your Player variant.", this);
            }

            // Reference wins when assigned; otherwise the actions ship in the
            // package FPS map and are resolved by name — no prefab wiring needed.
            _elevateAction = fpsElevateAction != null && fpsElevateAction.action != null
                ? fpsElevateAction.action
                : ResolveOptionalAction("Elevate");
            if (_elevateAction != null)
            {
                _elevateAction.performed += OnFpsElevatePerformed;
                _elevateAction.canceled += OnFpsElevateCancelled;
            }

            _freeCamToggleAction = freeCamAction != null && freeCamAction.action != null
                ? freeCamAction.action
                : ResolveOptionalAction("ActivateFreeCam");
            if (_freeCamToggleAction != null)
            {
                _freeCamToggleAction.performed += OnFreeCamPerformed;
            }

            if (xrMoveAction != null && xrMoveAction.action != null)
            {
                xrMoveAction.action.performed += OnXrMovePerformed;
                xrMoveAction.action.canceled += OnXrMoveCanceled;
            }

            sprintAction = ResolveOptionalAction("Sprint");
            if (sprintAction != null)
            {
                sprintAction.performed += OnSprintPerformed;
                sprintAction.canceled += OnSprintCanceled;
            }
            crouchAction = ResolveOptionalAction("Crouch");
            if (crouchAction != null)
            {
                crouchAction.performed += OnCrouchPerformed;
                crouchAction.canceled += OnCrouchCanceled;
            }
            jumpAction = ResolveOptionalAction("Jump");
            if (jumpAction != null)
            {
                jumpAction.performed += OnJumpPerformed;
            }

            PlayerEvents.ObjectTeleported += SetGroundLevel;
            PlayerEvents.SceneLoadingChanged += OnSceneLoadingChanged;
            PlayerEvents.MenuStateChanged += OnMenuStateChanged;
            PlayerEvents.PauseRequested += OnPauseRequested;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (fpsMoveAction != null && fpsMoveAction.action != null)
            {
                fpsMoveAction.action.performed -= OnFpsMovePerformed;
                fpsMoveAction.action.canceled -= OnFpsMoveCanceled;
            }

            if (xrMoveAction != null && xrMoveAction.action != null)
            {
                xrMoveAction.action.performed -= OnXrMovePerformed;
                xrMoveAction.action.canceled -= OnXrMoveCanceled;
            }
            if (_elevateAction != null)
            {
                _elevateAction.performed -= OnFpsElevatePerformed;
                _elevateAction.canceled -= OnFpsElevateCancelled;
            }

            if (_freeCamToggleAction != null)
            {
                _freeCamToggleAction.performed -= OnFreeCamPerformed;
            }

            if (sprintAction != null)
            {
                sprintAction.performed -= OnSprintPerformed;
                sprintAction.canceled -= OnSprintCanceled;
            }
            if (crouchAction != null)
            {
                crouchAction.performed -= OnCrouchPerformed;
                crouchAction.canceled -= OnCrouchCanceled;
            }
            if (jumpAction != null)
            {
                jumpAction.performed -= OnJumpPerformed;
            }

            PlayerEvents.ObjectTeleported -= SetGroundLevel;
            PlayerEvents.SceneLoadingChanged -= OnSceneLoadingChanged;
            PlayerEvents.MenuStateChanged -= OnMenuStateChanged;
            PlayerEvents.PauseRequested -= OnPauseRequested;

            BroadcastControlsStatus.SendControlScheme -= OnReceivedControlSchemeChange;
        }

        private InputAction ResolveOptionalAction(string actionName)
        {
            if (playerInput == null || playerInput.actions == null)
            {
                Debug.LogWarning($"{LogPrefix} PlayerMovement on '{name}': playerInput (or its actions asset) is not assigned — " +
                    $"the '{actionName}' action cannot be resolved, so that feature is disabled.", this);
                return null;
            }
            var action = playerInput.actions.FindAction($"FPS/{actionName}", throwIfNotFound: false);
            if (action == null)
            {
                Debug.LogWarning($"{LogPrefix} PlayerMovement on '{name}': no '{actionName}' action in the FPS map of " +
                    $"'{playerInput.actions.name}' — that feature is disabled. The package ships it in UniversalPlayer_InputActions; " +
                    $"if your project uses its own copy of the asset, add a Button action named '{actionName}' to the FPS map.", this);
            }
            return action;
        }

        private void SetGroundLevel(TeleportInformation teleportInfo)
        {
            groundLevel = teleportInfo.targetDestination.position;
            hasGroundLevel = true;
        }
        // The main menu (Escape) and the pause flow both freeze locomotion: walking
        // around behind an open menu is never intended in the simulator. Always logged:
        // a stuck freeze silently kills crouch/jump/walk and is otherwise invisible.
        private void OnMenuStateChanged(bool isOpen)
        {
            menuOpen = isOpen;
            LogFreezeState();
        }

        private void OnPauseRequested(bool isPaused)
        {
            paused = isPaused;
            LogFreezeState();
        }

        private void LogFreezeState()
        {
            Debug.Log($"{LogPrefix} locomotion {(menuOpen || paused ? "FROZEN" : "resumed")} (menuOpen: {menuOpen}, paused: {paused})", this);
        }

        private void OnSceneLoadingChanged(bool loading)
        {
            sceneIsLoading = loading;
            if (loading)
            {
                // New content is streaming in: freeze momentum and re-check for ground
                // afterwards (the floor under our feet may be part of what changes).
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                groundSeen = false;
                waitingForGroundWarned = false;
            }
        }

        /// <summary>
        /// Every layer the player's capsule can actually collide with, per the
        /// Physics Layer Collision Matrix.
        /// </summary>
        private int CollidableLayersMask()
        {
            var playerLayer = controller.gameObject.layer;
            var mask = 0;
            for (var i = 0; i < 32; i++)
            {
                if (!Physics.GetIgnoreLayerCollision(playerLayer, i)) mask |= 1 << i;
            }
            return mask;
        }

        /// <summary>
        /// True once the player has LANDABLE ground to fall onto (geometry on a layer the
        /// capsule collides with). Until then gravity holds, so a player spawned before
        /// additive scenes finish loading — or standing above floors on non-colliding
        /// layers — does not drop through the world.
        /// </summary>
        private bool GroundExistsBelow()
        {
            if (groundSeen || !waitForGroundBeforeFalling) return true;
            if (controller.isGrounded)
            {
                groundSeen = true;
                return true;
            }

            var worldCenter = transform.TransformPoint(controller.center);
            var bottom = worldCenter + Vector3.down * (controller.height * 0.5f - controller.radius);
            var collidable = CollidableLayersMask();
            if (collidable != 0 && Physics.SphereCast(bottom, controller.radius * 0.5f, Vector3.down, out _, 1000f,
                    collidable, QueryTriggerInteraction.Ignore))
            {
                groundSeen = true;
                if (waitingForGroundWarned) Debug.Log($"{LogPrefix} PlayerMovement on '{name}': ground detected below — gravity enabled.", this);
                return true;
            }

            if (!waitingForGroundWarned)
            {
                waitingForGroundWarned = true;
                var playerLayer = controller.gameObject.layer;
                if (collidable == 0)
                {
                    Debug.LogWarning($"{LogPrefix} PlayerMovement on '{name}': holding gravity — the player's layer " +
                        $"'{LayerMask.LayerToName(playerLayer)}' ({playerLayer}) collides with NOTHING in " +
                        "Project Settings > Physics > Layer Collision Matrix, so the capsule can never land. " +
                        "Put the Player variant root on a layer that collides with your floors (or fix the matrix). " +
                        "Tools/UniversalPlayer/ValidateSetup checks this too.", this);
                }
                else if (Physics.Raycast(bottom, Vector3.down, out var blockedHit, 1000f,
                             Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    Debug.LogWarning($"{LogPrefix} PlayerMovement on '{name}': holding gravity — there IS geometry below " +
                        $"('{blockedHit.collider.name}' on layer '{LayerMask.LayerToName(blockedHit.collider.gameObject.layer)}') " +
                        $"but the player's layer '{LayerMask.LayerToName(playerLayer)}' does NOT collide with it, so the " +
                        "capsule would fall straight through. Fix the Physics Layer Collision Matrix or move the player/floor " +
                        "to layers that collide. Tools/UniversalPlayer/ValidateSetup checks this too.", this);
                }
                else
                {
                    Debug.LogWarning($"{LogPrefix} PlayerMovement on '{name}': holding gravity — there is NOTHING below the " +
                        "player to land on. Either the scene is still loading (wire the SceneIsLoading channel for an explicit " +
                        "gate) or the player spawned over the void. Gravity resumes as soon as ground appears below.", this);
                }
            }
            return false;
        }

        private BroadcastControlsStatus.ControlScheme preFreecamScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;

        private BroadcastControlsStatus _controls;

        private void OnFreeCamPerformed(InputAction.CallbackContext ctx) => ActivateFreeMove();
        private void ActivateFreeMove()
        {
            // FreeCam is a LOGICAL sub-mode now (no device re-masking): toggle it through
            // the control authority so the decoupled input state is never disturbed.
            if (_controls == null && playerInput != null) _controls = playerInput.GetComponent<BroadcastControlsStatus>();
            if (_controls == null) return;

            switch (controlScheme)
            {
                case BroadcastControlsStatus.ControlScheme.KeyboardMouse:
                case BroadcastControlsStatus.ControlScheme.Gamepad:
                    preFreecamScheme = controlScheme;
                    _controls.SetFreecam(true, preFreecamScheme);
                    isFreeCamOn = true;
                    break;
                case BroadcastControlsStatus.ControlScheme.Freecam:
                    // Return to wherever we came from (keyboard or gamepad).
                    _controls.SetFreecam(false, preFreecamScheme);
                    isFreeCamOn = false;
                    break;
            }
        }
        private void OnFpsElevatePerformed(InputAction.CallbackContext ctx)
        {
            SetVerticalMoveValue(ctx.ReadValue<Vector2>());
            SetIsMoving(true);
        }

        private void OnReceivedControlSchemeChange(BroadcastControlsStatus.ControlScheme newScheme)
        {
            var leavingXr = controlScheme == BroadcastControlsStatus.ControlScheme.XR
                            && newScheme != BroadcastControlsStatus.ControlScheme.XR;

            if (leavingXr)
            {
                // Drop any momentum XRI locomotion left behind so desktop walking starts
                // clean. (Grounding is fine on exit — gravity/fall behaviour is unchanged —
                // so no reposition is needed; the "can't walk" bug was input, not grounding.)
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                jumpRequested = false;
            }
            else if (hasGroundLevel)
            {
                // Desktop<->desktop: realign with the last teleport's ground height — but
                // ONLY when a teleport actually set one. Before the first teleport,
                // groundLevel is (0,0,0): snapping to y=0 dropped the player INTO/below thin
                // floors and they fell through the world.
                controller.enabled = false;
                var p = playerInput.gameObject.transform.position;
                playerInput.gameObject.transform.position = new Vector3(p.x, groundLevel.y, p.z);
                controller.enabled = true;
            }

            this.controlScheme = newScheme;

            // If we were flying the FreeCam and something else changed the scheme
            // (the Ctrl+Alt+K desktop-force, a battery failsafe, ...), leave FreeCam
            // cleanly so gravity/fall-recovery resume and the toggle stays in sync.
            if (newScheme != BroadcastControlsStatus.ControlScheme.Freecam) isFreeCamOn = false;
        }
        private void OnFpsElevateCancelled(InputAction.CallbackContext ctx)
        {
            SetVerticalMoveValue(Vector2.zero);
            SetIsMoving(false);
        }

        private void OnFpsMovePerformed(InputAction.CallbackContext ctx)
        {
            SetMoveValue(ctx.ReadValue<Vector2>());
            SetIsMoving(true);
        }

        private void OnFpsMoveCanceled(InputAction.CallbackContext ctx)
        {
            SetMoveValue(Vector2.zero);
            SetIsMoving(false);
        }

        private void OnXrMovePerformed(InputAction.CallbackContext ctx)
        {
            SetIsMoving(true);
        }

        private void OnXrMoveCanceled(InputAction.CallbackContext ctx)
        {
            SetIsMoving(false);
        }

        private void OnSprintPerformed(InputAction.CallbackContext ctx) => SetSprintHeld(true);
        private void OnSprintCanceled(InputAction.CallbackContext ctx) => SetSprintHeld(false);
        private void OnCrouchPerformed(InputAction.CallbackContext ctx) => SetCrouchHeld(true);
        private void OnCrouchCanceled(InputAction.CallbackContext ctx) => SetCrouchHeld(false);
        private void OnJumpPerformed(InputAction.CallbackContext ctx) => RequestJump();

        /// <summary>
        /// Discards a buffered jump press. The SitController calls this when a
        /// jump was consumed to stand up from a seat — the same press must not
        /// ALSO launch the player the moment locomotion unlocks.
        /// </summary>
        public void CancelPendingJump() => jumpRequested = false;

        /// <summary>Queues a jump for the next frame; ignored unless grounded, standing and in M&K/gamepad mode.</summary>
        public void RequestJump()
        {
            if (debugInput) Debug.Log($"{LogPrefix} jump input (grounded: {IsGrounded}, crouchBlend: {crouchBlend:F2})", this);
            jumpRequested = true;
        }

        /// <summary>
        /// Call after teleporting the player from outside (FallRecovery does): drops all
        /// accumulated momentum — a rescued player must not resume falling at terminal
        /// velocity — and re-checks that ground exists below before gravity resumes, so
        /// a recovery spot over the void means hovering, not another infinite fall.
        /// </summary>
        public void OnExternalTeleport()
        {
            planarVelocity = Vector3.zero;
            verticalVelocity = 0f;
            jumpRequested = false;
            groundSeen = false;
            waitingForGroundWarned = false;
        }

        // Public input seams: used by the input callbacks above, the Hands Test Bench and PlayMode tests.
        public void SetMoveValue(Vector2 move)
        {
            moveValue = move;
        }

        public void SetSprintHeld(bool held)
        {
            if (debugInput) Debug.Log($"{LogPrefix} sprint input: {held}", this);
            sprintHeld = held;
        }

        public void SetCrouchHeld(bool held)
        {
            if (debugInput) Debug.Log($"{LogPrefix} crouch input: {held} (toggleMode: {crouchIsToggle}, scheme: {BroadcastControlsStatus.controlScheme}, grounded: {IsGrounded}, blend: {crouchBlend:F2})", this);
            if (crouchIsToggle)
            {
                if (held) crouchToggled = !crouchToggled;
            }
            else
            {
                crouchHeld = held;
            }
        }

        public void SetIsMoving(bool isMoving)
        {
            this.isMoving = isMoving;
            if (!isFreeCamOn)
            {
                PlayerEvents.RaisePlayerMoving(isMoving);
            }
        }

        private void SetVerticalMoveValue(Vector2 move)
        {
            verticalMoveValue = move;
        }

        // FreeCam is toggled with Ctrl+Alt+F from any desktop mode — hop in and out of
        // a free-fly camera (no cursor while flying). Read directly off the keyboard so
        // it works regardless of the active control scheme / action map.
        private void Update()
        {
            if (FreeCamComboPressed()) ActivateFreeMove();
            PollDesktopMoveInput();
        }

        /// <summary>
        /// Reads walk input by polling the physical devices DIRECTLY each frame in desktop
        /// modes (WASD/arrows off Keyboard.current, left stick off Gamepad.current) instead
        /// of through the FPS/Move action. The action route proved unreliable across mode
        /// switches — the Move Value action's callback/value would stall while the
        /// PassThrough mouse-look kept working ("look works, WASD dead"). Reading the keys
        /// directly cannot be broken by any device-pairing / binding-resolution state, so
        /// desktop walking always works. XR locomotion still uses xrMoveAction; FreeCam
        /// reads moveValue too, so this feeds it as well. Changes are pushed only on
        /// transition, so the player-moving event is not re-raised every frame.
        /// </summary>
        private void PollDesktopMoveInput()
        {
            var scheme = BroadcastControlsStatus.controlScheme;
            var desktop = scheme == BroadcastControlsStatus.ControlScheme.KeyboardMouse
                          || scheme == BroadcastControlsStatus.ControlScheme.Gamepad
                          || scheme == BroadcastControlsStatus.ControlScheme.Freecam;
            if (!desktop) return;

            var value = Vector2.zero;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) value.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) value.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) value.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) value.x -= 1f;
            }

            if (value == Vector2.zero)
            {
                var gamepad = Gamepad.current;
                if (gamepad != null)
                {
                    var stick = gamepad.leftStick.ReadValue();
                    if (stick.sqrMagnitude > 0.02f) value = stick; // deadzone
                }
            }

            value = Vector2.ClampMagnitude(value, 1f);
            if (value != moveValue) SetMoveValue(value);

            var moving = value.sqrMagnitude > 0.0001f;
            if (moving != isMoving) SetIsMoving(moving);
        }

        private static bool FreeCamComboPressed()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.fKey.wasPressedThisFrame) return false;
            if (!(kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed)) return false;
            if (!(kb.leftAltKey.isPressed || kb.rightAltKey.isPressed)) return false;
            return true;
        }

        private void LateUpdate()
        {
            if (LocomotionLocked || menuOpen || paused)
            {
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                jumpRequested = false;
                return;
            }
            if (controller == null || !controller.enabled) return;

            if (sceneIsLoading)
            {
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                jumpRequested = false;
                return;
            }

            var scheme = BroadcastControlsStatus.controlScheme;
            if (scheme == BroadcastControlsStatus.ControlScheme.Freecam)
            {
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                if (isMoving) MoveFreeCam(moveValue, verticalMoveValue);
            }
            else if (scheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                // XRI owns XR locomotion; drop any leftover momentum so switching back
                // to keyboard does not start with a stale velocity. Gravity stays the
                // legacy constant-velocity kind here (no jump in XR).
                planarVelocity = Vector3.zero;
                verticalVelocity = 0f;
                jumpRequested = false;
                if (!controller.isGrounded && GroundExistsBelow())
                {
                    controller.Move(Vector3.down * gravity * Time.deltaTime);
                }
            }
            else
            {
                if (!GroundExistsBelow()) return;
                UpdateCrouch(Time.deltaTime);
                UpdateMomentumMove(Time.deltaTime);
            }
        }

        private Transform LookTransform()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            if (cameraTransform != null) return cameraTransform;
            if (mouseLook != null && mouseLook.CameraOffset != null)
            {
                if (!missingCameraWarned)
                {
                    missingCameraWarned = true;
                    Debug.LogWarning($"{LogPrefix} PlayerMovement on '{name}': no camera tagged MainCamera was found — " +
                        "movement directions fall back to the CameraOffset transform. Add a Camera to your Player variant " +
                        "(the package prefab intentionally ships without one).", this);
                }
                return mouseLook.CameraOffset;
            }
            return transform;
        }

        private void UpdateMomentumMove(float dt)
        {
            var grounded = IsGroundedForMovement();
            if (grounded)
            {
                var look = LookTransform();
                var forward = look.forward;
                forward.y = 0f;
                var right = look.right;
                right.y = 0f;
                if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
                forward.Normalize();
                right.Normalize();

                var input = Vector2.ClampMagnitude(isMoving ? moveValue : Vector2.zero, 1f);
                var wishDirection = forward * input.y + right * input.x;
                if (wishDirection.sqrMagnitude > 1f) wishDirection.Normalize();

                var targetSpeed = speed * CurrentSpeedMultiplier();
                var targetVelocity = wishDirection * targetSpeed;

                // Weight & momentum: the velocity chases the wanted velocity instead of snapping to it.
                planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, speedChangeRate * dt);
            }
            else
            {
                // Airborne: momentum from takeoff carries, bleeding slightly — no
                // acceleration, no sprint boost, no steering until the feet touch down.
                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, airDrag * dt);
            }

            // Vertical: real integrated gravity (accelerating falls, enables jumping).
            if (grounded)
            {
                // Small downward bias keeps the capsule pressed to the ground so
                // isGrounded stays reliable on slopes and steps.
                if (verticalVelocity < 0f) verticalVelocity = -2f;
                if (jumpRequested && crouchBlend < 0.1f)
                {
                    verticalVelocity = Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, jumpHeight));
                }
            }
            jumpRequested = false;
            verticalVelocity -= gravity * dt;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
        }

        /// <summary>
        /// Ground test for locomotion. <see cref="CharacterController.isGrounded"/> alone is
        /// unreliable after another system moves the controller — coming back from VR the XR
        /// rig moves the shared CharacterController, leaving isGrounded stuck FALSE while the
        /// capsule sits on the floor, which silently killed desktop walking (WASD only
        /// applies while grounded). Confirm with a short downward cast so movement resumes
        /// the moment the feet are actually on/just-above the floor.
        /// </summary>
        private bool IsGroundedForMovement()
        {
            if (controller.isGrounded) return true;

            var collidable = CollidableLayersMask();
            if (collidable == 0) return false;

            var worldCenter = transform.TransformPoint(controller.center);
            var bottom = worldCenter + Vector3.down * (controller.height * 0.5f - controller.radius);
            // A little beyond the skin width — enough to catch "resting but isGrounded==false",
            // not so much that the player walks on air over a real ledge.
            return Physics.SphereCast(bottom, controller.radius * 0.9f, Vector3.down, out _,
                controller.skinWidth + 0.15f, collidable, QueryTriggerInteraction.Ignore);
        }

        private float CurrentSpeedMultiplier()
        {
            var crouchFactor = Mathf.Lerp(1f, crouchSpeedMultiplier, crouchBlend);
            var sprintFactor = sprintHeld && crouchBlend < 0.01f ? sprintSpeedMultiplier : 1f;
            return crouchFactor * sprintFactor;
        }

        private void UpdateCrouch(float dt)
        {
            if (standingHeight <= 0f) return;

            var wantCrouch = crouchIsToggle ? crouchToggled : crouchHeld;
            // Sprinting breaks the crouch (when there is headroom to stand).
            if (sprintHeld && wantCrouch && CanStandUp())
            {
                wantCrouch = false;
                crouchToggled = false;
            }
            // Never stand up into an obstacle.
            var target = wantCrouch || !CanStandUp() ? 1f : 0f;

            if (Mathf.Approximately(crouchBlend, target)) return;
            crouchBlend = Mathf.MoveTowards(crouchBlend, target, dt / Mathf.Max(0.01f, crouchTransitionSeconds));
            ApplyCrouchState();
        }

        private void ApplyCrouchState()
        {
            var height = Mathf.Lerp(standingHeight, standingHeight * crouchHeightRatio, crouchBlend);
            controller.height = height;
            // Keep the feet planted: the capsule shrinks from the top.
            controller.center = standingCenter - Vector3.up * ((standingHeight - height) * 0.5f);

            if (mouseLook != null && mouseLook.CameraOffset != null && standingCameraY > 0f)
            {
                var offset = mouseLook.CameraOffset.localPosition;
                offset.y = standingCameraY * Mathf.Lerp(1f, crouchHeightRatio, crouchBlend);
                mouseLook.CameraOffset.localPosition = offset;
            }
        }

        private bool CanStandUp()
        {
            if (crouchBlend <= 0f) return true;

            var worldCenter = transform.TransformPoint(controller.center);
            var standingTop = transform.TransformPoint(standingCenter).y + standingHeight * 0.5f;
            var currentTop = worldCenter.y + controller.height * 0.5f;
            var castDistance = standingTop - currentTop;
            if (castDistance <= 0f) return true;

            var radius = controller.radius * 0.95f;
            var origin = worldCenter + Vector3.up * (controller.height * 0.5f - radius);
            return !Physics.SphereCast(origin, radius, Vector3.up, out _, castDistance,
                standUpObstructionMask, QueryTriggerInteraction.Ignore);
        }

        private void MoveFreeCam(Vector2 move, Vector2 verticalMove)
        {
            var look = LookTransform();
            var moveDirection = (look.forward * move.y) + (look.right * move.x) + (look.up * verticalMove.y);
            controller.Move(moveDirection.normalized * speed * Time.deltaTime);
        }
    }
}
