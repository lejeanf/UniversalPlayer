using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace jeanf.vrplayer 
{
    public class BroadcastHmdStatus : MonoBehaviour
    {
        public delegate void HmdStatus(bool status);
        public static HmdStatus hmdStatus;
        public static bool hmdCurrentState = false;
        [SerializeField] private InputActionReference hmdPresenceInput;
        [SerializeField] private InputSystemUIInputModule inputSystemUIInputModule;

        void OnEnable()
        {
            //hmdPresenceInput.action.performed += ctx => SetHMD(ctx.ReadValue<bool>());
        }
        void OnDestroy() => Unsubscribe();
        void OnDisable() => Unsubscribe();
        void Unsubscribe()
        {
            //hmdPresenceInput.action.performed -= null;
        }

        private void Awake()
        {
            IsHmdOn();
            //SetHMD(false);
        }

        private void FixedUpdate()
        {
            if (hmdCurrentState == IsHmdOn()) return;
            //Debug.Log($"hmdCurrentState: {hmdCurrentState}");
            //isHmdOn();
            SetHMD();
        }

        public bool IsHmdOn()
        {
            bool hmdState = false;
            inputSystemUIInputModule.enabled = true;

            var xrDisplaySubsystems = new List<XRDisplaySubsystem>();
            SubsystemManager.GetInstances<XRDisplaySubsystem>(xrDisplaySubsystems);

            foreach (var xrDisplay in xrDisplaySubsystems)
            {
                if (xrDisplay.running)
                {
                    hmdState = true;
                    inputSystemUIInputModule.enabled = false;
                }
            }

            //hmdStatus?.Invoke(hmdState);
            hmdCurrentState = hmdState;
            
            return hmdState;
        }

        void SetHMD() //bool state
        {
            //Vector3 _playerPos = new Vector3(_playerPosition.transform.localPosition.x, _playerPosition.transform.localPosition.y, _playerPosition.transform.localPosition.z);
            /*Vector3 _playerPos = _playerPosition.transform.position;

            if (!isHmdPresent) // si pas de casque
            {
                //Debug.Log($"Pas de casque.");
                _lastHmdState = true;
                if (isHandsVisible)
                {
                    foreach (GameObject g in hands)
                    {
                        g.SetActive(false);
                    }
                    isHandsVisible = false;
                    leftRay.enabled = rightRay.enabled = leftRayLineRenderer.enabled = rightRayLineRenderer.enabled = false;
                }
            }
            else
            {
                //Debug.Log($"Casque VR en place.");
                _lastHmdState = false;
                //_sphereIpadLeft.transform.position = _positionLeftWithHMD;
                _sphereIpadLeft.transform.position = _playerPos + _positionLeftWithHMD;
                //Quaternion _rotationLeft = _sphereIpadLeft.transform.rotation * _playerPosition.transform.rotation;
                _sphereIpadLeft.transform.rotation = _sphereIpadLeft.transform.rotation * _trackerOffset.transform.rotation;
                //Debug.Log($"_playerPos : {_playerPos}, _positionLeftWithHMD : {_positionLeftWithHMD}, _playerPos + _positionLeftWithHMD : {_playerPos + _positionLeftWithHMD}");
                //_sphereIpadRight.transform.position = _positionRightWithHMD;
                _sphereIpadRight.transform.position = _playerPos + _positionRightWithHMD;
                _sphereIpadRight.transform.rotation = _sphereIpadRight.transform.rotation * _trackerOffset.transform.rotation;
                //Debug.Log($"_playerPos : {_playerPos}, _positionRightWithHMD : {_positionRightWithHMD}, _playerPos + _positionRightWithHMD : {_playerPos + _positionRightWithHMD}");
                if (!isHandsVisible)
                {
                    foreach (GameObject g in hands)
                    {
                        g.SetActive(true);
                    }
                    isHandsVisible = true;
                    leftRay.enabled = rightRay.enabled = leftRayLineRenderer.enabled = rightRayLineRenderer.enabled = true;
                }
            }

            _isHmdActive = isHmdPresent;

            //pubSubPublisher.PublishBoolean(isHmdPresent);
            broadcastHMDstate?.Invoke(isHmdPresent);*/
            hmdStatus?.Invoke(hmdCurrentState);
            //Debug.Log($"hmdCurrentState: {hmdCurrentState}");
        }
    }
}