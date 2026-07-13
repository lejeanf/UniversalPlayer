using System.Collections.Generic;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Desktop counterpart of the VR grab preview: the center reticle tints while the
    /// player is looking at something usable, and flashes when they interact.
    /// Three hover sources, any of which tints:
    ///   1. XRI interactor hover (grabbables, chairs, switches)
    ///   2. the gaze ray's UI raycast (world-space UI elements)
    ///   3. a plain physics raycast against <see cref="physicsHoverMask"/> — the same
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
        [SerializeField] private SVGImage reticleImage;
        [SerializeField] private Color hoverColor = new Color(0.35f, 0.95f, 0.6f);
        [Tooltip("Shown on the reticle for as long as Interact/TakeObject is held after pressing on something usable.")]
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
        private bool currentlyTinted;
        private bool currentlyFlashing;
        private bool missingImageWarned;

        private void OnEnable()
        {
            playerCamera = Camera.main;
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
            Apply(false, false);
        }

        private void OnHoverEntered(HoverEnterEventArgs args) => hovered.Add(args.interactableObject);
        private void OnHoverExited(HoverExitEventArgs args) => hovered.Remove(args.interactableObject);

        private void OnInteractPerformed(InputAction.CallbackContext _)
        {
            // Only turn gold when the press lands on something the reticle marks as
            // usable; it stays gold for as long as the press is held (drags included).
            if (currentlyTinted) interactHeld = true;
        }

        private void OnInteractCanceled(InputAction.CallbackContext _) => interactHeld = false;

        private void Update()
        {
            var scheme = BroadcastControlsStatus.controlScheme;
            var desktop = scheme == BroadcastControlsStatus.ControlScheme.KeyboardMouse
                          || scheme == BroadcastControlsStatus.ControlScheme.Gamepad;
            var uiHovered = desktop && gazeRay != null && gazeRay.TryGetCurrentUIRaycastResult(out _);
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
                && Physics.Raycast(origin.position, origin.forward, physicsHoverDistance, physicsHoverMask))
                return true;

            // XRI interactables and package usables: the gaze interactor only hovers
            // interactables that opt into allowGazeInteraction (off by default), so the
            // reticle finds them by raycast — same way TakeObject/SitController do.
            if (Physics.Raycast(origin.position, origin.forward, out var hit, interactableHoverDistance))
            {
                if (hit.collider.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable>() != null) return true;
                if (hit.collider.GetComponentInParent<Seat>() != null) return true;
                if (hit.collider.GetComponentInParent<PickableObject>() != null) return true;
            }
            return false;
        }

        private void Apply(bool tinted, bool flashing)
        {
            if (tinted == currentlyTinted && flashing == currentlyFlashing) return;
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
            reticleImage.color = flashing ? interactFlashColor : tinted ? hoverColor : defaultColor;
            currentlyTinted = tinted;
            currentlyFlashing = flashing;
        }
    }
}
