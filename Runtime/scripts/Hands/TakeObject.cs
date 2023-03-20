using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using DG.Tweening;

namespace jeanf.vrplayer
{
    public class TakeObject : MonoBehaviour
    {
        // Start is called before the first frame update
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float maxDistanceCheck = 1.5f;
        [SerializeField] private InputActionReference takeAction;
        private Transform _currentObjectHeld;
        private Rigidbody _currentObjectHeldRb;

        private bool _gravityBeforeGrab;
        private float _dragBeforeGrab;
        private float _angularDragBeforeGrab;
        
        private void OnEnable()
        {
            takeAction.action.Enable();
            takeAction.action.started += _ => Take();
            takeAction.action.canceled += _ => Release();
        }


        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            takeAction.action.started -= null;
            takeAction.action.canceled -= null;
            takeAction.action.Disable();
            DOTween.KillAll();
        }

        private void FixedUpdate()
        {
            if(BroadcastHmdStatus.hmdCurrentState) return;
            if (_currentObjectHeld) _currentObjectHeld.transform.DOMove(cameraTransform.position + cameraTransform.forward * 0.5f, .05f, false);
        }

        private void Take()
        {
            if(_currentObjectHeld) return;
            if(BroadcastHmdStatus.hmdCurrentState) return;
            
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (!Physics.Raycast(ray, out var hit, maxDistanceCheck)) return;
            if (!hit.collider.GetComponent<XRGrabInteractable>()) return;
            
            var rb = hit.transform.GetComponent<Rigidbody>();
            _gravityBeforeGrab = rb.useGravity;
            _dragBeforeGrab = rb.drag;
            _angularDragBeforeGrab = rb.angularDrag;
                    
            rb.useGravity = false;
            rb.drag = 10;
            rb.angularDrag = 10;
            _currentObjectHeldRb = rb;
            _currentObjectHeld = hit.transform;
        }

        private void Release()
        {
            if(BroadcastHmdStatus.hmdCurrentState) return;
            if (!_currentObjectHeld) return;
            
            _currentObjectHeldRb.useGravity = _gravityBeforeGrab;
            _currentObjectHeldRb.drag = _dragBeforeGrab;
            _currentObjectHeldRb.angularDrag = _angularDragBeforeGrab;
                
            _currentObjectHeld = null;
            _currentObjectHeldRb = null;
        }
    }
}


