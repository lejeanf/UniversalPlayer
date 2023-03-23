using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using DG.Tweening;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace jeanf.vrplayer
{
    public class TakeObject : MonoBehaviour
    {
        // Start is called before the first frame update
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private Transform cameraTransform;
        [Space(20)]
        [SerializeField] private InputActionReference takeAction;
        [SerializeField] private float maxDistanceCheck = 1.5f;
        private Transform _currentObjectHeld;
        private Rigidbody _currentObjectHeldRb;

        private bool _gravityBeforeGrab;
        private float _dragBeforeGrab;
        private float _angularDragBeforeGrab;



        public enum TakeStyle { hold, toggle}
        [SerializeField] private TakeStyle _takeStyle = TakeStyle.toggle;
        [Space(20)]
        [SerializeField] private bool isDebug = false;
        private bool holdState = false;

        private void OnEnable()
        {
            takeAction.action.Enable();
            takeAction.action.performed += _ => DecideAction();
            takeAction.action.canceled += _ => ReleaseHold();
        }


        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            //takeAction.action.started -= null;
            takeAction.action.canceled -= null;
            takeAction.action.performed -= null;
            takeAction.action.Disable();
            DOTween.KillAll();
        }

        private void FixedUpdate()
        {
            if(BroadcastHmdStatus.hmdCurrentState) return;
            if (_currentObjectHeld) _currentObjectHeld.transform.DOMove(cameraTransform.position + cameraTransform.forward * 0.5f, .05f, false);
        }

        private void ToggleTake()
        {
            if(isDebug) Debug.Log("toggle take");
            
            if (!holdState) Take();
            else Release();
            
            holdState = !holdState;
        }
        
        private void DecideAction()
        {
            switch (_takeStyle)
            {
                case TakeStyle.toggle:
                    ToggleTake();
                    break;
                default:
                    Take();
                    break;
            }
        }

        private void Take()
        {
            if(isDebug) Debug.Log("take");
            if(_currentObjectHeld) return;
            if(BroadcastHmdStatus.hmdCurrentState) return;
            
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (!Physics.Raycast(ray, out var hit, maxDistanceCheck, layerMask)) return;
            if(isDebug) Debug.Log($"ray hit with: {hit.transform.gameObject.name}");
            if (!hit.collider.GetComponent<XRGrabInteractable>()) return;
            if(isDebug) Debug.Log($"{hit.transform.gameObject.name} is grabbable");
            
            
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
            if(isDebug) Debug.Log("release");
            if(BroadcastHmdStatus.hmdCurrentState) return;
            if (!_currentObjectHeld) return;
            
            _currentObjectHeldRb.useGravity = _gravityBeforeGrab;
            _currentObjectHeldRb.drag = _dragBeforeGrab;
            _currentObjectHeldRb.angularDrag = _angularDragBeforeGrab;
            if(isDebug) Debug.Log($"releasing {_currentObjectHeld.name}");
                
            _currentObjectHeld = null;
            _currentObjectHeldRb = null;
        }

        private void ReleaseHold()
        {
            if(_takeStyle != TakeStyle.hold) return;
            if(isDebug) Debug.Log("release hold");
            Release();
        }
    }
}


