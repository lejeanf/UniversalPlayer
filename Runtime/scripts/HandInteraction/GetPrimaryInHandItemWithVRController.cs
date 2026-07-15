using System;
using System.Collections.Generic;
using jeanf.EventSystem;
using jeanf.validationTools;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
{
        public enum IpadState
        {
            Disabled,
            InLeftHand,
            InRightHand,
        }
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
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private HandPoseManager _leftHandPoseManager;
        [SerializeField] private HandPoseManager _rightHandPoseManager;

        [Validation("The pose applied to the hand holding the primary item is required.")]
        [SerializeField] private Pose primaryItemPose;

        [Header("PrimaryItem")]
        [Validation("The primary item (tablet) is required — drawing the item does NOTHING without it. Reference the scene instance (or prefab) here.")]
        public Transform primaryItem;
        [Validation("The primary item state channel is required (shared with PrimaryItemController and the cursor).")]
        [SerializeField] private BoolEventChannelSO _PrimaryItemStateChannel;
        [SerializeField] private StringEventChannelSO _primaryItemStateWithUsedHandChannel;
        [SerializeField] private VoidEventChannelSO _leftGrab;
        [SerializeField] private VoidEventChannelSO _rightGrab;
        [SerializeField] private VoidEventChannelSO _noGrab;
        public static event Action<IpadState> OnIpadStateChanged;
        [Header("Hands Positions")] 
        [SerializeField] private PoseContainer _poseContainer;
        
        [SerializeField] private List<SkinnedMeshRenderer> _hands = new List<SkinnedMeshRenderer>();


        private IpadState _ipadState = IpadState.Disabled;

        // The hand currently posed around the primary item: it holds its pose so
        // ControllerHandPoseDriver does not open the fingers while the item sits in it.
        private HandPoseManager _heldPoseManager;

        private void HoldPose(HandPoseManager manager)
        {
            if (_heldPoseManager == manager) return;
            if (_heldPoseManager != null) _heldPoseManager.ReleasePoseHold();
            _heldPoseManager = manager;
            if (_heldPoseManager != null) _heldPoseManager.AcquirePoseHold();
        }

        private void OnEnable()
        {
            //BlendableHand.AddHand += AddHand;
            //BlendableHand.RemoveHand += RemoveHand;

            drawPrimaryItem_LeftHand.action.performed += OnDrawLeftHand;
            drawPrimaryItem_RightHand.action.performed += OnDrawRightHand;
            _primaryItemStateWithUsedHandChannel.OnEventRaised += SetIpadStateForASpecificHand;
            TakeObject.OnVrGrabSwapPrimaryItem += ReceiveGrabSide;
        }

        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            HoldPose(null);
            _hands.Clear();
            _hands.TrimExcess();

            drawPrimaryItem_LeftHand.action.performed -= OnDrawLeftHand;
            drawPrimaryItem_RightHand.action.performed -= OnDrawRightHand;
            _primaryItemStateWithUsedHandChannel.OnEventRaised -= SetIpadStateForASpecificHand;
            TakeObject.OnVrGrabSwapPrimaryItem -= ReceiveGrabSide;
        }

        private void OnDrawLeftHand(InputAction.CallbackContext _) => SetIpadStateForLeftHand(primaryItemPose.leftHandInfo);
        private void OnDrawRightHand(InputAction.CallbackContext _) => SetIpadStateForRightHand(primaryItemPose.rightHandInfo);


        //private void AddHand(SkinnedMeshRenderer hand)
        //{
        //    if (_hands.Contains(hand)) return;
        //    _hands.Add(hand);
        //    var handPoseManager = hand.transform.parent.transform.parent.GetComponent<HandPoseManager>() == null? hand.transform.parent.GetComponent<HandPoseManager>(): hand.transform.parent.transform.parent.GetComponent<HandPoseManager>();
        //    var handType = handPoseManager.HandType;
        //    if(isDebug) Debug.Log($"handType {handType}");
        //    if(isDebug && handPoseManager) Debug.Log($"handPoseManager {handPoseManager.HandType}");
        //    switch (handType)
        //    {
        //        case HandType.Left:
        //            _leftHand = hand.gameObject.transform;
        //            _leftHandPoseManager = handPoseManager;
        //            break;
        //        case HandType.Right:
        //            _rightHand = hand.gameObject.transform;
        //            _rightHandPoseManager = handPoseManager;
        //            break;
        //        case HandType.None:
        //        default:
        //            throw new ArgumentOutOfRangeException();
        //    }
        //}
        //private void RemoveHand(SkinnedMeshRenderer hand)
        //{
        //    if(_hands.Count > 0 && _hands.Contains(hand)) _hands.Remove(hand);
        //    _leftHand = null;
        //    _rightHand = null;
        //    _leftHandPoseManager = null;
        //    _rightHandPoseManager = null;
        //    _noGrab.RaiseEvent();
        //}

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

        // A hand's draw button: hide the item if it is already showing in THAT hand,
        // otherwise show it there — moving it over from the other hand if needed.
        public void SetIpadStateForLeftHand(HandInfo handInfo)
        {
            if (_ipadState == IpadState.InLeftHand) HidePrimaryItem(_leftHandPoseManager);
            else ShowPrimaryItemInHand(handInfo, isLeft: true);
        }

        public void SetIpadStateForRightHand(HandInfo handInfo)
        {
            if (_ipadState == IpadState.InRightHand) HidePrimaryItem(_rightHandPoseManager);
            else ShowPrimaryItemInHand(handInfo, isLeft: false);
        }

        private void ShowPrimaryItemInHand(HandInfo handInfo, bool isLeft)
        {
            var handTransform = isLeft ? _leftHand : _rightHand;
            var handPose = isLeft ? _leftHandPoseManager : _rightHandPoseManager;
            var otherPose = isLeft ? _rightHandPoseManager : _leftHandPoseManager;

            SetIpadStateForASpecificHand(handInfo, handTransform);
            _ipadState = isLeft ? IpadState.InLeftHand : IpadState.InRightHand;
            (isLeft ? _leftGrab : _rightGrab)?.RaiseEvent();
            HoldPose(handPose);
            if (handPose) handPose.ApplyPose(primaryItemPose);
            // Open the hand it moved away from (the switch case); guarded, never early-returns.
            if (otherPose) otherPose.ApplyDefaultPose();
            _noGrab?.RaiseEvent();
            OnIpadStateChanged?.Invoke(_ipadState);
            _PrimaryItemStateChannel.RaiseEvent(true);
        }

        private void HidePrimaryItem(HandPoseManager fromHand)
        {
            _ipadState = IpadState.Disabled;
            _PrimaryItemStateChannel.RaiseEvent(false);
            OnIpadStateChanged?.Invoke(_ipadState);
            HoldPose(null);
            if (fromHand) fromHand.ApplyDefaultPose();
        }
        public void SetIpadStateForASpecificHand(HandInfo handInfo, Transform parent)
        {
            if(!primaryItem) return;
            primaryItem.SetParent(parent);
            primaryItem.localPosition = handInfo.attachPosition;
            primaryItem.localRotation = handInfo.attachRotation;
        }

        private void ReceiveGrabSide(string str)
        {
            if (!primaryItem) return;
            if (str == "RightHand")
            {
                SetIpadStateForASpecificHand(primaryItemPose.leftHandInfo, _leftHand.transform);
            }
            else if (str == "LeftHand")
            {
                SetIpadStateForASpecificHand(primaryItemPose.rightHandInfo, _rightHand.transform);
            }
        }
        public void SetIpadStateForASpecificHand(string hand)
        {
            if (!primaryItem || _ipadState != IpadState.Disabled) return;
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

