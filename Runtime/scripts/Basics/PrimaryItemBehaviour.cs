using jeanf.EventSystem;
using jeanf.validationTools;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The generic "held item" behavior — put this ON the primary item (tablet).
    /// It gives the draw/holster flow a visible result in every project without
    /// custom code:
    ///
    /// - Drawn (state channel true, M&amp;K/gamepad): the item docks IN FRONT OF THE
    ///   CAMERA (parented to it) at an authorable local pose, physics off.
    /// - Holstered (false): the item returns to where it lived and (optionally)
    ///   hides its renderers.
    /// - VR: placement is left to the hand flow (GetPrimaryInHandItemWithVRController)
    ///   when one drives this item; otherwise the camera dock is used there too.
    /// - Picked up like any object (TakeObject's taken channel, or an
    ///   XRGrabInteractable on the same GameObject): the grab PROMOTES the item
    ///   to drawn — grabbing the tablet on a table equals drawing it.
    ///
    /// Renderers are toggled instead of the GameObject so the component keeps
    /// listening while hidden.
    /// </summary>
    public class PrimaryItemBehaviour : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Header("Listening on:")]
        [Validation("The shared primary item state channel is required (same asset as PrimaryItemController).")]
        [SerializeField] private BoolEventChannelSO primaryItemStateChannel;
        [Tooltip("Optional: TakeObject's taken channel — picking this item up then PROMOTES it to drawn.")]
        [SerializeField] private GameObjectIntBoolEventChannelSO objectTakenChannel;

        [Header("Camera dock (used when no VR hand flow drives this item)")]
        [Tooltip("Local position in front of the camera while drawn.")]
        [SerializeField] private Vector3 dockLocalPosition = new Vector3(0f, -0.12f, 0.45f);
        [Tooltip("Local rotation while drawn — tilted toward the eyes like a held tablet.")]
        [SerializeField] private Vector3 dockLocalEuler = new Vector3(35f, 0f, 0f);
        [Tooltip("Hide the item's renderers while holstered (off = it stays visible wherever it was).")]
        [SerializeField] private bool hideWhenHolstered = true;
        [Tooltip("ON when a GetPrimaryInHandItemWithVRController positions this item in VR — the camera dock then stays out of the way in XR.")]
        [SerializeField] private bool vrHandFlowOwnsPlacement = true;

        private Transform _originalParent;
        private Vector3 _originalPosition;
        private Quaternion _originalRotation;
        private bool _originCaptured;
        private bool _drawn;

        private void OnEnable()
        {
            if (primaryItemStateChannel != null) primaryItemStateChannel.OnEventRaised += OnPrimaryItemState;
            if (objectTakenChannel != null) objectTakenChannel.OnEventRaised += OnObjectTaken;

            var grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grabInteractable != null) grabInteractable.selectEntered.AddListener(OnGrabbed);

            if (hideWhenHolstered && !_drawn) SetVisible(false);
        }

        private void OnDisable()
        {
            if (primaryItemStateChannel != null) primaryItemStateChannel.OnEventRaised -= OnPrimaryItemState;
            if (objectTakenChannel != null) objectTakenChannel.OnEventRaised -= OnObjectTaken;

            var grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            if (grabInteractable != null) grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        }

        // A grab IS a draw: whether the item was taken desktop-style or grabbed
        // in VR, promote it to the drawn state so every system (cursor, look,
        // hand pose) reacts identically.
        private void OnObjectTaken(GameObject taken, int _, bool isTaken)
        {
            if (taken != gameObject || !isTaken) return;
            if (primaryItemStateChannel != null) primaryItemStateChannel.RaiseEvent(true);
        }

        private void OnGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
        {
            // Let go of the XRI grab immediately — the dock (or the VR hand
            // flow) owns the placement from here.
            if (args.interactableObject is UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab
                && args.manager != null)
                args.manager.SelectExit(args.interactorObject, grab);
            if (primaryItemStateChannel != null) primaryItemStateChannel.RaiseEvent(true);
        }

        private void OnPrimaryItemState(bool drawn)
        {
            _drawn = drawn;
            if (drawn) Dock();
            else Holster();
        }

        private void Dock()
        {
            var inXr = BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR;
            SetVisible(true);

            // In VR the hand flow (when present) poses the item into the hand.
            if (inXr && vrHandFlowOwnsPlacement) return;

            var cameraTransform = Camera.main != null ? Camera.main.transform : null;
            if (cameraTransform == null)
            {
                Debug.LogWarning($"{LogPrefix} PrimaryItemBehaviour on '{name}': no main camera to dock to.", this);
                return;
            }

            CaptureOriginOnce();
            SetPhysicsActive(false);
            transform.SetParent(cameraTransform, false);
            transform.localPosition = dockLocalPosition;
            transform.localRotation = Quaternion.Euler(dockLocalEuler);
        }

        private void Holster()
        {
            if (_originCaptured)
            {
                transform.SetParent(_originalParent, true);
                transform.SetPositionAndRotation(_originalPosition, _originalRotation);
                SetPhysicsActive(true);
            }
            if (hideWhenHolstered) SetVisible(false);
        }

        private void CaptureOriginOnce()
        {
            if (_originCaptured) return;
            _originCaptured = true;
            _originalParent = transform.parent;
            _originalPosition = transform.position;
            _originalRotation = transform.rotation;
        }

        private void SetVisible(bool visible)
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
            foreach (var canvas in GetComponentsInChildren<Canvas>(true)) canvas.enabled = visible;
        }

        private void SetPhysicsActive(bool active)
        {
            foreach (var body in GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = !active;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = active;
        }
    }
}
