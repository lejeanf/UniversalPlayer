using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Serialization;

namespace jeanf.vrplayer 
{
    public class BroadcastHmdStatus : MonoBehaviour
    {
        public delegate void HmdStatus(bool status);
        public static HmdStatus hmdStatus;
        public static bool hmdCurrentState = false;
        [SerializeField] private InputActionReference hmdPresenceInput;
        [SerializeField] private InputSystemUIInputModule inputSystemUIInputModule;
        [SerializeField] private bool userPresence = false;

        private void Awake()
        {
            IsHmdOn();
        }

        private void FixedUpdate()
        {
            if (hmdCurrentState == IsHmdOn()) return;
            SetHMD();
        }

        public bool IsHmdOn()
        {
            var hmdState = false;
            inputSystemUIInputModule.enabled = true;

            var xrDisplaySubsystems = new List<XRDisplaySubsystem>();
            SubsystemManager.GetInstances<XRDisplaySubsystem>(xrDisplaySubsystems);

            foreach (var xrDisplay in xrDisplaySubsystems.Where(xrDisplay => xrDisplay.running))
            {
                hmdState = true;
                inputSystemUIInputModule.enabled = false;
            }
            hmdCurrentState = hmdState;
            userPresence = hmdCurrentState;
            
            return hmdState;
        }

        void SetHMD() //bool state
        {
            hmdStatus?.Invoke(hmdCurrentState);
        }
    }
}