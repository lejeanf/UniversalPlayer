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
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            uiClick.action.performed -= ctx => AssignLastUsedHand(ctx.action, ctx.control);
            xrLeftGrab.action.performed -= ctx => AssignLastUsedHand(ctx.action, ctx.control);
            xrRightGrab.action.performed -= ctx => AssignLastUsedHand(ctx.action, ctx.control);

        }


        public void AssignLastUsedHand(InputAction action, InputControl control)
        {
            InputBinding inputBinding;
            inputBinding = (InputBinding)action.GetBindingForControl(control);
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
