using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    /// <summary>
    /// VR pointing ray (ships on the Player root, zero wiring): while a hand is in the
    /// POINT pose (trigger released — see ControllerHandPoseDriver), a short straight
    /// ray fades out of the fingertip — but ONLY while it is actually aimed at
    /// something usable (an XRI interactable, seat, pickable, tooltip, or a project
    /// physicsHoverMask layer). Sweeping the hand across an interactable makes the ray
    /// appear with a haptic tick, exactly like the desktop reticle lighting up on hover.
    ///
    /// Colours come from the same single authority as the desktop cursor
    /// (CursorStateController): the ray shows HoverColor while hovering and ClickColor
    /// while the Interact button is held on a usable target, so VR and desktop feel
    /// cohesive. Pressing Interact (A / X) performs the action along this ray:
    /// PerformAction raycasts along it in XR.
    /// </summary>
    public class FingerPointingRay : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [SerializeField] private float rayLength = 2.5f;
        [SerializeField] private float rayWidth = 0.006f;
        [Tooltip("Fallback hover colour if no CursorStateController is present (the cursor's HoverColor overrides this).")]
        [SerializeField] private Color hoverColorFallback = new Color(0.35f, 0.95f, 0.6f, 0.9f);
        [Tooltip("Fallback click colour if no CursorStateController is present (the cursor's ClickColor overrides this).")]
        [SerializeField] private Color clickColorFallback = new Color(1f, 0.85f, 0.25f, 0.95f);
        [Tooltip("Project raycast-interactable layers (same mask as the desktop reticle / click handler). Empty = only XRI interactables, seats, pickables and tooltips show the ray.")]
        [SerializeField] private LayerMask physicsHoverMask;

        [Header("Haptics (hover tick)")]
        [Range(0f, 1f)][SerializeField] private float hoverHapticAmplitude = 0.25f;
        [Range(0f, 0.3f)][SerializeField] private float hoverHapticDuration = 0.06f;

        private sealed class HandRay
        {
            public HandPoseManager Manager;
            public Transform Origin;
            public LineRenderer Line;
            public bool Pointing;
            public bool WasUsable; // for the hover-enter haptic edge
        }

        private readonly List<HandRay> hands = new List<HandRay>();
        private ControllerHandPoseDriver poseDriver;
        private CursorStateController cursorColors; // shared colour authority (hover/click)
        private InputAction interactAction;
        private bool interactHeld;
        private Material lineMaterial;
        private float nextHandScan;

        private static FingerPointingRay activeInstance;

        /// <summary>The ray of the first hand currently pointing (PerformAction's XR origin).</summary>
        public static bool TryGetPointingRay(out Ray ray)
        {
            ray = default;
            if (activeInstance == null) return false;
            foreach (var hand in activeInstance.hands)
            {
                if (!hand.Pointing || hand.Origin == null) continue;
                ray = new Ray(hand.Origin.position, hand.Origin.forward);
                return true;
            }
            return false;
        }

        private void OnEnable()
        {
            activeInstance = this;
            poseDriver = GetComponentInChildren<ControllerHandPoseDriver>(true);
            if (poseDriver == null)
            {
                Debug.LogWarning($"{LogPrefix} FingerPointingRay on '{name}': no ControllerHandPoseDriver found — " +
                    "without it there is no POINT pose state, so the pointing ray never shows.", this);
            }

            cursorColors = GetComponentInParent<CursorStateController>()
                           ?? FindFirstObjectByType<CursorStateController>();

            // Same interact binding as the desktop reticle click-flash, resolved by name
            // so no wiring is needed; ClickColor is shown while it is held on a usable target.
            var playerInput = GetComponentInParent<PlayerInput>() ?? GetComponentInChildren<PlayerInput>(true);
            interactAction = playerInput != null && playerInput.actions != null
                ? playerInput.actions.FindAction("FPS/Interact", throwIfNotFound: false)
                : null;
            if (interactAction != null)
            {
                interactAction.performed += OnInteractPerformed;
                interactAction.canceled += OnInteractCanceled;
            }
        }

        private void OnDisable()
        {
            if (activeInstance == this) activeInstance = null;
            if (interactAction != null)
            {
                interactAction.performed -= OnInteractPerformed;
                interactAction.canceled -= OnInteractCanceled;
            }
            interactHeld = false;
            foreach (var hand in hands)
            {
                if (hand.Line != null) Destroy(hand.Line.gameObject);
            }
            hands.Clear();
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        private void OnInteractPerformed(InputAction.CallbackContext _) => interactHeld = true;
        private void OnInteractCanceled(InputAction.CallbackContext _) => interactHeld = false;

        private void Update()
        {
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR)
            {
                HideAll();
                return;
            }

            if (hands.Count == 0 && Time.unscaledTime >= nextHandScan) RefreshHands();

            foreach (var hand in hands)
            {
                if (hand.Manager == null) continue;
                hand.Pointing = poseDriver != null
                    && hand.Manager.gameObject.activeInHierarchy
                    && poseDriver.StateOf(hand.Manager.HandType) == ControllerHandPoseDriver.PoseState.Point;
                UpdateRay(hand);
            }
        }

        private void HideAll()
        {
            foreach (var hand in hands)
            {
                hand.Pointing = false;
                hand.WasUsable = false;
                if (hand.Line != null) hand.Line.enabled = false;
            }
        }

        /// <summary>Re-discovers the hands (they spawn at runtime); also a test seam.</summary>
        public void RefreshHands()
        {
            nextHandScan = Time.unscaledTime + 1f;
            foreach (var hand in hands)
            {
                if (hand.Line != null) Destroy(hand.Line.gameObject);
            }
            hands.Clear();
            foreach (var manager in GetComponentsInChildren<HandPoseManager>(true))
            {
                var origin = manager.targetInteractor != null && manager.targetInteractor.attachTransform != null
                    ? manager.targetInteractor.attachTransform
                    : manager.transform;
                hands.Add(new HandRay { Manager = manager, Origin = origin, Line = BuildLine(manager.HandType) });
            }
        }

        private LineRenderer BuildLine(HandType handType)
        {
            if (lineMaterial == null)
            {
                // Vertex-colored unlit: renders the fade in URP and HDRP alike.
                lineMaterial = PipelineMaterialGuard.VertexColorUnlit();
            }
            var go = new GameObject($"PointingRay_{handType}");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.material = lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = rayWidth;
            line.endWidth = rayWidth * 0.4f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            return line;
        }

        private void UpdateRay(HandRay hand)
        {
            if (hand.Line == null) return;

            var usable = false;
            var origin = Vector3.zero;
            var direction = Vector3.forward;
            var distance = rayLength;

            if (hand.Pointing)
            {
                origin = hand.Origin.position;
                direction = hand.Origin.forward;
                if (Physics.Raycast(origin, direction, out var hit, rayLength))
                {
                    distance = hit.distance;
                    usable = (physicsHoverMask.value & 1 << hit.collider.gameObject.layer) != 0
                             || hit.collider.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable>() != null
                             || hit.collider.GetComponentInParent<Seat>() != null
                             || hit.collider.GetComponentInParent<PickableObject>() != null
                             || hit.collider.GetComponentInParent<IReticleHoverable>() != null;
                }
            }

            // Only visible while aimed at something usable (the requested behavior); the
            // rising edge onto a usable target fires the hover tick — like the reticle lighting up.
            if (usable && !hand.WasUsable) PulseHover(hand.Manager.HandType);
            hand.WasUsable = usable;

            hand.Line.enabled = usable;
            if (!usable) return;

            var color = interactHeld ? ClickColor() : HoverColor();
            hand.Line.startColor = color;
            hand.Line.endColor = new Color(color.r, color.g, color.b, 0f); // fades out with distance
            hand.Line.SetPosition(0, origin);
            hand.Line.SetPosition(1, origin + direction * distance);
        }

        private Color HoverColor() => cursorColors != null ? cursorColors.HoverColor : hoverColorFallback;
        private Color ClickColor() => cursorColors != null ? cursorColors.ClickColor : clickColorFallback;

        private void PulseHover(HandType handType)
        {
            if (hoverHapticAmplitude <= 0f || hoverHapticDuration <= 0f) return;
            var hand = handType == HandType.Left ? "Left" : "Right";
            HandVibration.VibrateHand?.Invoke(hand, hoverHapticAmplitude, hoverHapticDuration);
        }
    }
}
