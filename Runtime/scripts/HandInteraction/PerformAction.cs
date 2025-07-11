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
            
        // Start is called before the first frame update
        private Transform cameraTransform;
        [Space(20)] 
        [SerializeField] private GameObject objectToInteractWith;
        [SerializeField] private InputActionReference performAction;
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private float maxDistanceCheck = 2f;
    
        [Header("Broadcasting on:")] 
        [SerializeField] private TransformEventChannelSO actionMade; 
        private void Awake()
        {
            if (!cameraTransform) cameraTransform = Camera.main.transform;
        }
    
        private void OnEnable()
        {
            if(performAction) performAction.action.performed += _ => AttemptAction();
        }
    
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();
    
        private void Unsubscribe()
        {
            if(performAction) performAction.action.performed -= null;
        }
    
        private void AttemptAction()
        {
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR) return;
            if (!cameraTransform) cameraTransform = Camera.main.transform;
    
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            if (!Physics.Raycast(ray, out var hit, maxDistanceCheck, layerMask)) return;
    
            if (_isDebug) Debug.Log($"ray hit with: {hit.transform.gameObject.name}");
            if (hit.transform.gameObject != objectToInteractWith) return;
            
            if (_isDebug) Debug.Log($"it's a match! lets act");
            if(actionMade) actionMade.RaiseEvent(hit.transform);
        }
    }
}