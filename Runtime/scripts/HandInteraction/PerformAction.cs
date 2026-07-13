using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    public class PerformAction : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;
            
        private const string LogPrefix = "[UniversalPlayer]";

        // Start is called before the first frame update
        private Transform cameraTransform;
        [Space(20)]
        [SerializeField] private GameObject objectToInteractWith;
        [Tooltip("Optional override. Empty = the player's FPS/Interact action (E / click / gamepad A).")]
        [SerializeField] private InputActionReference performAction;
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private float maxDistanceCheck = 2f;

        [Header("Broadcasting on:")]
        [SerializeField] private TransformEventChannelSO actionMade;

        private InputAction resolvedAction;

        private void Awake()
        {
            if (!cameraTransform) cameraTransform = Camera.main.transform;
        }

        private void OnEnable()
        {
            // Zero-wiring default: without an explicit reference, react to the
            // player's Interact binding — same resolve-by-name pattern as SitController.
            resolvedAction = performAction != null
                ? performAction.action
                : FindFirstObjectByType<PlayerInput>(FindObjectsInactive.Include)?.actions
                    ?.FindAction("FPS/Interact", throwIfNotFound: false);

            if (resolvedAction != null) resolvedAction.performed += OnPerformAction;
            else Debug.LogWarning($"{LogPrefix} PerformAction on '{name}': no performAction assigned and no PlayerInput with " +
                "an FPS/Interact action found — this button cannot be used outside VR.", this);
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (resolvedAction != null) resolvedAction.performed -= OnPerformAction;
            resolvedAction = null;
        }

        private void OnPerformAction(InputAction.CallbackContext _) => AttemptAction();
    
        private void AttemptAction()
        {
            Ray ray;
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                // In VR the interact press acts along the POINTING finger's ray
                // (trigger released = point pose); not pointing = no action.
                if (!FingerPointingRay.TryGetPointingRay(out ray)) return;
            }
            else
            {
                if (!cameraTransform) cameraTransform = Camera.main.transform;
                ray = new Ray(cameraTransform.position, cameraTransform.forward);
            }

            if (!Physics.Raycast(ray, out var hit, maxDistanceCheck, layerMask)) return;
    
            if (_isDebug) Debug.Log($"ray hit with: {hit.transform.gameObject.name}");
            // Buttons are compound (base/button/poke colliders): any collider of the
            // same rig counts, not only the exact assigned object.
            var target = objectToInteractWith.transform;
            if (hit.transform != target && !hit.transform.IsChildOf(target) && !target.IsChildOf(hit.transform)) return;
            
            if (_isDebug) Debug.Log($"it's a match! lets act");
            if(actionMade) actionMade.RaiseEvent(hit.transform);
        }
    }
}