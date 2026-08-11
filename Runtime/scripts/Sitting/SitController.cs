using jeanf.EventSystem;
using jeanf.validationTools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Player-side half of the sitting system (ships wired on the Player prefab).
    ///
    /// All modes share the same core: lock locomotion, disable the CharacterController,
    /// teleport the player root to the seat anchor and set the camera height; exiting
    /// restores everything. Differences per mode:
    /// - M&amp;K / gamepad: FPS/Interact raycast finds the Seat, FirstPersonBody plays the
    ///   sit pose, moving stands you back up;
    /// - VR: the seat's XR interactable calls Seat.ToggleSit() — the root is lowered so
    ///   the user's real head lands at the seat's eye height (teleport + camera height,
    ///   nothing more), and exit is the interactable again.
    /// </summary>
    public class SitController : MonoBehaviour, IDebugBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        public static SitController Instance { get; private set; }

        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        [Validation("PlayerMovement is required — without it a seated player's locomotion cannot be locked (they slide out of the chair).")]
        [SerializeField] private PlayerMovement playerMovement;
        [Validation("CharacterController is required — it is disabled while seated so the capsule doesn't fight the seat placement.")]
        [SerializeField] private CharacterController controller;
        [Tooltip("The transform that gets teleported to the seat (the Player root).")]
        [Validation("Player root is required — it is the transform teleported to the seat. Sitting is disabled without it.")]
        [SerializeField] private Transform playerRoot;
        [Validation("Camera offset is required — the seated eye height is set on it. Sitting is disabled without it.")]
        [SerializeField] private Transform cameraOffset;
        [Tooltip("Optional: plays the sit pose / drives the IsSeated animator parameter.")]
        [SerializeField] private FirstPersonBody body;
        [Tooltip("Used to resolve the FPS/Interact action for the M&K raycast path.")]
        [SerializeField] private PlayerInput playerInput;
        [Tooltip("Optional (auto-found under the player root): look is blended toward the seat facing during the sit/stand glide instead of hard-cutting.")]
        [SerializeField] private FPSCameraMovement cameraLook;

        // Scenario sit requests arrive over PlayerEvents.SitRequested — the
        // PlayerEventBridge forwards the project's sitRequest channel (hub slot
        // on PlayerChannelsSO). One script talks SO channels; internals use delegates.

        [Header("M&K / gamepad interaction")]
        [SerializeField] private float interactMaxDistance = 2.5f;
        [SerializeField] private LayerMask seatMask = ~0;
        [Tooltip("Jump (Space / gamepad south) stands the player back up — Interact stays free for using things around the seat.")]
        [SerializeField] private bool exitOnJump = true;
        [Tooltip("Off by default: with it on, any move input stands the player up, which reads as being ejected from the chair.")]
        [SerializeField] private bool exitOnMoveInput = false;
        [SerializeField] private float exitGraceSeconds = 0.3f;

        [Header("VR")]
        [Tooltip("VR: push the LEFT stick and hold it this long to stand up (debounces accidental nudges). " +
                 "Grabbing/triggering a seat only ever SITS in VR — standing is the stick's job.")]
        [SerializeField] private float standUpHoldSeconds = 0.2f;
        [Tooltip("Seconds of the sitting-down glide (M&K/gamepad only — VR always teleports instantly, no imposed camera motion).")]
        [UnityEngine.Serialization.FormerlySerializedAs("transitionSeconds")]
        [SerializeField] private float sitTransitionSeconds = 0.85f;
        [Tooltip("Seconds of the standing-up glide — a touch longer than sitting: pushing up out of a chair is the heavier motion.")]
        [SerializeField] private float standTransitionSeconds = 1.1f;

        [Header("Transition realism (M&K / gamepad)")]
        [Tooltip("How far the view glances DOWN toward the feet mid-transition (degrees) — people check where they land.")]
        [SerializeField] private float glanceDownDegrees = 22f;
        [Tooltip("Extra weight-shift head dip mid-transition (meters), on top of the height change itself.")]
        [SerializeField] private float weightShiftDip = 0.05f;

        // Seated state + camera reset go through PlayerEvents; the PlayerEventBridge
        // forwards them onto the project's channels.

        public bool IsSeated { get; private set; }

        /// <summary>Seat id of the currently occupied seat (0 when not seated) — lets seat-side UI
        /// (e.g. a chair's tooltip) know whether THIS seat is the one being sat on.</summary>
        public int CurrentSeatId => IsSeated ? _seat.SeatId : 0;

        private SeatData _seat;
        // The live source of the current seat (classic Seat or ECS proxy), kept for identity
        // checks (scenario re-seat) and so a streamed-out entity seat can force an exit later.
        private ISeatSource _currentSource;

        private InputAction interactAction;
        private InputAction jumpAction;
        // VR stand-up (left-stick hold) state.
        private float _vrMoveHeldSince = -1f;
        private bool _vrArmed;
        // VR trigger-to-sit (G3): the seat whose interaction ray is currently hovering us.
        private ISeatSource _hoveredSeat;
        private readonly System.Collections.Generic.List<InputAction> _activateActions =
            new System.Collections.Generic.List<InputAction>();
        private readonly System.Collections.Generic.List<Behaviour> disabledLocomotionProviders =
            new System.Collections.Generic.List<Behaviour>();
        private Vector3 preSitPosition;
        private Quaternion preSitRotation;
        private float preSitCameraOffsetY;
        private float seatedSince;
        private bool missingRefsWarned;
        private Coroutine _transition;
        private bool _transitioning;

        /// <summary>Standing camera height above the player root (pre-sit value while seated).</summary>
        public float StandingCameraHeight => IsSeated || _transitioning
            ? preSitCameraOffsetY
            : cameraOffset != null ? cameraOffset.localPosition.y : 1.7f;

        private void OnEnable()
        {
            Instance = this;

            if (cameraLook == null && playerRoot != null)
                cameraLook = playerRoot.GetComponentInChildren<FPSCameraMovement>(true);

            PlayerEvents.SitRequested += OnSitRequested;

            if (playerInput != null && playerInput.actions != null)
            {
                interactAction = playerInput.actions.FindAction("FPS/Interact", throwIfNotFound: false);
                if (interactAction != null) interactAction.performed += OnInteract;
                else Debug.LogWarning($"{LogPrefix} SitController on '{name}': no 'Interact' action in the FPS map of " +
                    $"'{playerInput.actions.name}' — sitting via aim+interact is disabled (Seat.ToggleSit() still works).", this);

                jumpAction = playerInput.actions.FindAction("FPS/Jump", throwIfNotFound: false);
                if (jumpAction != null) jumpAction.performed += OnJumpWhileSeated;

                // G3: trigger (XRI Activate) sits when the interaction ray hovers a seat.
                // Both hands' Activate actions feed the same "press to sit" path.
                foreach (var mapName in new[] { "XRI LeftHand", "XRI RightHand" })
                {
                    var activate = playerInput.actions.FindAction($"{mapName}/Activate", throwIfNotFound: false);
                    if (activate == null) continue;
                    activate.performed += OnActivateWhileHovering;
                    _activateActions.Add(activate);
                }
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} SitController on '{name}': playerInput is not assigned — sitting via " +
                    "aim+interact is disabled (Seat.ToggleSit() still works).", this);
            }
        }

        private void OnDisable()
        {
            if (interactAction != null) interactAction.performed -= OnInteract;
            if (jumpAction != null) jumpAction.performed -= OnJumpWhileSeated;
            foreach (var activate in _activateActions) activate.performed -= OnActivateWhileHovering;
            _activateActions.Clear();
            _hoveredSeat = null;
            PlayerEvents.SitRequested -= OnSitRequested;
            if (Instance == this) Instance = null;
        }

        // Scenario-driven seating: while the screen is black (loading fade) the
        // placement is INSTANT — the player is revealed already seated. With the
        // world visible, the request plays the normal glide instead.
        private void OnSitRequested(GameObject seatObject)
        {
            var instant = FadeMask.ScreenFaded;

            if (seatObject == null)
            {
                if (IsSeated) Exit(instant);
                return;
            }

            var source = seatObject.GetComponentInParent<ISeatSource>();
            if (source == null)
            {
                Debug.LogWarning($"{LogPrefix} SitController: a sit was requested on '{seatObject.name}' but there is no " +
                    "Seat component on it (or its parents) — add a Seat and set its sit anchor.", seatObject);
                return;
            }

            if (IsSeated && ReferenceEquals(_currentSource, source)) return;
            if (IsSeated) Exit(true); // silent swap: release the previous seat instantly
            SitOn(source, instant);
        }

        /// <summary>
        /// Scenario sit by plain seat values — the GameObject-free twin of the sit-request flow,
        /// for seats resolved through <see cref="SeatRegistry"/> (baked SubScene seats, seats in
        /// other additive scenes). Same semantics: instant behind a black screen, already on this
        /// seat = no-op, seated elsewhere = silent instant swap.
        /// </summary>
        public void RequestSit(in SeatData seat)
        {
            if (IsSeated && CurrentSeatId == seat.SeatId) return;
            if (IsSeated) Exit(true);
            SitOn(seat, FadeMask.ScreenFaded);
        }

        private void OnJumpWhileSeated(InputAction.CallbackContext _)
        {
            if (!exitOnJump || !IsSeated || _transitioning) return;
            if (Time.time < seatedSince + exitGraceSeconds) return;
            Exit();
        }

        // G3: seats report when their interaction ray hovers/leaves them (Seat / SeatProxy
        // forward the XR interactable's hover events) so a trigger press can sit the player.
        public void NotifySeatHoverEntered(ISeatSource source)
        {
            if (source != null) _hoveredSeat = source;
        }

        public void NotifySeatHoverExited(ISeatSource source)
        {
            if (ReferenceEquals(_hoveredSeat, source)) _hoveredSeat = null;
        }

        // Trigger (XRI Activate) while the interaction ray hovers a seat sits the player.
        // VR only, sit-only: standing is the left stick's job, so this never toggles.
        private void OnActivateWhileHovering(InputAction.CallbackContext _)
        {
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR) return;
            if (IsSeated || _transitioning || _hoveredSeat == null) return;
            SitOn(_hoveredSeat);
        }

        private void OnInteract(InputAction.CallbackContext _)
        {
            // VR sits through the seat's own interactable, not through this raycast.
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR) return;
            if (_transitioning) return;

            if (IsSeated)
            {
                Exit();
                return;
            }

            // The press is aimed at world-space UI — the UI owns it. Without this a
            // click on a canvas would also sit on a chair standing behind it.
            if (DesktopWorldUiInteractor.UiHoverActive) return;

            var origin = Camera.main != null ? Camera.main.transform : cameraOffset;
            if (origin == null) return;
            if (!Physics.Raycast(new Ray(origin.position, origin.forward), out var hit,
                    interactMaxDistance, seatMask, QueryTriggerInteraction.Collide)) return;

            var source = hit.collider.GetComponentInParent<ISeatSource>();
            if (source != null) SitOn(source);
        }

        private void LateUpdate()
        {
            if (!IsSeated || _transitioning) return;
            var pastGrace = Time.time >= seatedSince + exitGraceSeconds;

            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                // VR stand-up: push the left stick and HOLD it for standUpHoldSeconds.
                // Debounce with _vrArmed so a stick that was already held when we sat
                // (walked into the chair) doesn't instantly pop us back up — it must be
                // RELEASED and pushed again to count.
                var moving = playerMovement != null && playerMovement.IsMoving;
                if (!pastGrace) { _vrArmed = false; _vrMoveHeldSince = -1f; return; }
                if (!moving) { _vrArmed = true; _vrMoveHeldSince = -1f; return; } // released -> armed for a fresh push
                if (!_vrArmed) return;                                             // still holding from before -> ignore
                if (_vrMoveHeldSince < 0f) _vrMoveHeldSince = Time.time;
                else if (Time.time >= _vrMoveHeldSince + standUpHoldSeconds) { _vrMoveHeldSince = -1f; Exit(); }
                return;
            }

            // Desktop / gamepad: optional stand-up on move input (unchanged).
            if (!exitOnMoveInput || !pastGrace) return;
            if (playerMovement != null && playerMovement.MoveInput.sqrMagnitude > 0.25f) Exit();
        }

        public void ToggleSit(ISeatSource source)
        {
            if (IsSeated) Exit();
            else SitOn(source);
        }

        public void SitOn(ISeatSource source) => SitOn(source, false);

        public void SitOn(ISeatSource source, bool instant)
        {
            if (source == null) return;
            var wasSeated = IsSeated;
            SitOn(source.GetSeatData(), instant);
            // Record identity only when this call actually seated the player (not on a
            // no-op because we were already seated), so scenario re-seat checks stay correct.
            if (!wasSeated && IsSeated) _currentSource = source;
        }

        public void SitOn(in SeatData seat, bool instant)
        {
            if (IsSeated) return;
            if (_transitioning)
            {
                if (!instant) return;
                CancelTransition(); // scenario requests preempt a running glide
            }
            if (!HasRequiredRefs()) return;

            preSitPosition = playerRoot.position;
            preSitRotation = playerRoot.rotation;
            preSitCameraOffsetY = cameraOffset.localPosition.y;

            controller.enabled = false;
            if (playerMovement != null) playerMovement.LocomotionLocked = true;

            // VR: PlayerMovement is not the locomotion authority — XRI's joystick move
            // providers are, and they must not slide a seated player off the chair.
            disabledLocomotionProviders.Clear();
            foreach (var provider in playerRoot.GetComponentsInChildren<
                         UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement.ContinuousMoveProvider>(false))
            {
                if (!provider.enabled) continue;
                provider.enabled = false;
                disabledLocomotionProviders.Add(provider);
            }

            var facing = Quaternion.Euler(0f, seat.SitFacingYaw, 0f);

            _seat = seat;
            IsSeated = true;
            _currentSource = null; // set by the ISeatSource wrapper when a source drove this
            seatedSince = Time.time;
            _vrArmed = false; _vrMoveHeldSince = -1f; // each sit starts clean for the stick-stand debounce
            if (body != null) body.SetSeated(true);
            PlayerEvents.RaiseSeated(true);

            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                // Teleport + camera height, INSTANT: lower the root so the user's REAL
                // head (wherever the HMD is in the play space) ends up at the seat's
                // eye height. Never animate the camera in VR — imposed motion is nausea.
                var cameraHeightAboveRoot = Camera.main != null
                    ? Camera.main.transform.position.y - playerRoot.position.y
                    : 1.6f;
                playerRoot.SetPositionAndRotation(
                    new Vector3(seat.SitPosition.x,
                        seat.SitPosition.y + seat.EyeHeightAboveSeat - cameraHeightAboveRoot,
                        seat.SitPosition.z),
                    facing);
                PlayerEvents.RaiseCameraReset();
                if (isDebug) Debug.Log($"{LogPrefix} seated on '{seat.Name}'", this);
            }
            else
            {
                // Seated eyes must end up BELOW the standing eyes — a seat authored
                // too high reads as levitating. Clamp and point at the fix.
                var eyeHeight = seat.EyeHeightAboveSeat;
                var standingHeadY = preSitPosition.y + preSitCameraOffsetY;
                const float minimumDrop = 0.15f;
                if (seat.SitPosition.y + eyeHeight > standingHeadY - minimumDrop)
                {
                    eyeHeight = standingHeadY - minimumDrop - seat.SitPosition.y;
                    Debug.LogWarning($"{LogPrefix} Seat '{seat.Name}': seated eye height would be ABOVE the standing eye " +
                        $"height — clamped to {eyeHeight:F2}m above the sit anchor. Lower the sit anchor or " +
                        "'Eye Height Above Seat' on the Seat (select it to see the height gizmos).", this);
                }

                if (instant)
                {
                    // Scenario placement under a black screen: no glide, arrive seated.
                    playerRoot.SetPositionAndRotation(seat.SitPosition, facing);
                    var offset = cameraOffset.localPosition;
                    offset.y = eyeHeight;
                    cameraOffset.localPosition = offset;
                    if (cameraLook != null) cameraLook.OverrideLook(Vector2.zero);
                    if (isDebug) Debug.Log($"{LogPrefix} seated on '{seat.Name}' (instant)", this);
                    return;
                }

                // M&K/gamepad: glide into the chair (root + camera height together).
                // No RaiseCameraReset here: ResetCameraSettings would restore the
                // STANDING camera offset on top of the seat anchor (seated view
                // higher than standing) and hard-cut the look rotation.
                var seatName = seat.Name; // 'in' params can't be captured by the completion lambda
                StartTransition(seat.SitPosition, facing, eyeHeight,
                    seat.HasHandSupport, seat.HandSupportWorldPos, seat.HandSupportWorldRot,
                    sitTransitionSeconds, () =>
                {
                    if (isDebug) Debug.Log($"{LogPrefix} seated on '{seatName}'", this);
                });
            }
        }

        public void Exit() => Exit(false);

        public void Exit(bool instant)
        {
            if (!IsSeated) return;
            if (_transitioning)
            {
                if (!instant) return;
                CancelTransition();
            }

            var seat = _seat;
            IsSeated = false;
            _currentSource = null;

            Vector3 targetPosition;
            Quaternion targetRotation;
            if (seat.HasExit)
            {
                targetPosition = seat.ExitPosition;
                targetRotation = Quaternion.Euler(0f, seat.ExitFacingYaw, 0f);
            }
            else
            {
                targetPosition = preSitPosition;
                targetRotation = preSitRotation;
            }

            if (body != null) body.SetSeated(false);
            PlayerEvents.RaiseSeated(false);

            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                playerRoot.SetPositionAndRotation(targetPosition, targetRotation);
                var offset = cameraOffset.localPosition;
                offset.y = preSitCameraOffsetY;
                cameraOffset.localPosition = offset;
                FinishExit(seat);
                PlayerEvents.RaiseCameraReset(); // XR only: recenter the HMD view
            }
            else if (instant)
            {
                playerRoot.SetPositionAndRotation(targetPosition, targetRotation);
                var offset = cameraOffset.localPosition;
                offset.y = preSitCameraOffsetY;
                cameraOffset.localPosition = offset;
                if (cameraLook != null) cameraLook.OverrideLook(Vector2.zero);
                if (body != null) body.SetHandSupport(null, 0f);
                FinishExit(seat);
            }
            else
            {
                // Locomotion stays locked and the controller disabled until the
                // glide ends — this also guarantees the jump press that stood us
                // up can never double as a real jump. No camera reset: the glide
                // blends both height and look, a reset would snap them.
                StartTransition(targetPosition, targetRotation, preSitCameraOffsetY,
                    seat.HasHandSupport, seat.HandSupportWorldPos, seat.HandSupportWorldRot,
                    standTransitionSeconds, () => FinishExit(seat));
            }
        }

        private void FinishExit(in SeatData seat)
        {
            if (playerMovement != null)
            {
                playerMovement.LocomotionLocked = false;
                playerMovement.CancelPendingJump(); // the exit press must not also jump
            }
            foreach (var provider in disabledLocomotionProviders)
            {
                if (provider != null) provider.enabled = true;
            }
            disabledLocomotionProviders.Clear();
            controller.enabled = true;

            if (isDebug) Debug.Log($"{LogPrefix} stood up from '{seat.Name}'", this);
        }

        private void StartTransition(Vector3 targetPosition, Quaternion targetRotation, float targetCameraY,
            bool hasHandSupport, Vector3 handSupportPos, Quaternion handSupportRot, float seconds, System.Action onComplete)
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(TransitionRoutine(targetPosition, targetRotation, targetCameraY,
                hasHandSupport, handSupportPos, handSupportRot, seconds, onComplete));
        }

        /// <summary>
        /// Re-express a free-look yaw so that, blended to 0 while the body slerps from
        /// <paramref name="startYaw"/> to <paramref name="seatYaw"/>, the COMBINED view turn takes
        /// the shortest path. The result is the same camera orientation as <paramref name="lookYaw"/>
        /// (equal mod 360), so swapping it in causes no visual pop.
        /// </summary>
        public static float ShortestBlendLookYaw(float startYaw, float seatYaw, float lookYaw)
        {
            var rootDelta = Mathf.DeltaAngle(startYaw, seatYaw);              // body turn (shortest)
            var viewDelta = Mathf.DeltaAngle(startYaw + lookYaw, seatYaw);    // total view turn (shortest)
            return rootDelta - viewDelta;
        }

        private void CancelTransition()
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = null;
            _transitioning = false;
            if (body != null) body.SetHandSupport(null, 0f);
        }

        // One smooth-stepped glide moving the root (position + facing), the
        // camera height AND the accumulated look together. Realism layer, all
        // peaking mid-transition on a sine arc: the view glances DOWN toward
        // the feet, the head dips with the weight shift, and (when the seat
        // has a hand-support anchor) the body's hand reaches the chair back.
        private System.Collections.IEnumerator TransitionRoutine(Vector3 targetPosition, Quaternion targetRotation, float targetCameraY,
            bool hasHandSupport, Vector3 handSupportPos, Quaternion handSupportRot, float seconds, System.Action onComplete)
        {
            _transitioning = true;
            var startPosition = playerRoot.position;
            var startRotation = playerRoot.rotation;
            var startCameraY = cameraOffset.localPosition.y;
            var startLook = cameraLook != null ? cameraLook.LookRotation : Vector2.zero;
            // The view turns to the seat via TWO blends: the body (root) slerps to the seat facing
            // and the free-look yaw unwinds to 0. Free-look yaw accumulates unbounded, and the two
            // can add up to a long way round (a 280° stored look, or body +170° while look unwinds
            // +170° = 340°). Re-express the look yaw so the COMBINED view takes the shortest path.
            if (cameraLook != null)
                startLook.y = ShortestBlendLookYaw(startRotation.eulerAngles.y, targetRotation.eulerAngles.y, startLook.y);
            var duration = Mathf.Max(0.01f, seconds);

            for (var t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                var s = Mathf.SmoothStep(0f, 1f, t);
                var arc = Mathf.Sin(s * Mathf.PI); // 0 -> 1 -> 0 hump

                playerRoot.SetPositionAndRotation(
                    Vector3.Lerp(startPosition, targetPosition, s),
                    Quaternion.Slerp(startRotation, targetRotation, s));

                var offset = cameraOffset.localPosition;
                offset.y = Mathf.Lerp(startCameraY, targetCameraY, s)
                           - weightShiftDip * arc                                        // weight-shift dip
                           - weightShiftDip * 0.25f * Mathf.Sin(s * Mathf.PI * 3f) * arc; // slight bodily wobble
                cameraOffset.localPosition = offset;

                // The look blends to neutral WITH a downward glance at the feet
                // mid-way (+x = pitch down) — the root slerp alone would end with
                // whatever look offset remained, and land with a hard cut.
                if (cameraLook != null)
                {
                    var look = Vector2.Lerp(startLook, Vector2.zero, s);
                    look.x += glanceDownDegrees * arc;
                    cameraLook.OverrideLook(look);
                }

                if (body != null && hasHandSupport) body.SetHandSupport(handSupportPos, handSupportRot, arc);
                yield return null;
            }

            playerRoot.SetPositionAndRotation(targetPosition, targetRotation);
            var finalOffset = cameraOffset.localPosition;
            finalOffset.y = targetCameraY;
            cameraOffset.localPosition = finalOffset;
            if (cameraLook != null) cameraLook.OverrideLook(Vector2.zero);
            if (body != null) body.SetHandSupport(null, 0f);

            _transitioning = false;
            _transition = null;
            onComplete?.Invoke();
        }

        private bool HasRequiredRefs()
        {
            if (playerRoot != null && controller != null && cameraOffset != null) return true;
            if (!missingRefsWarned)
            {
                missingRefsWarned = true;
                Debug.LogWarning($"{LogPrefix} SitController on '{name}': playerRoot, controller or cameraOffset is not " +
                    "assigned — sitting is disabled. Wire them on the Player prefab (or variant).", this);
            }
            return false;
        }
    }
}
