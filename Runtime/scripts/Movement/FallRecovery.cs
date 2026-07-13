using jeanf.EventSystem;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Failsafe against infinite falls (ships wired on the Player prefab, all modes
    /// including VR). Remembers the last position where the player stood on the ground;
    /// when the player descends without landing for longer than <see cref="maxFallSeconds"/>
    /// or deeper than <see cref="maxFallMeters"/>, they are teleported back there and a
    /// loud console warning explains the likely causes (hole in the level, missing floor
    /// collider, or two gravity systems fighting — see ValidateSetup).
    /// FreeCam flight and being seated never trigger it.
    /// </summary>
    public class FallRecovery : MonoBehaviour, IDebugBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        [SerializeField] private CharacterController controller;
        [Tooltip("The transform that gets teleported back (the Player root).")]
        [SerializeField] private Transform playerRoot;
        [SerializeField] private PlayerMovement playerMovement;

        [Header("Trigger")]
        [Tooltip("Continuous descent longer than this triggers recovery.")]
        [SerializeField] private float maxFallSeconds = 3f;
        [Tooltip("Continuous descent deeper than this (meters) triggers recovery.")]
        [SerializeField] private float maxFallMeters = 40f;

        public enum RecoveryTarget
        {
            LastGroundedPosition,
            WorldOrigin,
            OverrideTransform,
        }

        [Header("Recovery")]
        [Tooltip("Where a runaway fall teleports the player. WorldOrigin (0,0,0) suits worlds whose guaranteed-safe spawn is the origin; LastGroundedPosition can be stale when additive streaming unloads the floor it was recorded on.")]
        [SerializeField] private RecoveryTarget recoveryTarget = RecoveryTarget.WorldOrigin;
        [Tooltip("Used when Recovery Target is OverrideTransform.")]
        [SerializeField] private Transform safePositionOverride;

        // Recovery messages go through PlayerEvents (fallRecoveryMessage on the bridge).

        private Vector3 lastSafePosition;
        private Quaternion lastSafeRotation;
        private bool hasSafePosition;
        private float lastSaveTime;
        private float fallTimer;
        private float fallStartY;
        private bool descending;
        private float previousY;
        private int recoveryCount;
        private bool missingRefsWarned;

        /// <summary>How many times recovery has fired this session (grows loud when it keeps happening).</summary>
        public int RecoveryCount => recoveryCount;

        private void Start()
        {
            if (playerRoot != null)
            {
                // Best effort until the first real grounding: the spawn position.
                lastSafePosition = playerRoot.position;
                lastSafeRotation = playerRoot.rotation;
                hasSafePosition = true;
                previousY = playerRoot.position.y;
            }
        }

        private void LateUpdate()
        {
            if (playerRoot == null || controller == null)
            {
                if (!missingRefsWarned)
                {
                    missingRefsWarned = true;
                    Debug.LogWarning($"{LogPrefix} FallRecovery on '{name}': playerRoot or controller is not assigned — " +
                        "the infinite-fall failsafe is disabled. Wire them on the Player prefab (or variant).", this);
                }
                return;
            }

            // Deliberate vertical freedom: flying down in FreeCam or sitting (controller
            // disabled) must never look like a fall.
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.Freecam
                || !controller.enabled
                || (playerMovement != null && playerMovement.LocomotionLocked))
            {
                ResetFallTracking();
                return;
            }

            var y = playerRoot.position.y;

            if (controller.isGrounded)
            {
                ResetFallTracking();
                if (Time.time - lastSaveTime > 0.5f)
                {
                    lastSafePosition = playerRoot.position;
                    lastSafeRotation = playerRoot.rotation;
                    hasSafePosition = true;
                    lastSaveTime = Time.time;
                }
            }
            else if (y < previousY - 0.001f)
            {
                if (!descending)
                {
                    descending = true;
                    fallStartY = previousY;
                    fallTimer = 0f;
                }
                fallTimer += Time.deltaTime;

                if (fallTimer > maxFallSeconds || fallStartY - y > maxFallMeters)
                {
                    Recover(fallTimer, fallStartY - y);
                }
            }
            else
            {
                // Flat or rising (jump apex, elevators): not a runaway fall.
                ResetFallTracking();
            }

            previousY = playerRoot.position.y;
        }

        private void ResetFallTracking()
        {
            descending = false;
            fallTimer = 0f;
        }

        /// <summary>Teleport back to safety immediately (also callable from gameplay/UI).</summary>
        public void Recover() => Recover(fallTimer, descending ? fallStartY - playerRoot.position.y : 0f);

        private void Recover(float seconds, float meters)
        {
            recoveryCount++;
            Vector3 target;
            Quaternion rotation;
            string targetDescription;
            switch (recoveryTarget)
            {
                case RecoveryTarget.OverrideTransform when safePositionOverride != null:
                    target = safePositionOverride.position;
                    rotation = safePositionOverride.rotation;
                    targetDescription = $"'{safePositionOverride.name}'";
                    break;
                case RecoveryTarget.OverrideTransform: // configured but not assigned — be loud, fall back to origin
                    Debug.LogWarning($"{LogPrefix} FallRecovery on '{name}': recoveryTarget is OverrideTransform but " +
                        "safePositionOverride is not assigned — recovering to the world origin instead.", this);
                    target = Vector3.zero;
                    rotation = playerRoot.rotation;
                    targetDescription = "the world origin (override missing)";
                    break;
                case RecoveryTarget.WorldOrigin:
                    target = Vector3.zero;
                    rotation = playerRoot.rotation;
                    targetDescription = "the world origin";
                    break;
                default:
                    target = hasSafePosition ? lastSafePosition : Vector3.zero;
                    rotation = hasSafePosition ? lastSafeRotation : playerRoot.rotation;
                    targetDescription = hasSafePosition ? "the last grounded position" : "the world origin (never grounded yet)";
                    break;
            }

            var wasEnabled = controller.enabled;
            controller.enabled = false;
            playerRoot.SetPositionAndRotation(target + Vector3.up * 0.1f, rotation);
            controller.enabled = wasEnabled;
            ResetFallTracking();
            previousY = playerRoot.position.y;
            // Drop the accumulated fall velocity and re-arm the ground check: without this
            // the next fall resumed at terminal speed, and a safe spot whose floor got
            // unloaded (additive scene streaming) meant an endless recover-fall loop
            // instead of hovering in place until ground exists again.
            if (playerMovement != null) playerMovement.OnExternalTeleport();

            var message = $"Player fell for {seconds:F1}s / {meters:F0}m without landing — teleported back to " +
                $"{targetDescription} {target}.";
            Debug.LogWarning($"{LogPrefix} FallRecovery: {message}" +
                (recoveryCount > 1
                    ? $" This is recovery #{recoveryCount} — the player keeps falling. Likely causes: no collider under " +
                      "the spawn/safe position, or two gravity systems enabled (run Tools/UniversalPlayer/ValidateSetup, " +
                      "'single gravity system' check)."
                    : " If this was not a level design hole, check for a missing floor collider under the player."), this);
            PlayerEvents.RaiseFallRecovered(message);
        }
    }
}
