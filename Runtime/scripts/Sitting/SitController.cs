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

        public Seat CurrentSeat { get; private set; }
        public bool IsSeated => CurrentSeat != null;

        private InputAction interactAction;
        private InputAction jumpAction;
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

            var seat = seatObject.GetComponentInParent<Seat>();
            if (seat == null)
            {
                Debug.LogWarning($"{LogPrefix} SitController: a sit was requested on '{seatObject.name}' but there is no " +
                    "Seat component on it (or its parents) — add a Seat and set its sit anchor.", seatObject);
                return;
            }

            if (CurrentSeat == seat) return;
            if (IsSeated) Exit(true); // silent swap: release the previous seat instantly
            SitOn(seat, instant);
        }

        private void OnJumpWhileSeated(InputAction.CallbackContext _)
        {
            if (!exitOnJump || !IsSeated || _transitioning) return;
            if (Time.time < seatedSince + exitGraceSeconds) return;
            Exit();
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

            var seat = hit.collider.GetComponentInParent<Seat>();
            if (seat != null) SitOn(seat);
        }

        private void LateUpdate()
        {
            if (!IsSeated || !exitOnMoveInput || _transitioning) return;
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR) return;
            if (Time.time < seatedSince + exitGraceSeconds) return;
            if (playerMovement != null && playerMovement.MoveInput.sqrMagnitude > 0.25f) Exit();
        }

        public void ToggleSit(Seat seat)
        {
            if (IsSeated) Exit();
            else SitOn(seat);
        }

        public void SitOn(Seat seat) => SitOn(seat, false);

        public void SitOn(Seat seat, bool instant)
        {
            if (seat == null || IsSeated) return;
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

            var anchor = seat.SitAnchor;
            var facing = Quaternion.Euler(0f, anchor.eulerAngles.y, 0f);

            CurrentSeat = seat;
            seatedSince = Time.time;
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
                    new Vector3(anchor.position.x,
                        anchor.position.y + seat.EyeHeightAboveSeat - cameraHeightAboveRoot,
                        anchor.position.z),
                    facing);
                PlayerEvents.RaiseCameraReset();
                if (isDebug) Debug.Log($"{LogPrefix} seated on '{seat.name}'", this);
            }
            else
            {
                // Seated eyes must end up BELOW the standing eyes — a seat authored
                // too high reads as levitating. Clamp and point at the fix.
                var eyeHeight = seat.EyeHeightAboveSeat;
                var standingHeadY = preSitPosition.y + preSitCameraOffsetY;
                const float minimumDrop = 0.15f;
                if (anchor.position.y + eyeHeight > standingHeadY - minimumDrop)
                {
                    eyeHeight = standingHeadY - minimumDrop - anchor.position.y;
                    Debug.LogWarning($"{LogPrefix} Seat '{seat.name}': seated eye height would be ABOVE the standing eye " +
                        $"height — clamped to {eyeHeight:F2}m above the sit anchor. Lower the sit anchor or " +
                        "'Eye Height Above Seat' on the Seat (select it to see the height gizmos).", seat);
                }

                if (instant)
                {
                    // Scenario placement under a black screen: no glide, arrive seated.
                    playerRoot.SetPositionAndRotation(anchor.position, facing);
                    var offset = cameraOffset.localPosition;
                    offset.y = eyeHeight;
                    cameraOffset.localPosition = offset;
                    if (cameraLook != null) cameraLook.OverrideLook(Vector2.zero);
                    if (isDebug) Debug.Log($"{LogPrefix} seated on '{seat.name}' (instant)", this);
                    return;
                }

                // M&K/gamepad: glide into the chair (root + camera height together).
                // No RaiseCameraReset here: ResetCameraSettings would restore the
                // STANDING camera offset on top of the seat anchor (seated view
                // higher than standing) and hard-cut the look rotation.
                StartTransition(anchor.position, facing, eyeHeight, seat.HandSupportAnchor, sitTransitionSeconds, () =>
                {
                    if (isDebug) Debug.Log($"{LogPrefix} seated on '{seat.name}'", this);
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

            var seat = CurrentSeat;
            CurrentSeat = null;

            Vector3 targetPosition;
            Quaternion targetRotation;
            if (seat.ExitAnchor != null)
            {
                targetPosition = seat.ExitAnchor.position;
                targetRotation = Quaternion.Euler(0f, seat.ExitAnchor.eulerAngles.y, 0f);
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
                StartTransition(targetPosition, targetRotation, preSitCameraOffsetY, seat.HandSupportAnchor, standTransitionSeconds, () => FinishExit(seat));
            }
        }

        private void FinishExit(Seat seat)
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

            if (isDebug) Debug.Log($"{LogPrefix} stood up from '{seat.name}'", this);
        }

        private void StartTransition(Vector3 targetPosition, Quaternion targetRotation, float targetCameraY, Transform handSupport, float seconds, System.Action onComplete)
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = StartCoroutine(TransitionRoutine(targetPosition, targetRotation, targetCameraY, handSupport, seconds, onComplete));
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
        private System.Collections.IEnumerator TransitionRoutine(Vector3 targetPosition, Quaternion targetRotation, float targetCameraY, Transform handSupport, float seconds, System.Action onComplete)
        {
            _transitioning = true;
            var startPosition = playerRoot.position;
            var startRotation = playerRoot.rotation;
            var startCameraY = cameraOffset.localPosition.y;
            var startLook = cameraLook != null ? cameraLook.LookRotation : Vector2.zero;
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

                if (body != null && handSupport != null) body.SetHandSupport(handSupport, arc);
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
