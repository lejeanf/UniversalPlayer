using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Drop this on a chair and the player can sit on it — no other wiring needed.
    ///
    /// M&amp;K / gamepad: the SitController on the Player raycasts on the FPS/Interact
    /// action; aiming at anything with a Seat in its parents sits you down (and the
    /// same input, or moving, stands you back up).
    /// VR: an XRSimpleInteractable is added and wired to <see cref="ToggleSit"/>
    /// automatically at startup (needs a collider on the chair). If you add your own
    /// interactable, wire its Select to ToggleSit — the auto-wiring detects that and
    /// stays out of the way.
    /// </summary>
    public class Seat : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("Where the hips go (position) and which way the player faces (forward). Defaults to this transform.")]
        [SerializeField] private Transform sitAnchor;
        [Tooltip("Optional: where the player stands when exiting. When empty, they return to where they sat down from.")]
        [SerializeField] private Transform exitAnchor;
        [Tooltip("Eye height above the sit anchor while seated.")]
        [SerializeField] private float eyeHeightAboveSeat = 0.7f;
        [Tooltip("Optional: where a hand rests (chair back / armrest) while sitting down or standing up — the body reaches for it with IK in M&K/gamepad.")]
        [SerializeField] private Transform handSupportAnchor;
        [Tooltip("Add (or reuse) an XRSimpleInteractable at startup and wire its Select to ToggleSit, so the chair works in VR with zero extra setup.")]
        [SerializeField] private bool autoConfigureXrInteractable = true;

        public Transform SitAnchor => sitAnchor != null ? sitAnchor : transform;
        public Transform ExitAnchor => exitAnchor;
        public float EyeHeightAboveSeat => eyeHeightAboveSeat;
        public Transform HandSupportAnchor => handSupportAnchor;

        private XRBaseInteractable interactable;
        private bool listeningOnInteractable;

        private void Awake()
        {
            if (!autoConfigureXrInteractable) return;

            interactable = GetComponent<XRBaseInteractable>();
            if (interactable == null)
            {
                if (GetComponentInChildren<Collider>() == null)
                {
                    Debug.LogWarning($"{LogPrefix} Seat '{name}': no collider on the chair — the player cannot aim at it " +
                        "in desktop mode and no VR interactable can be set up. Add a collider (or disable " +
                        "autoConfigureXrInteractable if this is intentional).", this);
                    return;
                }
                interactable = gameObject.AddComponent<XRSimpleInteractable>();
            }
        }

        private void OnEnable()
        {
            if (interactable == null || AlreadyWiredManually()) return;
            interactable.selectEntered.AddListener(OnSelectEntered);
            listeningOnInteractable = true;
        }

        private void OnDisable()
        {
            if (!listeningOnInteractable) return;
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            listeningOnInteractable = false;
        }

        /// <summary>True when the user wired ToggleSit into the interactable's inspector events themselves.</summary>
        private bool AlreadyWiredManually()
        {
            for (var i = 0; i < interactable.selectEntered.GetPersistentEventCount(); i++)
            {
                if (interactable.selectEntered.GetPersistentMethodName(i) == nameof(ToggleSit)) return true;
            }
            return false;
        }

        private void OnSelectEntered(SelectEnterEventArgs _) => ToggleSit();

        /// <summary>Sit on / stand up from this seat. Safe to wire to UnityEvents (XRI Select, buttons, ...).</summary>
        public void ToggleSit()
        {
            if (SitController.Instance == null)
            {
                Debug.LogWarning($"{LogPrefix} Seat '{name}': ToggleSit was called but there is no active SitController " +
                    "in the scene. The Player prefab ships one on its Move object — is the player missing, disabled, " +
                    "or is your variant built from an older prefab?", this);
                return;
            }
            SitController.Instance.ToggleSit(this);
        }

#if UNITY_EDITOR
        private static SitController _gizmoSitController;
        private static double _gizmoSitControllerLookupTime;

        /// <summary>Where the eyes end up while seated (world space).</summary>
        public Vector3 SeatedEyePosition => SitAnchor.position + Vector3.up * eyeHeightAboveSeat;

        /// <summary>Best edit-time estimate of the standing eye position after exiting (world space).</summary>
        public Vector3 EstimatedStandingEyePosition
        {
            get
            {
                // Cheap cached lookup — gizmos draw every repaint.
                if (UnityEditor.EditorApplication.timeSinceStartup > _gizmoSitControllerLookupTime + 5d || _gizmoSitController == null)
                {
                    _gizmoSitController = FindFirstObjectByType<SitController>(FindObjectsInactive.Include);
                    _gizmoSitControllerLookupTime = UnityEditor.EditorApplication.timeSinceStartup;
                }
                var standingHeight = _gizmoSitController != null ? _gizmoSitController.StandingCameraHeight : 1.7f;
                var ground = exitAnchor != null ? exitAnchor.position : SitAnchor.position;
                return ground + Vector3.up * standingHeight;
            }
        }

        // Height feedback: seated eyes (sphere over the anchor) vs standing eyes
        // (sphere over the exit spot). Seated at-or-above standing = ORANGE-RED,
        // because sitting must LOWER the view.
        private void OnDrawGizmosSelected()
        {
            var anchor = SitAnchor;
            var seatedEye = SeatedEyePosition;
            var standingEye = EstimatedStandingEyePosition;
            var seatedTooHigh = seatedEye.y >= standingEye.y - 0.05f;

            // Sit anchor + facing
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(anchor.position, new Vector3(0.3f, 0.02f, 0.3f));
            Gizmos.DrawRay(anchor.position, Quaternion.Euler(0f, anchor.eulerAngles.y, 0f) * Vector3.forward * 0.45f);

            // Seated eyes
            Gizmos.color = seatedTooHigh ? new Color(1f, 0.45f, 0.1f) : Color.cyan;
            Gizmos.DrawLine(anchor.position, seatedEye);
            Gizmos.DrawWireSphere(seatedEye, 0.08f);
            UnityEditor.Handles.Label(seatedEye + Vector3.up * 0.14f,
                seatedTooHigh ? $"Seated eyes {seatedEye.y:F2}m — TOO HIGH (must be below standing)" : $"Seated eyes {seatedEye.y:F2}m");

            // Standing eyes at the exit spot
            var ground = exitAnchor != null ? exitAnchor.position : anchor.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(ground, standingEye);
            Gizmos.DrawWireSphere(standingEye, 0.08f);
            UnityEditor.Handles.Label(standingEye + Vector3.up * 0.14f,
                exitAnchor != null ? $"Standing eyes {standingEye.y:F2}m (exit anchor)" : $"Standing eyes ~{standingEye.y:F2}m (no exit anchor: estimated at the seat)");

            // Hand support (chair back / armrest)
            if (handSupportAnchor != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(handSupportAnchor.position, 0.05f);
                UnityEditor.Handles.Label(handSupportAnchor.position + Vector3.up * 0.1f, "Hand support");
            }
        }
#endif
    }
}
