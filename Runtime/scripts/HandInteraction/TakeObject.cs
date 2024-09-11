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
        #region variables
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

        //Object Movement
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
        int roomId;

        [Range(0.01f, 0.5f)]
        [SerializeField] private float sliderMotionDuration;

        //Taken Object status channels
        [Header("Broadcasting On")]
        [SerializeField] GameObjectEventChannelSO objectDropped;
        [SerializeField] GameObjectIntBoolEventChannelSO objectTakenChannel;
        
        [Header("Listening On")]
        [SerializeField] IntEventChannelSO roomIdChannelSO;
        [SerializeField] GameObjectEventChannelSO snapEventChannelSO;

        [Header("XR")]
        [SerializeField] XRDirectInteractor rightInteractor;
        [SerializeField] XRDirectInteractor leftInteractor;

        [Header("Objects in players's hand")]
        PickableObject objectRightHand;
        PickableObject objectLeftHand;
        PickableObject objectInHand;

        //Snap variable(s)
        bool objectIsSnapping;
        #endregion

        #region Default MonoBehaviour Methods
        private void OnEnable()
        {
            takeAction.action.performed += ctx => DispatchAction();
            scrollAction.action.performed += ctx => UpdateObjectDistance(ctx.ReadValue<float>());
            try
            {
                roomIdChannelSO.OnEventRaised += AssignRoomId;
            }
            catch { }

            snapEventChannelSO.OnEventRaised += ctx => Snap(ctx);
        }

        private void OnDisable() => Unsubscribe();


        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            takeAction.action.performed -= ctx => DispatchAction();
            scrollAction.action.performed -= ctx => UpdateObjectDistance(ctx.ReadValue<float>());
            try
            {
                roomIdChannelSO.OnEventRaised -= AssignRoomId;
            }
            catch { }
            DisablePositionHandle();
            DisableRotationHandle();
            snapEventChannelSO.OnEventRaised -= ctx => Snap(ctx);
        }

        private void Update()
        {
            if (objectInHand && objectIsSnapping)
            {
                Snap(objectInHand.gameObject);
            }
        }
        private void LateUpdate()
        {
            if (objectInHand)
            {
                var goal = mainCamera.transform.position + mainCamera.transform.forward * objectDistance;
                SetObjectPosition(objectInHandTransform, goal, false);
            }  
        }
        #endregion

        #region Take Handling methods
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
                    objectTakenChannel?.RaiseEvent(hit.transform.gameObject, roomId, true);
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
                SetObjectPosition(objectInHandTransform, goalPosition, false);

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

            if (objectInHand == null && objectRightHand == null && objectLeftHand == null) return false;
            else { return true; }
        }
        public List<GameObject> GetObjectsInHand()
        {
            List<GameObject> objectsInHand = new List<GameObject>();

            if (objectLeftHand != null)
            {
                objectsInHand.Add(objectLeftHand.gameObject);
            }

            if (objectRightHand != null)
            {
                objectsInHand.Add(objectRightHand.gameObject);
            }

            if (objectInHand != null)
            {
                objectsInHand.Add(objectInHand.gameObject);
            }

            return objectsInHand;
        }
        #endregion

        #region Object movemement methods
        private void UpdateObjectDistance(float value)
        {
            if (objectIsSnapping) return;
            value *= scrollStep;
            if (_isDebug) Debug.Log($"scroll reading: {value}");
            objectDistance += value;
            if (objectDistance > maxDistance) objectDistance = maxDistance;
            if (objectDistance < minDistance) objectDistance = minDistance;

            var goalPosition = mainCamera.transform.position + mainCamera.transform.forward * objectDistance;
            SetObjectPosition(objectInHandTransform, goalPosition, false);
        }
        private void Snap(GameObject objectToSnap)
        {
            if (objectToSnap.GetComponent<SnapObject>() == null) return;
            SnapObject snapObject = objectToSnap.GetComponent<SnapObject>();
            RaycastHit hit;

            if (Physics.Raycast(mainCamera.ScreenPointToRay(Input.mousePosition), out hit, Mathf.Infinity, layerMask: snapObject.SnapTargetLayer))
            {
                float minDistance = Mathf.Infinity;

                foreach (SnapPoint snapPoint in snapObject.SnapPoints)
                {
                    float distance = Vector3.Distance(hit.point, snapPoint.transform.position);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        snapObject.NearestSnapPoint = snapPoint;

                    }
                }
                objectToSnap.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                snapObject.transform.LookAt(snapObject.NearestSnapPoint.Parent.transform.position);
                SetObjectPosition(snapObject.transform, snapObject.NearestSnapPoint.transform.position, true);
                objectIsSnapping = true;
            }
            else
            {
                objectToSnap.GetComponent<Rigidbody>().constraints = ~RigidbodyConstraints.FreezePosition;
                objectIsSnapping = false;
            }
        }
        private void SetObjectPosition(Transform objectToMove, Vector3 goal, bool snapState)
        {
            if (objectIsSnapping != snapState) return;
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
        private void DisablePositionHandle()
        {
            if (!_positionHandle.IsActive()) return;
            _positionHandle.Complete();
            _positionHandle.Cancel();
        }

        private void SetObjectRotation(Transform objectToMove, Quaternion goal)
        {
            if (objectIsSnapping) return;
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
        private void DisableRotationHandle()
        {
            if (!_rotationHandle.IsActive()) return;
            _rotationHandle.Complete();
            _rotationHandle.Cancel();
        }
        #endregion

        #region room id Method
        private void AssignRoomId(int roomId)
        {
            this.roomId = roomId;
        }
        #endregion
    }
}



