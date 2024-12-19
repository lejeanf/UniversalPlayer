using jeanf.EventSystem;
using jeanf.vrplayer;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

namespace jeanf.vrplayer
{
    public class XRHandsInteractionManager : MonoBehaviour
    {
        [SerializeField] InputActionReference uiClick;
        [SerializeField] InputActionReference xrLeftGrab;
        [SerializeField] InputActionReference xrRightGrab;
        [SerializeField] private InputActionReference drawPrimaryItem_LeftHand;
        [SerializeField] private InputActionReference drawPrimaryItem_RightHand;
        public static LastUsedHand hand;
        public enum LastUsedHand
        {
            LeftHand,
            RightHand
        }

        private void OnEnable()
        {
            uiClick.action.performed += ctx => AssignLastUsedHand(ctx.action, ctx.control);
            xrLeftGrab.action.performed += ctx => AssignLastUsedHand(ctx.action, ctx.control);
            xrRightGrab.action.performed += ctx => AssignLastUsedHand(ctx.action, ctx.control);
            drawPrimaryItem_LeftHand.action.performed += ctx => AssignLastUsedHand(ctx.action, ctx.control);
            drawPrimaryItem_RightHand.action.performed += ctx => AssignLastUsedHand(ctx.action, ctx.control);

        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            uiClick.action.performed -= ctx => AssignLastUsedHand(ctx.action, ctx.control);
            xrLeftGrab.action.performed -= ctx => AssignLastUsedHand(ctx.action, ctx.control);
            xrRightGrab.action.performed -= ctx => AssignLastUsedHand(ctx.action, ctx.control);
            drawPrimaryItem_LeftHand.action.performed -= ctx => AssignLastUsedHand(ctx.action, ctx.control);
            drawPrimaryItem_RightHand.action.performed -= ctx => AssignLastUsedHand(ctx.action, ctx.control);
        }


        public void AssignLastUsedHand(InputAction action, InputControl control)
        {
            InputBinding inputBinding;
            inputBinding = (InputBinding)action.GetBindingForControl(control);
            Debug.Log(inputBinding.effectivePath);
            if (inputBinding.effectivePath.Contains("RightHand"))
            {
                hand = LastUsedHand.RightHand;
            }
            else if (inputBinding.effectivePath.Contains("LeftHand"))
            {
                hand = LastUsedHand.LeftHand;
            }
        }
    }
}
