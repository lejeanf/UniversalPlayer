using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using jeanf.EventSystem;
using Debug = UnityEngine.Debug;
using jeanf.propertyDrawer;
using LitMotion;
using UnityEngine.UIElements;
using System;

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
        Transform objectInHandTransform; //Really useful ?

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
        [Range(.0001f, 0.1f)]
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
        public static event Action<HandType> OnHandGrabbed;
        public static event Action<bool, HandType> OnGrabDeactivateCollider;
        public static event Action<string> OnVrGrabSwapPrimaryItem;
        [Header("Listening On")]
        [SerializeField] IntEventChannelSO roomIdChannelSO;
        [SerializeField] GameObjectEventChannelSO snapEventChannelSO;

        [Header("XR")]
        [SerializeField] NearFarInteractor rightInteractor;
        [SerializeField] NearFarInteractor leftInteractor;

        [Header("Objects in players's hand")]
        PickableObject objectRightHand;
        PickableObject objectLeftHand;
        PickableObject objectInHand;

        bool objectIsSnapping;
        #endregion

        #region Default MonoBehaviour Methods

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();


        private void Subscribe()
        {
            if(takeAction) takeAction.action.performed += ctx => DispatchAction();
            if(scrollAction) scrollAction.action.performed += ctx => UpdateObjectDistance(ctx.ReadValue<float>());
            try
            {
                roomIdChannelSO.OnEventRaised += AssignRoomId;
            }
            catch { }
            SnapObject.OnSnapMove += SetObjectPosition;
            SnapObject.OnSnap += UpdateSnapStatus;
            SnapObject.OnSnapRotate += SetObjectRotation;
        }

        private void Unsubscribe()
        {
            if(takeAction) takeAction.action.performed -= ctx => DispatchAction();
            if(scrollAction) scrollAction.action.performed -= ctx => UpdateObjectDistance(ctx.ReadValue<float>());
            try
            {
                roomIdChannelSO.OnEventRaised -= AssignRoomId;
            }
            catch { }
            DisablePositionHandle();
            DisableRotationHandle();
            SnapObject.OnSnapMove -= SetObjectPosition;
            SnapObject.OnSnap -= UpdateSnapStatus;
            SnapObject.OnSnapRotate -= SetObjectRotation;

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

                    objectInHand = hit.transform.gameObject.GetComponent<PickableObject>();
                    objectInHand.transform.position = mainCamera.transform.position + mainCamera.transform.forward * objectDistance;
                    objectInHand.transform.SetParent(mainCamera.transform);
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
                objectInHand.transform.position = objectInHand.InitialPosition; 
                objectInHand.transform.rotation = objectInHand.InitialRotation;
            }
            objectDropped?.RaiseEvent(objectInHand.gameObject);
            objectInHand.Rigidbody.useGravity = objectInHand.InitialUseGravity;
            objectInHand.Rigidbody.linearDamping = objectInHand.InitialDrag;
            objectInHand.Rigidbody.angularDamping = objectInHand.InitialAngularDrag;
            objectInHand.Rigidbody.freezeRotation = false;
            if (objectInHand.Parent != null)
            {
                objectInHand.transform.SetParent(objectInHand.Parent);
            }
            else
            {
                objectInHand.transform.SetParent(null);
            }
            objectInHand = null;
            //UpdateSnapStatus(false);
        }
        public void AssignGameObjectInRightHand()
        {
            if (rightInteractor.interactablesSelected.Count <= 0) return;
            var selectedInteractable = rightInteractor.interactablesSelected[0]; // Get the first selected interactable
            objectRightHand = selectedInteractable.transform.gameObject.GetComponent<PickableObject>();
            OnGrabDeactivateCollider.Invoke(true, HandType.Right);
            OnVrGrabSwapPrimaryItem("RightHand");
        }

        public void AssignGameObjectInLeftHand()
        {
            if (leftInteractor.interactablesSelected.Count <= 0) return;
            var selectedInteractable = leftInteractor.interactablesSelected[0]; // Get the first selected interactable
            objectLeftHand = selectedInteractable.transform.gameObject.GetComponent<PickableObject>();
            OnGrabDeactivateCollider.Invoke(true, HandType.Left);
            OnVrGrabSwapPrimaryItem("LeftHand");

        }
        public void RemoveGameObjectInRightHand()
        {

            objectDropped?.RaiseEvent(objectRightHand.gameObject);

            objectRightHand = null;
            OnGrabDeactivateCollider.Invoke(false, HandType.Right);

        }

        public void RemoveGameObjectInLeftHand()
        {
            objectDropped?.RaiseEvent(objectLeftHand.gameObject);

            objectLeftHand = null;
            OnGrabDeactivateCollider.Invoke(false, HandType.Left);

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
            if (objectIsSnapping || objectInHand == null) return;
            value *= scrollStep;
            if (_isDebug) Debug.Log($"scroll reading: {value}");
            objectDistance += value;
            if (objectDistance > maxDistance) objectDistance = maxDistance;
            if (objectDistance < minDistance) objectDistance = minDistance;
            objectInHand.transform.position = mainCamera.transform.position + mainCamera.transform.forward * objectDistance;
        }

        private void UpdateSnapStatus(bool snapState)
        {
            objectIsSnapping = snapState;
            if (!snapState && objectInHand != null)
            {
                objectInHand.transform.position = mainCamera.transform.position + mainCamera.transform.forward * objectDistance;
            }
        }
        private void SetObjectPosition(Transform objectToMove, Vector3 goal)
        {
            DisablePositionHandle();
            if (!objectInHand) return;

            if (objectToMove.position == goal)
            {
                return;
            }
            if (!objectToMove)
            {
                if (_isDebug) Debug.Log($"objectToMove is null");

                DisablePositionHandle();
                return;
            }


            if (objectToMove.transform.position == goal)
            {
                return;
            }
            objectToMove.position = Vector3.Lerp(objectToMove.position, goal, 1f);
            //_positionHandle = LMotion.Create(objectToMove.transform.position, goal, sliderMotionDuration)
            //    .Bind(x => objectToMove.transform.position = x)
            //    .AddTo(objectToMove.gameObject);
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
            if (!objectToMove)
            {
                DisableRotationHandle();
                return;
            }
            if (objectToMove.transform.rotation == goal) return;
            if (!objectInHand) return;

            objectToMove.transform.rotation = Quaternion.Lerp(objectToMove.transform.rotation, goal, 1f);
            //_rotationHandle = LMotion.Create(objectToMove.transform.rotation, goal, sliderMotionDuration)
            //    .Bind(x => objectToMove.transform.rotation = x)
            //    .AddTo(objectToMove.gameObject);
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



