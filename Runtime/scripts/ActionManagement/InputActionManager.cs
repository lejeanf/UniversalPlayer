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



        private void Update()
        {
            Debug.Log(playerInputComponent.currentControlScheme);
        }
    }
}
