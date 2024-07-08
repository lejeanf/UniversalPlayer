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
        [SerializeField] XRDirectInteractor rightInteractor;
        [SerializeField] XRDirectInteractor leftInteractor;
        [SerializeField] PickableObject objectRightHand;
        [SerializeField] PickableObject objectLeftHand;
        [SerializeField] GameObjectEventChannelSO objectDropped;
        TakeObject takeObject;
        [SerializeField] InputActionReference uiClick;
        public static LastUsedHand hand;
        public enum LastUsedHand
        {
            LeftHand,
            RightHand
        }

        private void Awake()
        {
            takeObject = GetComponent<TakeObject>();
        }

        private void OnEnable()
        {
            uiClick.action.performed += ctx => AssignLastUsedHand(ctx.action, ctx.control);
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            uiClick.action.performed -= ctx => AssignLastUsedHand(ctx.action, ctx.control);
        }

        public void AssignGameObjectInRightHand()
        {
            takeObject._objectInHand = rightInteractor.selectTarget.gameObject.GetComponent<PickableObject>();
        }

        public void AssignGameObjectInLeftHand()
        {
            takeObject._objectInHand = leftInteractor.selectTarget.gameObject.GetComponent<PickableObject>();
        }

        public void RemoveGameObjectInRightHand()
        {
            takeObject._objectInHand = null;
        }

        public void RemoveGameObjectInLeftHand()
        {
            takeObject._objectInHand = null;
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
