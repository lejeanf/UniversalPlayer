using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.EventSystem;

namespace jeanf.vrplayer
{
    public class HandleControlSchemeChangeAndSendEvents : MonoBehaviour
    {
        PlayerInput playerInput;

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
        }
    }
}
