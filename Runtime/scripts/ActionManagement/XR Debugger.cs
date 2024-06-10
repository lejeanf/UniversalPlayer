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
            Debug.Log("moving on " + value);
        }

        private void SnapTurn(Vector2 value)
        {
            Debug.Log("snap turn on " + value);
        }
    }
}
