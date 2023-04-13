using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer 
{
    public class BroadcastHmdStatus : MonoBehaviour
    {
        public delegate void HmdStatus(bool status);
        public static HmdStatus hmdStatus;
        
        [SerializeField] private InputActionReference userPresenceInput;
        public static bool hmdCurrentState = false;
        [SerializeField] private bool userPresence = false; // just used as visual feedback in unity Editor
        [SerializeField] private bool isDebug = false;
        
        private void OnEnable()
        {
            userPresenceInput.action.Enable();
            userPresenceInput.action.started += (ctx) => UpdateHmdState(true);
            userPresenceInput.action.canceled += (ctx) => UpdateHmdState(false);
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            userPresenceInput.action.started -= null;
            userPresenceInput.action.canceled -= null;
            userPresenceInput.action.Disable();
        }

        private void UpdateHmdState(bool state)
        {
            if(isDebug) Debug.Log($"UserPresence: {state}");
            hmdCurrentState = state;
            userPresence = state;
            hmdStatus?.Invoke(state);
        }
    }
}