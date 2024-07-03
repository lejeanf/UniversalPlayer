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

        [SerializeField] StringEventChannelSO activeControlScheme;
        [SerializeField] public PlayerInput playerInput;


        //public delegate void HmdStatus(bool status);
        //public static HmdStatus hmdStatus;

        [SerializeField] private InputActionReference userPresenceInput;
        public static bool hmdCurrentState = false;
        [SerializeField] private bool userPresence = false; // just us

        [Header("Broadcasting on:")]
        [SerializeField] private BoolEventChannelSO hmdStateChannel;

        private void OnEnable()
        {
            activeControlScheme.OnEventRaised += ctx => UpdateHmdState(ctx);
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Start()
        {
            hmdStateChannel.RaiseEvent(GetHMDState());
        }

        private void Unsubscribe()
        {
            activeControlScheme.OnEventRaised -= ctx => UpdateHmdState(ctx);

        }

        private void UpdateHmdState(string controlScheme)
        {
            if (controlScheme == "XR")
            {
                hmdCurrentState = true;
                hmdStateChannel.RaiseEvent(hmdCurrentState);
            }
            else
            {
                hmdCurrentState = false;
                hmdStateChannel.RaiseEvent(hmdCurrentState);

            }
        }

        private bool GetHMDState()
        {
            if (playerInput.currentControlScheme == "XR")
            {
                return true;
            }
            else { return false; }
        }

    }
}