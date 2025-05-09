using jeanf.EventSystem;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
namespace jeanf.universalplayer
{
    public class PrimaryItemController : MonoBehaviour
    {
        [SerializeField] private bool useInputAction = true; 
        [SerializeField] private InputActionReference drawPrimaryItem;
        [SerializeField] public PlayerInput playerInput;
        [Header("Listening On")]
        [SerializeField] private BoolEventChannelSO loginFieldIsOpened;

        [Header("Broadcasting on:")]
        [SerializeField] private BoolEventChannelSO _PrimaryItemStateChannel;
        public static event Action<XRHandsInteractionManager.LastUsedHand, bool> TriggerLastUsedHand;
        private bool primaryItemState = false;
        public bool PrimaryItemState { get { return primaryItemState; } }

        private void OnEnable()
        {
            if(useInputAction) drawPrimaryItem.action.performed += ctx=> InvertState();
            _PrimaryItemStateChannel.OnEventRaised += StateOverride;

            loginFieldIsOpened.OnEventRaised += state => SetDrawPrimaryItemActionState(state);

        }

        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            if(useInputAction) drawPrimaryItem.action.performed -= ctx=> InvertState();
            _PrimaryItemStateChannel.OnEventRaised -= StateOverride;
            loginFieldIsOpened.OnEventRaised -= state => SetDrawPrimaryItemActionState(state);

        }

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
