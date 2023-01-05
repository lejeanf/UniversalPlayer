using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer 
{
    public class BroadcastHmdStatus : MonoBehaviour
    {
        public delegate void HmdStatus(bool status);
        public static HmdStatus hmdStatus;
        public static bool hmdCurrentState = false;
        public static bool hmdState = false;
        
        private void Awake()
        {
            isHmdOn();
        }

        private void FixedUpdate()
        {
            if (hmdCurrentState == hmdState) return;
            Debug.Log($"hmdState: {hmdState}, hmdCurrentState: {hmdCurrentState}");
            isHmdOn();
        }

        public static bool isHmdOn()
        {
            hmdState = false;

            var xrDisplaySubsystems = new List<XRDisplaySubsystem>();
            SubsystemManager.GetInstances<XRDisplaySubsystem>(xrDisplaySubsystems);

            foreach (var xrDisplay in xrDisplaySubsystems)
            {
                if (xrDisplay.running)
                {
                    hmdState = true;
                }
            }

            //hmdStatus?.Invoke(hmdState);
            hmdCurrentState = hmdState;
            Debug.Log($"hmdState: {hmdState}");
            return hmdState;
        }

        void setHMD()
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
            hmdStatus?.Invoke(hmdState);
        }
    }
}