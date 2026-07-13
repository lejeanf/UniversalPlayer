using jeanf.EventSystem;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Head bob, turn/strafe camera roll and landing dip for the keyboard&amp;mouse / gamepad modes.
    /// Lives on a dedicated "CameraFeel" transform between the CameraOffset and the camera so it
    /// never fights FPSCameraMovement (which rotates the CameraOffset) or the XR TrackedPoseDriver
    /// (which drives the camera itself). Fully inert in XR — no artificial camera motion in VR.
    /// </summary>
    public class FpsCameraFeel : MonoBehaviour, IDebugBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        [Tooltip("Source of speed / grounded / crouch state. Required.")]
        [SerializeField] private PlayerMovement playerMovement;
        [Tooltip("The transform the yaw lives on (the CameraOffset). Used to measure turn rate for the roll.")]
        [SerializeField] private Transform lookTransform;

        [Header("Head bob")]
        [SerializeField] private bool headBobEnabled = true;
        [Tooltip("Bob cycles per meter walked. Vertical bob runs at twice this rate (one dip per step).")]
        [SerializeField] private float bobCyclesPerMeter = 0.45f;
        // Deliberately subdued: the bob should be felt, not seen (user preference).
        [SerializeField] private float bobHeight = 0.012f;
        [SerializeField] private float bobSway = 0.005f;
        [Tooltip("Multiplier applied to the bob amplitude while crouched.")]
        [Range(0f, 1f)][SerializeField] private float crouchBobFactor = 0.5f;

        [Header("Camera roll (turn tilt)")]
        [SerializeField] private bool rollEnabled = true;
        [Tooltip("Degrees of roll per degree/second of turn rate. Keep subtle — fast mouse flicks reach very high turn rates.")]
        [SerializeField] private float rollFromTurn = 0.004f;
        [Tooltip("Degrees of roll at full strafe input.")]
        [SerializeField] private float rollFromStrafe = 0.8f;
        [SerializeField] private float maxRoll = 1.5f;
        [Tooltip("How fast the roll follows its target (1/seconds). Higher = stiffer.")]
        [SerializeField] private float rollResponse = 8f;

        [Header("Landing dip")]
        [SerializeField] private bool landingDipEnabled = true;
        [Tooltip("How deep the camera dips (meters) after a long fall.")]
        [SerializeField] private float dipDepth = 0.06f;
        [Tooltip("Minimum airborne time (seconds) before landing triggers a dip.")]
        [SerializeField] private float minAirTime = 0.25f;
        [SerializeField] private float dipSpring = 90f;
        [SerializeField] private float dipDamping = 12f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private float bobPhase;
        private float bobAmplitudeBlend; // eases bob in/out so stopping doesn't pop
        private float currentRoll;
        private float previousYaw;
        private bool hasPreviousYaw;
        private float airTime;
        private bool wasGrounded = true;
        private float dipOffset;
        private float dipVelocity;
        private bool missingRefsWarned;
        private bool menuOpen;
        private bool paused;

        /// <summary>Current position offset applied on top of the base local position (world-agnostic, local meters).</summary>
        public Vector3 CurrentOffset { get; private set; }
        /// <summary>Current roll in degrees (z rotation applied on top of the base local rotation).</summary>
        public float CurrentRoll => currentRoll;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
        }

        private void OnEnable()
        {
            PlayerEvents.MenuStateChanged += OnMenuStateChanged;
            PlayerEvents.PauseRequested += OnPauseRequested;
        }

        private void OnMenuStateChanged(bool isOpen) => menuOpen = isOpen;
        private void OnPauseRequested(bool isPaused) => paused = isPaused;

        private void OnDisable()
        {
            PlayerEvents.MenuStateChanged -= OnMenuStateChanged;
            PlayerEvents.PauseRequested -= OnPauseRequested;
            // Never leave a half-applied bob/roll behind (e.g. when switching to XR disables us).
            transform.localPosition = baseLocalPosition;
            transform.localRotation = baseLocalRotation;
            CurrentOffset = Vector3.zero;
            currentRoll = 0f;
            bobAmplitudeBlend = 0f;
            dipOffset = 0f;
            dipVelocity = 0f;
            hasPreviousYaw = false;
        }

        private void LateUpdate()
        {
            if (playerMovement == null)
            {
                if (!missingRefsWarned)
                {
                    missingRefsWarned = true;
                    Debug.LogWarning($"{LogPrefix} FpsCameraFeel on '{name}': playerMovement is not assigned — " +
                        "head bob, camera roll and landing dip are disabled. Wire the PlayerMovement component " +
                        "from the Player prefab (or variant).", this);
                }
                return;
            }

            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            var inXr = BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR;
            // Menu open (or game paused): every artificial camera motion eases
            // back to neutral — the frozen frame behind the menu must not bob,
            // tilt or dip. Same gating as XR, where the head drives the camera.
            var effectsOff = inXr || menuOpen || paused;
            if (effectsOff) hasPreviousYaw = false; // no stale-yaw roll spike on resume

            var targetRoll = 0f;
            if (!effectsOff && rollEnabled) targetRoll = ComputeTargetRoll(dt);
            currentRoll = Mathf.Lerp(currentRoll, targetRoll, 1f - Mathf.Exp(-rollResponse * dt));

            UpdateBob(effectsOff, dt);
            UpdateLandingDip(effectsOff, dt);

            CurrentOffset = ComputeBobOffset() + Vector3.up * dipOffset;
            transform.localPosition = baseLocalPosition + CurrentOffset;
            transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, 0f, currentRoll);
        }

        private float ComputeTargetRoll(float dt)
        {
            var roll = -playerMovement.MoveInput.x * rollFromStrafe;

            if (lookTransform != null)
            {
                var yaw = lookTransform.localEulerAngles.y;
                if (hasPreviousYaw)
                {
                    var yawRate = Mathf.DeltaAngle(previousYaw, yaw) / dt;
                    roll += -yawRate * rollFromTurn;
                }
                previousYaw = yaw;
                hasPreviousYaw = true;
            }

            return Mathf.Clamp(roll, -maxRoll, maxRoll);
        }

        private void UpdateBob(bool effectsOff, float dt)
        {
            // Actual (achieved) velocity: no head bob while pushing against a wall.
            var speed = playerMovement.ActualPlanarVelocity.magnitude;
            var bobActive = !effectsOff && headBobEnabled && playerMovement.IsGrounded && speed > 0.1f;

            bobAmplitudeBlend = Mathf.MoveTowards(bobAmplitudeBlend, bobActive ? 1f : 0f, dt * 6f);
            if (bobActive)
            {
                bobPhase += speed * bobCyclesPerMeter * dt * Mathf.PI * 2f;
            }
            else if (bobAmplitudeBlend <= 0f)
            {
                bobPhase = 0f;
            }
        }

        private Vector3 ComputeBobOffset()
        {
            if (bobAmplitudeBlend <= 0f) return Vector3.zero;

            var amplitude = bobAmplitudeBlend * Mathf.Lerp(1f, crouchBobFactor, playerMovement.CrouchBlend);
            // Speed scales the amplitude a little so sprinting feels heavier than walking.
            amplitude *= Mathf.Lerp(0.7f, 1.3f, playerMovement.NormalizedSpeed);

            return new Vector3(
                Mathf.Sin(bobPhase) * bobSway * amplitude,
                Mathf.Sin(bobPhase * 2f) * bobHeight * amplitude,
                0f);
        }

        private void UpdateLandingDip(bool effectsOff, float dt)
        {
            var grounded = playerMovement.IsGrounded;
            if (!grounded)
            {
                airTime += dt;
            }
            else
            {
                if (!wasGrounded && !effectsOff && landingDipEnabled && airTime >= minAirTime)
                {
                    // Impulse scaled by how long the fall lasted, capped at one second worth.
                    dipVelocity = -dipDepth * dipSpring * 0.15f * Mathf.Clamp01(airTime);
                    if (isDebug) Debug.Log($"{LogPrefix} landing dip after {airTime:F2}s in the air", this);
                }
                airTime = 0f;
            }
            wasGrounded = grounded;

            dipVelocity += (-dipOffset * dipSpring - dipVelocity * dipDamping) * dt;
            dipOffset += dipVelocity * dt;
        }
    }
}
