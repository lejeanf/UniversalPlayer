using System;
using System.Diagnostics;
using Codice.CM.Common;
using jeanf.EventSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer
{
    public class GetPrimaryInHandItemWithVRController : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private InputActionReference drawPrimaryItem_LeftHand;
        [SerializeField] private InputActionReference drawPrimaryItem_RightHand;

        [Header("Hands Information")]
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Pose ipadPose;
        
        [Header("PrimaryItem")] 
        public Transform primaryItem;
        [SerializeField] private BoolEventChannelSO _PrimaryItemStateChannel;

        private enum IpadState
        {
            Disabled,
            InLeftHand,
            InRightHand,
        }

        private IpadState _ipadState = IpadState.Disabled;

        private void OnEnable()
        {
            drawPrimaryItem_LeftHand.action.performed += ctx=> SetIpadStateForLeftHand(ipadPose.leftHandInfo);
            drawPrimaryItem_RightHand.action.performed += ctx=> SetIpadStateForRightHand(ipadPose.rightHandInfo);
        }

        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            drawPrimaryItem_LeftHand.action.performed -= null;
            drawPrimaryItem_RightHand.action.performed -= null;
        }


        public void SetIpadStateForLeftHand(HandInfo handInfo)
        {
            if (_ipadState is IpadState.Disabled or IpadState.InRightHand)
            {
                SetIpadStateForASpecificHand(handInfo, leftHand);
                _ipadState = IpadState.InLeftHand;
                _PrimaryItemStateChannel.RaiseEvent(true);
            }
            else
            {
                _ipadState = IpadState.Disabled;
                _PrimaryItemStateChannel.RaiseEvent(false);
            }
        }
        public void SetIpadStateForRightHand(HandInfo handInfo)
        {
            if (_ipadState is IpadState.Disabled or IpadState.InLeftHand)
            {
                SetIpadStateForASpecificHand(handInfo, rightHand);
                _ipadState = IpadState.InRightHand;
                _PrimaryItemStateChannel.RaiseEvent(true);
            }
            else
            {
                _ipadState = IpadState.Disabled;
                _PrimaryItemStateChannel.RaiseEvent(false);
            }
        }
        public void SetIpadStateForASpecificHand(HandInfo handInfo, Transform parent)
        {
            if(!primaryItem) return;
            primaryItem.SetParent(parent);
            primaryItem.localPosition = handInfo.attachPosition;
            primaryItem.localRotation = handInfo.attachRotation;
        }
    }
}

