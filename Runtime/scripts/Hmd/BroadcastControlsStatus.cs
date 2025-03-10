using jeanf.EventSystem;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer
{
    public class BroadcastControlsStatus : MonoBehaviour, IDebugBehaviour
    {
        public delegate void SendControlSchemeDelegate(ControlScheme controlScheme);
        public static SendControlSchemeDelegate SendControlScheme;
        
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        public enum ControlScheme
        {
            KeyboardMouse,
            XR,
            Gamepad
        }

        public static ControlScheme controlScheme;

        [SerializeField] public PlayerInput playerInput;

        [Header("Broadcasting on:")]
        [SerializeField] private BoolEventChannelSO hmdStateChannel;
        [SerializeField] VoidEventChannelSO activeControlScheme;


        private void Awake()
        {
            SetCurrentControlSchemeOnSwitch();
        }
        private void OnEnable()
        {
            playerInput.onControlsChanged += ctx => SetCurrentControlSchemeOnSwitch();
        }

        private void SetCurrentControlSchemeOnSwitch()
        {
            switch (playerInput.currentControlScheme)
            {
                case "Keyboard&Mouse":
                    controlScheme = ControlScheme.KeyboardMouse;
                    activeControlScheme.RaiseEvent();
                    break;
                case "Gamepad":
                    controlScheme = ControlScheme.Gamepad;
                    activeControlScheme.RaiseEvent();
                    break;
                case "XR":
                    controlScheme = ControlScheme.XR;
                    activeControlScheme.RaiseEvent();
                    break;
            }

            SendControlScheme?.Invoke(controlScheme);

        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Start()
        {
            hmdStateChannel.RaiseEvent(GetHMDState());
        }

        private void Unsubscribe()
        {
            playerInput.onControlsChanged -= ctx => SetCurrentControlSchemeOnSwitch();
        }

        public bool GetHMDState()
        {
            if (controlScheme == ControlScheme.XR)
            {
                return true;
            }
            else { return false; }
        }

    }
}