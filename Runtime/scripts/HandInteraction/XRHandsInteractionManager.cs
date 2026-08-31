using System;
using jeanf.EventSystem;
using jeanf.validationTools;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace jeanf.universalplayer
{
    public class XRHandsInteractionManager : MonoBehaviour
    {
        [Validation("UI-click action is required — it is subscribed unguarded at startup (a null reference throws).")]
        [SerializeField] InputActionReference uiClick;
        [Validation("Left grab action is required — it is subscribed unguarded at startup (a null reference throws).")]
        [SerializeField] InputActionReference xrLeftGrab;
        [Validation("Right grab action is required — it is subscribed unguarded at startup (a null reference throws).")]
        [SerializeField] InputActionReference xrRightGrab;
        [Validation("Left draw-primary-item action is required — it is subscribed unguarded at startup (a null reference throws).")]
        [SerializeField] private InputActionReference drawPrimaryItem_LeftHand;
        [Validation("Right draw-primary-item action is required — it is subscribed unguarded at startup (a null reference throws).")]
        [SerializeField] private InputActionReference drawPrimaryItem_RightHand;
        public static LastUsedHand hand;
        public enum LastUsedHand
        {
            LeftHand,
            RightHand
        }

        private Action<InputAction.CallbackContext> _onActionPerformed;

        private void OnEnable()
        {
            _onActionPerformed ??= ctx => AssignLastUsedHand(ctx.action, ctx.control);
            uiClick.action.performed += _onActionPerformed;
            xrLeftGrab.action.performed += _onActionPerformed;
            xrRightGrab.action.performed += _onActionPerformed;
            drawPrimaryItem_LeftHand.action.performed += _onActionPerformed;
            drawPrimaryItem_RightHand.action.performed += _onActionPerformed;
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_onActionPerformed == null) return;
            uiClick.action.performed -= _onActionPerformed;
            xrLeftGrab.action.performed -= _onActionPerformed;
            xrRightGrab.action.performed -= _onActionPerformed;
            drawPrimaryItem_LeftHand.action.performed -= _onActionPerformed;
            drawPrimaryItem_RightHand.action.performed -= _onActionPerformed;
        }


        public void AssignLastUsedHand(InputAction action, InputControl control)
        {
            InputBinding inputBinding;
            inputBinding = (InputBinding)action.GetBindingForControl(control);
            if (inputBinding.effectivePath.Contains("RightHand"))
            {
                if (action == drawPrimaryItem_RightHand.action)
                {
                    hand = LastUsedHand.LeftHand;
                }
                else
                {
                    hand = LastUsedHand.RightHand;
                }
            }
            else if (inputBinding.effectivePath.Contains("LeftHand"))
            {
                if (action == drawPrimaryItem_LeftHand.action)
                {
                    hand = LastUsedHand.RightHand;
                }
                else
                {
                    hand = LastUsedHand.LeftHand;
                }
            }
        }
    }
}
