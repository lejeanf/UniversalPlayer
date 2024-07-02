using jeanf.EventSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer
{
    public class ControlSchemeBroadcaster : MonoBehaviour
    {
        PlayerInput playerInput;
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        [Header("Broadcasting On")]
        [SerializeField] StringEventChannelSO activeControlSchemeChannel;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            playerInput.onControlsChanged += ctx => SendCurrentControlSchemeOnSwitch();
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            playerInput.onControlsChanged -= ctx => SendCurrentControlSchemeOnSwitch();
        }

        private void SendCurrentControlSchemeOnSwitch()
        {
            activeControlSchemeChannel.RaiseEvent(playerInput.currentControlScheme);
            if (isDebug)
            {
                Debug.Log($"Current Contrl Scheme is {playerInput.currentControlScheme}");
            }
        }
    }

}
