using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer
{
    public class XRDebugger : MonoBehaviour
    {
        [SerializeField] InputActionReference moveAction;
        [SerializeField] InputActionReference snapTurnAction;
        [SerializeField] PlayerInput PlayerInput;



        private void OnEnable()
        {
            moveAction.action.performed += ctx => Move(ctx.ReadValue<Vector2>());
            moveAction.action.performed += ctx => SnapTurn(ctx.ReadValue<Vector2>());

        }

        private void OnDisable()
        {
            moveAction.action.performed -= ctx => Move(ctx.ReadValue<Vector2>());
            snapTurnAction.action.performed -= ctx => SnapTurn(ctx.ReadValue<Vector2>());


        }

        private void Move(Vector2 value)
        {
            Debug.Log($"Moving on {value}, the playerInput's active controlScheme is {PlayerInput.currentControlScheme} and the current action maps are {PlayerInput.currentActionMap}");
        }

        private void SnapTurn(Vector2 value)
        {
            Debug.Log($"Snap Turn {value}, the playerInput's active controlScheme is {PlayerInput.currentControlScheme} and the current action maps are {PlayerInput.currentActionMap}");
        }
    }
}
