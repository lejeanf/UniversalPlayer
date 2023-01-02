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

        public static bool isHmdOn()
        {
            bool hmdState = false;

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
            return hmdState;
        }
    }
}