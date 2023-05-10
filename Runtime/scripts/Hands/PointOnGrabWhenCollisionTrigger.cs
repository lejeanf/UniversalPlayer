using System;
using jeanf.EventSystem;
using UnityEngine;

public class PointOnGrabWhenCollisionTrigger : MonoBehaviour, IDebugBehaviour
{
    public bool isDebug
    {
        get => _isDebug;
        set => _isDebug = value;
    }

    [SerializeField] private bool _isDebug = false;
    
    [Space(10)]
    [SerializeField] private int grabCount = 0;
    [SerializeField] private int handsInDetectionzone = 0;
    
    [Header("Listening on:")]
    [SerializeField] private BoolEventChannelSO _LeftHandState = default;
    [SerializeField] private BoolEventChannelSO _RightHandState = default;
    [SerializeField] private VoidEventChannelSO _HandDetectedEvent = default;
    [SerializeField] private VoidEventChannelSO _HandDisapearedEvent = default;

    [Header("Broadcasting on:")]
    [SerializeField] private IntEventChannelSO grabCountChannelSO;
    [SerializeField] private  bool setPointingPoseOnOppositeHandGrab = false;
    [DrawIf("setPointingPoseOnOppositeHandGrab", true, ComparisonType.Equals)]
    [SerializeField] private BoolEventChannelSO leftHandIsPointingChannelSO;
    [DrawIf("setPointingPoseOnOppositeHandGrab", true, ComparisonType.Equals)]
    [SerializeField] private BoolEventChannelSO rightHandIsPointingChannelSO;

    [SerializeField] private bool leftHandGrabState = false;
    [SerializeField] private bool rightHandGrabState = false;

    private void OnEnable()
    {
        if (_LeftHandState != null)
            _LeftHandState.OnEventRaised += RegisterLeftHandState;
        if (_RightHandState != null)
            _RightHandState.OnEventRaised += RegisterRightHandState;
        
        if (_HandDetectedEvent != null)
            _HandDetectedEvent.OnEventRaised += HandDetectedInPointingZone;
        if (_HandDisapearedEvent != null)
            _HandDisapearedEvent.OnEventRaised += HandDisapearedInPointingZone;
    }

    private void OnDisable()
    {
        if (_LeftHandState != null)
            _LeftHandState.OnEventRaised -= null;
        if (_RightHandState != null)
            _RightHandState.OnEventRaised -= null;
        
        if (_HandDetectedEvent != null)
            _HandDetectedEvent.OnEventRaised -= null;
        if (_HandDisapearedEvent != null)
            _HandDisapearedEvent.OnEventRaised -= null;
    }

    private void CountTotalGrabsInAction(bool value)
    {
        if (isDebug) Debug.Log($"Grab event received: {value}");

        grabCount = value ? grabCount += 1 : grabCount -= 1;
        SendGrabCount(grabCount);
    }

    private void RegisterLeftHandState(bool value)
    {
        leftHandGrabState = value;
        CountTotalGrabsInAction(value);
    }

    private void RegisterRightHandState(bool value)
    {
        rightHandGrabState = value;
        CountTotalGrabsInAction(value);
    }

    private void SendGrabCount(int value)
    {
        grabCountChannelSO.RaiseEvent(value);
    }

    private void HandDetectedInPointingZone()
    {
        handsInDetectionzone += 1;
        
        SetPointingPose();
    }
    private void HandDisapearedInPointingZone()
    {
        handsInDetectionzone -= 1;
        if (handsInDetectionzone < 0) handsInDetectionzone = 0;
        
        SetPointingPose();
    }

    private void SetPointingPose()
    {
        if(!setPointingPoseOnOppositeHandGrab) return;
        if (grabCount >= 1 && handsInDetectionzone > 1)
        {
            if(_isDebug) Debug.Log("grab > 1 handInDetectZone > 1");
            if (leftHandGrabState)
            {
                if(_isDebug) Debug.Log("setting RIGHT hand pointing to TRUE");
                rightHandIsPointingChannelSO.RaiseEvent(true);
            }

            else if (rightHandGrabState)
            {
                if(_isDebug) Debug.Log("setting LEFT hand pointing to TRUE");
                leftHandIsPointingChannelSO.RaiseEvent(true);
            }

        }
        else
        {
            if(_isDebug) Debug.Log("setting RIGHT hand pointing to FALSE");
            rightHandIsPointingChannelSO.RaiseEvent(false);
            if(_isDebug) Debug.Log("setting LEFT hand pointing to FALSE");
            leftHandIsPointingChannelSO.RaiseEvent(false);
        }
    }
}