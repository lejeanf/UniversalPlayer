using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer 
{
    public class BroadcastHmdStatus : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;
        
        //public delegate void HmdStatus(bool status);
        //public static HmdStatus hmdStatus;
        
        [SerializeField] private InputActionReference userPresenceInput;
        public static bool hmdCurrentState = false;
        [SerializeField] private bool userPresence = false; // just us
        
        [Header("Broadcasting on:")]
        [SerializeField] private BoolEventChannelSO hmdStateChannel;
        
        private void OnEnable()
        {
            userPresenceInput.action.Enable();
            userPresenceInput.action.started += (ctx) => UpdateHmdState(true);
            userPresenceInput.action.canceled += (ctx) => UpdateHmdState(false);
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Start()
        {
            hmdStateChannel.RaiseEvent(hmdCurrentState);
        }
        private void Unsubscribe()
        {
            userPresenceInput.action.started -= null;
            userPresenceInput.action.canceled -= null;
            userPresenceInput.action.Disable();
        }

        private void UpdateHmdState(bool state)
        {
            if(_isDebug) Debug.Log($"UserPresence: {state}");
            hmdCurrentState = state;
            userPresence = state;
            //hmdStatus?.Invoke(state);
            hmdStateChannel.RaiseEvent(state);
        }

    }
}