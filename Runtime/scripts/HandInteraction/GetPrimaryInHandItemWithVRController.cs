using System;
using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace jeanf.vrplayer
{
    public class GetPrimaryInHandItemWithVRController : MonoBehaviour
    {
        [Header("Inputs")]
        [SerializeField] private InputActionReference drawPrimaryItem_LeftHand;
        [SerializeField] private InputActionReference drawPrimaryItem_RightHand;

        [Header("Hands Information")]
        private Transform _leftHand, _rightHand;
        private HandPoseManager _leftHandPoseManager, _rightHandPoseManager;
        [SerializeField] private Pose primaryItemPose;
        
        [Header("PrimaryItem")] 
        public Transform primaryItem;
        [SerializeField] private BoolEventChannelSO _PrimaryItemStateChannel;
        [SerializeField] private VoidEventChannelSO _leftGrab;
        [SerializeField] private VoidEventChannelSO _rightGrab;
        [SerializeField] private VoidEventChannelSO _noGrab;
        
        
        
        [SerializeField] private List<SkinnedMeshRenderer> _hands = new List<SkinnedMeshRenderer>();

        private enum IpadState
        {
            Disabled,
            InLeftHand,
            InRightHand,
        }

        private IpadState _ipadState = IpadState.Disabled;


        private void OnEnable()
        {
            BlendableHand.AddHand += AddHand;
            BlendableHand.RemoveHand += RemoveHand;
            
            drawPrimaryItem_LeftHand.action.performed += ctx=> SetIpadStateForLeftHand(primaryItemPose.leftHandInfo);
            drawPrimaryItem_RightHand.action.performed += ctx=> SetIpadStateForRightHand(primaryItemPose.rightHandInfo);
        }

        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            _hands.Clear();
            _hands.TrimExcess();
            
            BlendableHand.AddHand -= null;
            BlendableHand.RemoveHand -= null;
            drawPrimaryItem_LeftHand.action.performed -= null;
            drawPrimaryItem_RightHand.action.performed -= null;
        }
        
        private void AddHand(SkinnedMeshRenderer hand)
        {
            if (_hands.Contains(hand)) return;
            _hands.Add(hand);
            var handPoseManager = hand.transform.parent.transform.parent.GetComponent<HandPoseManager>();
            var handType = handPoseManager.HandType;
            Debug.Log($"handType {handType}");
            Debug.Log($"handPoseManager {handPoseManager.HandType}");
            switch (handType)
            {
                case HandType.Left:
                    _leftHand = hand.gameObject.transform;
                    _leftHandPoseManager = handPoseManager;
                    break;
                case HandType.Right:
                    _rightHand = hand.gameObject.transform;
                    _rightHandPoseManager = handPoseManager;
                    break;
                case HandType.None:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private void RemoveHand(SkinnedMeshRenderer hand)
        {
            if(_hands.Count > 0 && _hands.Contains(hand)) _hands.Remove(hand);
            _leftHand = null;
            _rightHand = null;
            _leftHandPoseManager = null;
            _rightHandPoseManager = null;
            _noGrab.RaiseEvent();
        }



        public void SetIpadStateForLeftHand(HandInfo handInfo)
        {
            if (_ipadState is IpadState.Disabled or IpadState.InRightHand)
            {
                SetIpadStateForASpecificHand(handInfo, _leftHand);
                _ipadState = IpadState.InLeftHand;
                _PrimaryItemStateChannel.RaiseEvent(true);
                _leftGrab.RaiseEvent();
                if(_leftHandPoseManager) _leftHandPoseManager.ApplyPose(primaryItemPose);
                if(_rightHandPoseManager) _rightHandPoseManager.ApplyDefaultPose();
            }
            else
            {
                _ipadState = IpadState.Disabled;
                _PrimaryItemStateChannel.RaiseEvent(false);
                if (!_leftHandPoseManager) return;
                _leftHandPoseManager.ApplyDefaultPose();
                _noGrab.RaiseEvent();
            }
        }
        public void SetIpadStateForRightHand(HandInfo handInfo)
        {
            if (_ipadState is IpadState.Disabled or IpadState.InLeftHand)
            {
                SetIpadStateForASpecificHand(handInfo, _rightHand.transform);
                _ipadState = IpadState.InRightHand;
                _PrimaryItemStateChannel.RaiseEvent(true);
                _rightGrab.RaiseEvent();
                if(_rightHandPoseManager) _rightHandPoseManager.ApplyPose(primaryItemPose);
                if (!_leftHandPoseManager) return;
                _leftHandPoseManager.ApplyDefaultPose();
                _noGrab.RaiseEvent();
            }
            else
            {
                _ipadState = IpadState.Disabled;
                _PrimaryItemStateChannel.RaiseEvent(false);
                if(_rightHandPoseManager) _rightHandPoseManager.ApplyDefaultPose();
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

