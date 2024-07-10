using System;
using System.Diagnostics;
using System.Collections.Generic;
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

        //Camera
        [SerializeField] Camera mainCamera;

        //TakeObject
        Transform objectInHandTransform;

        public PickableObject _objectInHand { get { return objectInHand; } set { objectInHand = value; } }

        //InputActions
        [SerializeField] InputActionReference takeAction;
        [SerializeField] InputActionReference scrollAction;

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
        private MotionHandle _positionHandle;
        private MotionHandle _rotationHandle;
        [SerializeField] private LayerMask layerMask;


        [Range(0.01f, 0.5f)]
        [SerializeField] private float sliderMotionDuration;

        [Header("Broadcasting On")]
        [SerializeField] GameObjectEventChannelSO objectDropped;

        [Header("XR")]
        [SerializeField] XRDirectInteractor rightInteractor;
        [SerializeField] XRDirectInteractor leftInteractor;

        [Header("Objects in players's hand")]
        PickableObject objectRightHand;
        PickableObject objectLeftHand;
        PickableObject objectInHand;

        private void LateUpdate()
        {
            if (objectInHand)
            {
                var goal = mainCamera.transform.position + mainCamera.transform.forward * objectDistance;
                SetObjectPosition(objectInHandTransform, goal);
            }  
        }


        private void OnEnable()
        {
            takeAction.action.performed += ctx => DispatchAction();
            scrollAction.action.performed += ctx => UpdateObjectDistance(ctx.ReadValue<float>());
        }

        private void OnDisable() => Unsubscribe();


        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            takeAction.action.performed -= ctx => DispatchAction();
            scrollAction.action.performed -= ctx => UpdateObjectDistance(ctx.ReadValue<float>());
            DisablePositionHandle();
            DisableRotationHandle();


        }

        //Check, when received input action for take, if there's already an object in hand or not
        private void DispatchAction()
        {
            if (objectInHand == null)
            {
                Take();
            }
            else
            {
                Release();
            }
        }

        //Checks for raycast hit, if object is pickable then pick it
        private void Take()
        {
            RaycastHit hit;
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            
            if (Physics.Raycast(ray, out hit, maxDistanceCheck, layerMask))
            {
                if (hit.transform.gameObject.GetComponent<PickableObject>())
                {
                    Transform objectHit = hit.transform;
                    objectInHandTransform = objectHit;
                    objectInHand = hit.transform.gameObject.GetComponent<PickableObject>();
                    //objectInHandTransform.SetParent(this.gameObject.transform);
                    objectInHand.Rigidbody.freezeRotation = true;
                    objectInHand.Rigidbody.useGravity = false;
                }
            }
        }

        //Drops object in hand
        private void Release()
        {
            if (objectInHand.ReturnToInitialPositionOnRelease)
            {
                DisablePositionHandle();
                var goalPosition = objectInHand.InitialPosition;
                SetObjectPosition(objectInHandTransform, goalPosition);

                var goalRotation = objectInHand.InitialRotation;
                SetObjectRotation(objectInHandTransform, goalRotation);
            }
            objectDropped?.RaiseEvent(objectInHand.gameObject);
            objectInHand.Rigidbody.useGravity = objectInHand.InitialUseGravity;
            objectInHand.Rigidbody.drag = objectInHand.InitialDrag;
            objectInHand.Rigidbody.angularDrag = objectInHand.InitialAngularDrag;
            objectInHand.Rigidbody.freezeRotation = false;
            objectInHandTransform.SetParent(null);
            objectInHandTransform = null;
            objectInHand = null;

        }

        //public GameObject GetObjectInHand()
        //{
        //    if(objectInHand) return objectInHand.gameObject;
        //    return null;
        //}
        private void UpdateObjectDistance(float value)
        {
            value *= scrollStep;
            if (_isDebug) Debug.Log($"scroll reading: {value}");
            objectDistance += value;
            if (objectDistance > maxDistance) objectDistance = maxDistance;
            if (objectDistance < minDistance) objectDistance = minDistance;

            var goalPosition = mainCamera.transform.position + mainCamera.transform.forward * objectDistance;
            SetObjectPosition(objectInHandTransform, goalPosition);
        }

        private void DisablePositionHandle()
        {
            if (!_positionHandle.IsActive()) return;
            _positionHandle.Complete();
            _positionHandle.Cancel();
        }
        private void DisableRotationHandle()
        {
            if (!_rotationHandle.IsActive()) return;
            _rotationHandle.Complete();
            _rotationHandle.Cancel();
        }

        private void SetObjectPosition(Transform objectToMove, Vector3 goal)
        {
            if (!objectToMove)
            {
                if (_isDebug) Debug.Log($"objectToMove is null");

                DisablePositionHandle();
                return;
            }


            if (objectToMove.transform.position == goal) return;

            _positionHandle = LMotion.Create(objectToMove.transform.position, goal, sliderMotionDuration)
                .Bind(x => objectToMove.transform.position = x)
                .AddTo(objectToMove.gameObject);
            //objectToMove.transform.position = goal;
        }
        private void SetObjectRotation(Transform objectToMove, Quaternion goal)
        {
            if (!objectToMove)
            {
                DisableRotationHandle();
                return;
            }
            if (objectToMove.transform.rotation == goal) return;

            _rotationHandle = LMotion.Create(objectToMove.transform.rotation, goal, sliderMotionDuration)
                .Bind(x => objectToMove.transform.rotation = x)
                .AddTo(objectToMove.gameObject);

            //objectToMove.transform.rotation = goal;
            
        }

        public void AssignGameObjectInRightHand()
        {
            objectRightHand = rightInteractor.selectTarget.gameObject.GetComponent<PickableObject>();
        }

        public void AssignGameObjectInLeftHand()
        {
            objectLeftHand = leftInteractor.selectTarget.gameObject.GetComponent<PickableObject>();
        }

        public void RemoveGameObjectInRightHand()
        {
            objectDropped?.RaiseEvent(objectRightHand.gameObject);

            objectRightHand = null;
        }

        public void RemoveGameObjectInLeftHand()
        {
            objectDropped?.RaiseEvent(objectLeftHand.gameObject);

            objectLeftHand = null;
        }

        public bool GetObjectsInHandStatus()
        {
            try
            {
                Debug.Log("Object in hand" + objectInHand);
            }
            catch
            {
                Debug.Log("No object in hand");
            }

            try
            {
                Debug.Log("Object in left hand VR" + objectLeftHand);
            }
            catch
            {
                Debug.Log("No object in left hand VR");
            }

            try
            {
                Debug.Log("Object in right hand VR" + objectRightHand);
            }
            catch
            {
                Debug.Log("No object in right hand");
            }
            if (objectInHand == null && objectRightHand == null && objectLeftHand == null) return false;
            else { return true; }
        }
        public List<GameObject> GetObjectsInHand()
        {
            List<GameObject> objectsInHand = new List<GameObject> ();

            if(objectLeftHand != null)
            {
                objectsInHand.Add(objectLeftHand.gameObject);
            }

            if (objectRightHand != null)
            {
                objectsInHand.Add (objectRightHand.gameObject);
            }

            if (objectInHand != null)
            {
                objectsInHand.Add(objectInHand.gameObject);
            }

            return objectsInHand;
        }
    }
}



