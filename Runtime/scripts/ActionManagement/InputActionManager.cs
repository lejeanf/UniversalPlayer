using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer 
{
    public class InputActionManager : MonoBehaviour
    {
        [SerializeField] PlayerInput playerInputComponent;
        [SerializeField] InputActionAsset inputActionAsset;


        private void OnEnable()
        {
            SwitchEnabledInputs();
            playerInputComponent.onControlsChanged += ctx => SwitchEnabledInputs();
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            playerInputComponent.onControlsChanged -= ctx => SwitchEnabledInputs();

        }

        private void SwitchEnabledInputs()
        {
            Debug.Log("Testing switch inputs");
        }
    }
}
