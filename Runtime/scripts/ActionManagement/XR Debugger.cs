using System;
using jeanf.validationTools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
    public class XRDebugger : MonoBehaviour
    {
        [Validation("Move action is required — subscribed unguarded on enable (a null reference throws).")]
        [SerializeField] InputActionReference moveAction;
        [Validation("Snap-turn action is required — subscribed unguarded on enable (a null reference throws).")]
        [SerializeField] InputActionReference snapTurnAction;
        [Validation("PlayerInput is required — its control scheme is read unguarded on every move/snap-turn log (a null reference throws).")]
        [SerializeField] PlayerInput PlayerInput;

        private Action<InputAction.CallbackContext> _onMove;
        private Action<InputAction.CallbackContext> _onSnapTurn;

        private void OnEnable()
        {
            _onMove ??= ctx => Move(ctx.ReadValue<Vector2>());
            _onSnapTurn ??= ctx => SnapTurn(ctx.ReadValue<Vector2>());
            moveAction.action.performed += _onMove;
            snapTurnAction.action.performed += _onSnapTurn;
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_onMove != null) moveAction.action.performed -= _onMove;
            if (_onSnapTurn != null) snapTurnAction.action.performed -= _onSnapTurn;
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
