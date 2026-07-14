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

        /// <summary>
        /// Draw (true) or holster (false) the primary item. Picking up a PickableObject
        /// whose Carry Slot is Primary calls this, so grabbing the tablet off a table
        /// equips it — the same state every other system already listens to (cursor,
        /// look, tooltips), not a parallel path.
        /// </summary>
        public void SetState(bool state)
        {
            if (primaryItemState == state) return;
            SetPrimaryItemState(state);
        }

        /// <summary>
        /// Drawn (true) / holstered (false), raised on EVERY change however it was
        /// caused — the draw binding, a scenario, or picking the item up. TakeObject
        /// listens so a Primary carry-slot item is actually stowed and brought back,
        /// which is what makes the draw binding work for a plain PickableObject with no
        /// PrimaryItemBehaviour on it.
        /// </summary>
        public static event Action<bool> PrimaryItemStateChanged;

        private void SetPrimaryItemState(bool state)
        {
            primaryItemState = state;
            _PrimaryItemStateChannel.RaiseEvent(state);
            PrimaryItemStateChanged?.Invoke(state);
        }

        private void StateOverride(bool state)
        {
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                TriggerLastUsedHand.Invoke(XRHandsInteractionManager.hand, state);
            }
            if (primaryItemState == state) return;
            primaryItemState = state;
            PrimaryItemStateChanged?.Invoke(state);
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
