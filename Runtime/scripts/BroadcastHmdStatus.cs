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
        public bool hmdCurrentState = false;
        public bool hmdState = false;
        
        private void Awake()
        {
            isHmdOn();
        }

        private void FixedUpdate()
        {
            if (hmdState == hmdCurrentState) return;
            Debug.Log($"hmdState: {hmdState}, hmdCurrentState: {hmdCurrentState}");
            isHmdOn();
        }

        public bool isHmdOn()
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

            hmdStatus?.Invoke(hmdState);
            hmdCurrentState = hmdState;
            Debug.Log($"hmdState: {hmdState}");
            return hmdState;
        }
    }
}