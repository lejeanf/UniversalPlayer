using jeanf.EventSystem;
using jeanf.validationTools;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
namespace jeanf.universalplayer
{
    public class PrimaryItemController : MonoBehaviour
    {
        [SerializeField] private bool useInputAction = true;
        [Validation("The DrawPrimaryItem action is required — pressing 1/dpad-up does nothing without it.")]
        [SerializeField] private InputActionReference drawPrimaryItem;
        [SerializeField] public PlayerInput playerInput;
        [Header("Listening On")]
        [SerializeField] private BoolEventChannelSO loginFieldIsOpened;

        [Header("Broadcasting on:")]
        [Validation("The primary item state channel is required — nothing hears the draw/holster presses without it.")]
        [SerializeField] private BoolEventChannelSO _PrimaryItemStateChannel;
        public static event Action<XRHandsInteractionManager.LastUsedHand, bool> TriggerLastUsedHand;
        private bool primaryItemState = false;
        public bool PrimaryItemState { get { return primaryItemState; } }

        private void OnEnable()
        {
            if (useInputAction) drawPrimaryItem.action.performed += OnDrawPerformed;
            _PrimaryItemStateChannel.OnEventRaised += StateOverride;
            loginFieldIsOpened.OnEventRaised += SetDrawPrimaryItemActionState;
        }

        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            // Named handlers: `-= lambda` removes a fresh instance and silently
            // leaks the subscription.
            if (useInputAction) drawPrimaryItem.action.performed -= OnDrawPerformed;
            _PrimaryItemStateChannel.OnEventRaised -= StateOverride;
            loginFieldIsOpened.OnEventRaised -= SetDrawPrimaryItemActionState;
        }

        private void OnDrawPerformed(InputAction.CallbackContext _) => InvertState();

        public void Reset()
        {
            primaryItemState = false;
            SetPrimaryItemState(primaryItemState);
        }

        public void InvertState()
        {
            primaryItemState = !primaryItemState;
            SetPrimaryItemState(primaryItemState);
        }

        private void SetPrimaryItemState(bool state)
        {
            primaryItemState = state;
            _PrimaryItemStateChannel.RaiseEvent(state);
        }

        private void StateOverride(bool state)
        {
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                TriggerLastUsedHand.Invoke(XRHandsInteractionManager.hand, state);
            }
            primaryItemState = state;
        }

        private void SetDrawPrimaryItemActionState(bool state)
        {
            if (state)
            {
                drawPrimaryItem.action.Disable();
            }

            else
            {
                drawPrimaryItem.action.Enable();
            }
        }
    }
}
