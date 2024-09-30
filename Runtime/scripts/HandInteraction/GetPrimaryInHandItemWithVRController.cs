using System;
using System.Collections.Generic;
using jeanf.EventSystem;
using jeanf.propertyDrawer;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

namespace jeanf.vrplayer
{
    public class GetPrimaryInHandItemWithVRController : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;
        [Header("Inputs")]
        [SerializeField] private InputActionReference drawPrimaryItem_LeftHand;
        [SerializeField] private InputActionReference drawPrimaryItem_RightHand;
        

        [Header("Hands Information")]
        private Transform _leftHand, _rightHand;
        [ReadOnly][SerializeField] private HandPoseManager _leftHandPoseManager, _rightHandPoseManager;
        [SerializeField] private Pose primaryItemPose;
        
        [Header("PrimaryItem")] 
        public Transform primaryItem;
        [SerializeField] private BoolEventChannelSO _PrimaryItemStateChannel;
        [SerializeField] private StringEventChannelSO _primaryItemStateWithUsedHandChannel;
        [SerializeField] private VoidEventChannelSO _leftGrab;
        [SerializeField] private VoidEventChannelSO _rightGrab;
        [SerializeField] private VoidEventChannelSO _noGrab;

        [Header("Hands Positions")] 
        [SerializeField] private PoseContainer _poseContainer;
        
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
            _primaryItemStateWithUsedHandChannel.OnEventRaised += ctx => SetIpadStateForASpecificHand(ctx);
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
            _primaryItemStateWithUsedHandChannel.OnEventRaised -= ctx => SetIpadStateForASpecificHand(ctx);

        }

        private void AddHand(SkinnedMeshRenderer hand)
        {
            if (_hands.Contains(hand)) return;
            _hands.Add(hand);
            var handPoseManager = hand.transform.parent.transform.parent.GetComponent<HandPoseManager>();
            var handType = handPoseManager.HandType;
            if(isDebug) Debug.Log($"handType {handType}");
            if(isDebug && handPoseManager) Debug.Log($"handPoseManager {handPoseManager.HandType}");
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

        public InputActionReference GetActiveHand()
        {
            if (_ipadState is IpadState.InLeftHand)
            {
                return drawPrimaryItem_LeftHand;
            }
            else if (_ipadState is IpadState.InRightHand)
            {
                return drawPrimaryItem_RightHand;
            }
            else
            {
                return null;
            }
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
                //_poseContainer.SetAttachTransform_Left();
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
                //_poseContainer.SetAttachTransform_Right();
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

        public void SetIpadStateForASpecificHand(string hand)
        {
            if (!primaryItem) return;
            if (hand.Contains("LeftHand"))
            {
                SetIpadStateForRightHand(primaryItemPose.rightHandInfo);
            }
            else if (hand.Contains("RightHand"))
            {
                SetIpadStateForLeftHand(primaryItemPose.leftHandInfo);
            }

        }
    }
}

