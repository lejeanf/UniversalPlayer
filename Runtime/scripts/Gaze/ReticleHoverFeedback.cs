using System.Collections.Generic;
using jeanf.validationTools;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Desktop counterpart of the VR grab preview: the center reticle tints while the
    /// player is looking at something usable, and flashes when they interact.
    /// Four hover sources, any of which tints:
    ///   1. XRI interactor hover (grabbables, chairs, switches)
    ///   2. the gaze ray's UI raycast (world-space UI elements, camera-forward)
    ///   3. the DesktopWorldUiInteractor's hover (world-space UI, follows the
    ///      reticle even when the cursor is free in tablet/menu mode)
    ///   4. a plain physics raycast against <see cref="physicsHoverMask"/> — the same
    ///      camera-forward pattern project click handlers use (e.g. uvs's
    ///      UIClickHandler → Highlight_Interactionable). Set the SAME LayerMask on the
    ///      Player variant as the project's click handler uses; layers are
    ///      project-specific so the packaged default is empty (source disabled).
    /// The validation fill (validationFeedbackImage) is a separate image and stays
    /// untouched. M&K and gamepad modes only.
    /// </summary>
    public class ReticleHoverFeedback : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("The reticle SVG image (the same one CursorStateController drives).")]
        [Validation("Reticle image is required — it is what tints on hover. Wire the Cursor's SVGImage (same one CursorStateController uses).")]
        [SerializeField] private SVGImage reticleImage;
        [Tooltip("Fallback only — the CursorStateController's Hover Color overrides this when a manager is present (it always is on the shipped prefab).")]
        [SerializeField] private Color hoverColor = new Color(0.35f, 0.95f, 0.6f);
        [Tooltip("Fallback only — the CursorStateController's Click Color overrides this. Shown while Interact/TakeObject is held on something usable.")]
        [SerializeField] private Color interactFlashColor = new Color(1f, 0.85f, 0.25f);

        [Header("Physics hover (project click-handler parity)")]
        [Tooltip("Layers holding the project's raycast interactables (uvs: the layers UIClickHandler raycasts). Empty = this hover source is off.")]
        [SerializeField] private LayerMask physicsHoverMask;
        [SerializeField] private float physicsHoverDistance = 50f;
        [Tooltip("How far the reticle looks for XRI interactables/seats/pickables (a gaze interactor only hovers interactables that opt into allowGazeInteraction, so the reticle raycasts for them instead).")]
        [SerializeField] private float interactableHoverDistance = 3.5f;

        private readonly HashSet<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRHoverInteractable> hovered =
            new HashSet<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRHoverInteractable>();
        private readonly List<XRBaseInteractor> subscribedInteractors = new List<XRBaseInteractor>();
        private XRRayInteractor gazeRay;
        private Camera playerCamera;
        private InputAction interactAction;
        private InputAction takeAction;
        private bool interactHeld;
        private Color defaultColor;
        private bool defaultCaptured;
        private CursorStateController cursorColors; // single color authority (normal/tablet/hover/click)
        private DesktopWorldUiInteractor worldUiInteractor; // world-canvas hover (tracks the reticle even off-center in tablet mode)
        private bool currentlyTinted;
        private bool currentlyFlashing;
        private bool missingImageWarned;

        private void OnEnable()
        {
            playerCamera = Camera.main;
            cursorColors = GetComponentInParent<CursorStateController>()
                           ?? FindFirstObjectByType<CursorStateController>();
            worldUiInteractor = GetComponentInParent<DesktopWorldUiInteractor>()
                                ?? FindFirstObjectByType<DesktopWorldUiInteractor>();
            foreach (var interactor in GetComponentsInChildren<XRBaseInteractor>(true))
            {
                interactor.hoverEntered.AddListener(OnHoverEntered);
                interactor.hoverExited.AddListener(OnHoverExited);
                subscribedInteractors.Add(interactor);

                // The gaze rig (the one TrackedPoseSchemeGate keeps camera-aligned on
                // desktop) is the ray that hits UI in M&K/gamepad mode.
                if (gazeRay == null && interactor is XRRayInteractor ray
                    && ray.GetComponentInParent<TrackedPoseSchemeGate>() != null)
                {
                    gazeRay = ray;
                }
            }

            if (gazeRay == null)
            {
                Debug.LogWarning($"{LogPrefix} ReticleHoverFeedback on '{name}': no gaze XRRayInteractor found (the ray " +
                    "under a TrackedPoseSchemeGate) — UI hover cannot tint the reticle in M&K/gamepad, only interactable " +
                    "hover will. Is the Gaze Interactor piece present on this Player (variant)?", this);
            }

            var playerInput = GetComponentInParent<PlayerInput>() ?? GetComponentInChildren<PlayerInput>(true);
            if (playerInput != null && playerInput.actions != null)
            {
                interactAction = playerInput.actions.FindAction("FPS/Interact", throwIfNotFound: false);
                takeAction = playerInput.actions.FindAction("FPS/TakeObject", throwIfNotFound: false);
                if (interactAction != null)
                {
                    interactAction.performed += OnInteractPerformed;
                    interactAction.canceled += OnInteractCanceled;
                }
                if (takeAction != null)
                {
                    takeAction.performed += OnInteractPerformed;
                    takeAction.canceled += OnInteractCanceled;
                }
            }
        }

        private void OnDisable()
        {
            foreach (var interactor in subscribedInteractors)
            {
                if (interactor == null) continue;
                interactor.hoverEntered.RemoveListener(OnHoverEntered);
                interactor.hoverExited.RemoveListener(OnHoverExited);
            }
            subscribedInteractors.Clear();
            hovered.Clear();
            if (interactAction != null)
            {
                interactAction.performed -= OnInteractPerformed;
                interactAction.canceled -= OnInteractCanceled;
            }
            if (takeAction != null)
            {
                takeAction.performed -= OnInteractPerformed;
                takeAction.canceled -= OnInteractCanceled;
            }
            interactHeld = false;
            // Never leave the reticle stranded pressed (shrunk) if we are torn down or
            // the scheme switches while the button is still down.
            if (cursorColors != null) cursorColors.SetClickHeld(false);
            Apply(false, false);
        }

        private void OnHoverEntered(HoverEnterEventArgs args) => hovered.Add(args.interactableObject);
        private void OnHoverExited(HoverExitEventArgs args) => hovered.Remove(args.interactableObject);

        // Telemetry for the F8 overlay. Four independent sources can tint the reticle,
        // and if ANY of them is stuck on it never returns to its resting colour — so the
        // overlay reports each one separately instead of just the final verdict.
        internal bool DebugTinted => currentlyTinted;
        internal bool DebugInteractHeld => interactHeld;
        internal int DebugXriHoverCount => hovered.Count;
        internal bool DebugGazeRayUiHover => gazeRay != null
                                             && gazeRay.TryGetCurrentUIRaycastResult(out var h) && IsRealUi(h);

        /// <summary>What the gaze ray's UI raycast returned — and whether it was a real UI element or a blocker sentinel.</summary>
        internal string DebugGazeRayUiDescription()
        {
            if (gazeRay == null) return "<no gaze ray>";
            if (!gazeRay.TryGetCurrentUIRaycastResult(out var hit) || hit.gameObject == null) return "no UI hit";
            var raycaster = hit.module != null ? hit.module.GetType().Name : "?";
            return IsRealUi(hit)
                ? $"'{hit.gameObject.name}' via {raycaster} at {hit.distance:0.##}m -> TINTS"
                : $"'{hit.gameObject.name}' via {raycaster} — BLOCKER sentinel (the raycaster's own object, not UI) -> ignored";
        }
        internal bool DebugWorldUiHover => worldUiInteractor != null && worldUiInteractor.HasUiHover;
        internal bool DebugPhysicsHover => PhysicsHover();
        internal LayerMask DebugPhysicsMask => physicsHoverMask;
        internal float DebugPhysicsDistance => physicsHoverDistance;
        internal float DebugInteractableDistance => interactableHoverDistance;

        /// <summary>What the two physics hover rays actually hit right now, and why it counts as a hover.</summary>
        internal string DebugPhysicsHitDescription()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            if (playerCamera == null) return "<no camera>";
            var origin = playerCamera.transform;

            var text = string.Empty;
            if (physicsHoverMask.value != 0
                && Physics.Raycast(origin.position, origin.forward, out var masked, physicsHoverDistance, physicsHoverMask))
            {
                var held = IsInOurOwnHand(masked.collider) ? "  (IN HAND -> ignored)" : "  -> TINTS";
                text += $"maskRay hit '{masked.collider.name}' layer '{LayerMask.LayerToName(masked.collider.gameObject.layer)}' "
                        + $"at {masked.distance:0.##}m{held}";
            }
            else text += physicsHoverMask.value == 0 ? "maskRay: <mask empty, source off>" : "maskRay: no hit";

            if (Physics.Raycast(origin.position, origin.forward, out var hit, interactableHoverDistance))
            {
                var what = IsInOurOwnHand(hit.collider) ? "IN HAND -> ignored"
                    : hit.collider.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable>() != null ? "IXRInteractable -> TINTS"
                    : hit.collider.GetComponentInParent<Seat>() != null ? "Seat -> TINTS"
                    : hit.collider.GetComponentInParent<PickableObject>() != null ? "PickableObject -> TINTS"
                    : hit.collider.GetComponentInParent<IReticleHoverable>() != null ? "IReticleHoverable -> TINTS"
                    : "nothing usable on it";
                text += $"\n     usableRay hit '{hit.collider.name}' at {hit.distance:0.##}m: {what}";
            }
            else text += "\n     usableRay: no hit";

            return text;
        }

        private void OnInteractPerformed(InputAction.CallbackContext _)
        {
            // Only turn gold when the press lands on something the reticle marks as
            // usable; it stays gold for as long as the press is held (drags included).
            if (currentlyTinted)
            {
                interactHeld = true;
                if (cursorColors != null) cursorColors.SetClickHeld(true); // and shrink, for as long as it is held
            }
        }

        private void OnInteractCanceled(InputAction.CallbackContext _)
        {
            interactHeld = false;
            if (cursorColors != null) cursorColors.SetClickHeld(false); // released: grow back
        }

        private void Update()
        {
            var scheme = BroadcastControlsStatus.controlScheme;
            var desktop = scheme == BroadcastControlsStatus.ControlScheme.KeyboardMouse
                          || scheme == BroadcastControlsStatus.ControlScheme.Gamepad;
            var uiHovered = desktop && gazeRay != null
                            && gazeRay.TryGetCurrentUIRaycastResult(out var gazeUiHit)
                            && IsRealUi(gazeUiHit);
            // Fourth source: the world-canvas interactor. Unlike the gaze ray (always
            // camera-forward) it follows the reticle even when the cursor is free.
            uiHovered |= desktop && worldUiInteractor != null && worldUiInteractor.HasUiHover;
            var physicsHovered = desktop && PhysicsHover();
            Apply(desktop && (hovered.Count > 0 || uiHovered || physicsHovered),
                desktop && interactHeld);
        }

        private bool PhysicsHover()
        {
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return false;
            }
            var origin = playerCamera.transform;

            // Project layers: any hit on the mask counts (uvs Highlight_Interactionable pattern).
            if (physicsHoverMask.value != 0
                && Physics.Raycast(origin.position, origin.forward, out var masked, physicsHoverDistance, physicsHoverMask)
                && !IsInOurOwnHand(masked.collider))
                return true;

            // XRI interactables and package usables: the gaze interactor only hovers
            // interactables that opt into allowGazeInteraction (off by default), so the
            // reticle finds them by raycast — same way TakeObject/SitController do.
            if (Physics.Raycast(origin.position, origin.forward, out var hit, interactableHoverDistance))
            {
                // The thing we are HOLDING is docked right in front of the camera, so it
                // sits permanently under the reticle. Without this the reticle stays lit
                // for as long as you carry anything and can never return to its resting
                // colour — you are not "hovering" what is already in your hand.
                if (IsInOurOwnHand(hit.collider)) return false;

                if (hit.collider.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable>() != null) return true;
                if (hit.collider.GetComponentInParent<Seat>() != null) return true;
                if (hit.collider.GetComponentInParent<PickableObject>() != null) return true;
                if (hit.collider.GetComponentInParent<IReticleHoverable>() != null) return true; // tooltips & friends
            }
            return false;
        }

        // Held items are parented to the camera (or to a dock beneath it), so "is it under
        // the camera?" is exactly "is it in our hand?" — and it stays true for hand docks
        // and the camera dock alike.
        private bool IsInOurOwnHand(Component hit)
            => playerCamera != null && hit != null && hit.transform.IsChildOf(playerCamera.transform);

        /// <summary>
        /// Is this UI raycast result actually UI?
        ///
        /// "The ray produced a UI hit" is NOT the same question. UI Toolkit's
        /// WorldDocumentRaycaster appends a result for ANY collider its world picker hits,
        /// even when there is no document there — and sets that result's gameObject to
        /// ITSELF. Unity's own comment says why: such hits "should block UI but not hide
        /// the PhysicsRaycaster results", i.e. they are occlusion sentinels, not UI.
        ///
        /// Taking them at face value meant a plain wall, floor or building counted as
        /// hovering UI, so the reticle sat on its hover colour almost everywhere.
        /// </summary>
        private static bool IsRealUi(RaycastResult hit)
        {
            if (hit.gameObject == null) return false;
            // The blocker sentinel: the result points at the raycaster's own GameObject.
            if (hit.module != null && hit.gameObject == hit.module.gameObject) return false;
            return true;
        }

        private void Apply(bool tinted, bool flashing)
        {
            // The cursor manager's invalid-request flash owns the reticle color while
            // active — don't fight it.
            if (cursorColors != null && cursorColors.IsFlashingInvalid)
            {
                currentlyTinted = false;
                currentlyFlashing = false;
                return;
            }
            // NO "unchanged since last frame" early-out. CursorStateController ALSO writes
            // this color (state changes, the invalid flash), so a cached flag goes stale
            // behind our back and the reticle then stays tinted forever — the resting
            // color is only re-applied on a CHANGE that never comes. Re-asserting it every
            // frame costs one color write and makes this the single authority.
            if (reticleImage == null)
            {
                if (!missingImageWarned && tinted)
                {
                    missingImageWarned = true;
                    Debug.LogWarning($"{LogPrefix} ReticleHoverFeedback on '{name}': reticleImage is not assigned — the " +
                        "reticle cannot signal hover. Wire the Cursor's SVGImage (same one CursorStateController uses).", this);
                }
                return;
            }
            if (!defaultCaptured)
            {
                defaultCaptured = true;
                defaultColor = reticleImage.color;
            }
            // All four reticle colors come from the CursorStateController when present
            // (the single color authority under _settings); the serialized fields here
            // are only a fallback. Resting is the manager's CURRENT normal/tablet color,
            // so the reticle returns to the right base after a tablet toggle.
            var hover = cursorColors != null ? cursorColors.HoverColor : hoverColor;
            var flash = cursorColors != null ? cursorColors.ClickColor : interactFlashColor;
            var resting = cursorColors != null ? cursorColors.RestingColor : defaultColor;
            reticleImage.color = flashing ? flash : tinted ? hover : resting;
            currentlyTinted = tinted;
            currentlyFlashing = flashing;
        }
    }
}
