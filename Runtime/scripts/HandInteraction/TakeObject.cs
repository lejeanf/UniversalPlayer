using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using jeanf.EventSystem;
using UnityEditor;
using Debug = UnityEngine.Debug;
using jeanf.propertyDrawer;
using LitMotion;

namespace jeanf.vrplayer
{
    public class TakeObject : MonoBehaviour, IDebugBehaviour
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
        [SerializeField] private InputActionReference takeAction;
        [SerializeField] private InputActionReference scrollAction;
        public enum TakeStyle { hold, toggle}
        [Space(20)]
        [SerializeField] private LayerMask layerMask;
        [SerializeField] private TakeStyle _takeStyle = TakeStyle.toggle;
        
        
        [Space(20)]
        [SerializeField] private bool advancedSettings = false;
        private float objectDistance = .5f;
        [DrawIf("advancedSettings", true, ComparisonType.Equals)]
        [Range(.1f, .9f)]
        [SerializeField]
        private float minDistance = .5f;
        [DrawIf("advancedSettings", true, ComparisonType.Equals)]
        [Range(1f, 2f)]
        [SerializeField]
        private float maxDistance = 1.25f;
        [DrawIf("advancedSettings", true, ComparisonType.Equals)]
        [Range(.0001f, 0.001f)]
        [SerializeField]
        private float scrollStep = .001f;
        [DrawIf("advancedSettings", true, ComparisonType.Equals)]
        [Range(.5f, 10f)]
        [SerializeField] private float maxDistanceCheck = 2f;
        private Transform _currentObjectHeld;
        private Rigidbody _currentObjectHeldRb;
        private MotionHandle _positionHandle;
        private MotionHandle _rotationHandle;

        private bool _gravityBeforeGrab;
        private float _dragBeforeGrab;
        private float _angularDragBeforeGrab;
        
        //Reset position on release
        public bool resetPositionOnRelease = false;
        [SerializeField] [ReadOnly] private Vector3 initialPos;
        [SerializeField] [ReadOnly] private Quaternion initialRot;

        [Space(20)]
        private bool holdState = false;

        private void Awake()
        {
            if (!cameraTransform) cameraTransform = Camera.main.transform;
            initialPos = gameObject.transform.position;
            initialRot = gameObject.transform.rotation;
        }

        private void OnEnable()
        {
            takeAction.action.Enable();
            scrollAction.action.Enable();
            takeAction.action.performed += _ => DecideAction();
            takeAction.action.canceled += _ => ReleaseHold();
            scrollAction.action.performed += ctx => UpdateObjectDistance(ctx.ReadValue<float>());
        }


        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            //takeAction.action.started -= null;
            takeAction.action.canceled -= null;
            takeAction.action.performed -= null;
            scrollAction.action.performed -= null;
            takeAction.action.Disable();
            scrollAction.action.Disable();
        }

        private void LateUpdate()
        {
            if(BroadcastHmdStatus.hmdCurrentState) return;
            if (!cameraTransform) cameraTransform = Camera.main.transform;

            var goal = cameraTransform.position + cameraTransform.forward * objectDistance;
            SetObjectPosition(_currentObjectHeld, goal);
        }
        

        private void ToggleTake()
        {
            if(_isDebug) Debug.Log("toggle take");
            
            if (!holdState) Take();
            else Release();
            
            holdState = !holdState;
        }
        
        private void DecideAction()
        {
            switch (_takeStyle)
            {
                case TakeObject.TakeStyle.toggle:
                    ToggleTake();
                    break;
                default:
                    Take();
                    break;
            }
        }

        private void Take()
        {
            if(_isDebug) Debug.Log("take");
            if(_currentObjectHeld) return;
            if(BroadcastHmdStatus.hmdCurrentState) return;
            if (!cameraTransform) cameraTransform = Camera.main.transform;
            
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);

            if (!Physics.Raycast(ray, out var hit, maxDistanceCheck, layerMask)) return;
            if(_isDebug) Debug.Log($"ray hit with: {hit.transform.gameObject.name}");
            if (!hit.collider.GetComponent<XRGrabInteractable>()) return;
            if(_isDebug) Debug.Log($"{hit.transform.gameObject.name} is grabbable");
            
            
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
            if(_isDebug) Debug.Log("release");
            if(BroadcastHmdStatus.hmdCurrentState) return;
            if (!_currentObjectHeld) return;
            
            if (resetPositionOnRelease)
            {
                if(_isDebug) Debug.Log("Reset position");

                var goalPosition = initialPos;
                SetObjectPosition(_currentObjectHeld, goalPosition);
                
                var goalRotation = initialRot;
                SetObjectRotation(_currentObjectHeld, goalRotation);
            }

            _currentObjectHeldRb.useGravity = _gravityBeforeGrab;
            _currentObjectHeldRb.drag = _dragBeforeGrab;
            _currentObjectHeldRb.angularDrag = _angularDragBeforeGrab;
            if(_isDebug) Debug.Log($"releasing {_currentObjectHeld.name}");
                
            _currentObjectHeld = null;
            _currentObjectHeldRb = null;
        }

        private void ReleaseHold()
        {
            if(_takeStyle != TakeObject.TakeStyle.hold) return;
            if(_isDebug) Debug.Log("release hold");
            Release();
        }

        private void UpdateObjectDistance(float value)
        {
            value *= scrollStep;
            if (_isDebug) Debug.Log($"scroll reading: {value}");
            objectDistance += value;
            if (objectDistance > maxDistance) objectDistance = maxDistance;
            if (objectDistance < minDistance) objectDistance = minDistance;
            
            var goalPosition = cameraTransform.position + cameraTransform.forward * objectDistance;
            SetObjectPosition(_currentObjectHeld, goalPosition);
        }

        private void SetObjectPosition(Transform objectToMove ,Vector3 goal)
        {
            if (!objectToMove)
            {
                if (_positionHandle.IsActive())
                {
                    _positionHandle.Complete();
                    _positionHandle.Cancel();
                }
                return;
            }
            _positionHandle = LMotion.Create(objectToMove.transform.position,goal,.05f)
                .Bind(x => objectToMove.transform.position = x)
                .AddTo(objectToMove.gameObject);
        }
        private void SetObjectRotation(Transform objectToMove ,Quaternion goal)
        {
            if (!objectToMove)
            {
                if (_rotationHandle.IsActive())
                {
                    _rotationHandle.Complete();
                    _rotationHandle.Cancel();
                }
                return;
            }
            _rotationHandle = LMotion.Create(objectToMove.transform.rotation,goal,.05f)
                .Bind(x => objectToMove.transform.rotation = x)
                .AddTo(objectToMove.gameObject);
        }

    }
}


