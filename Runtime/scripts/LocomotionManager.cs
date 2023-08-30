using System;
using System.Collections;
using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(ContinuousMoveProviderBase))]
[RequireComponent(typeof(TeleportationProvider))]
public class LocomotionManager : MonoBehaviour, IDebugBehaviour
{
    public bool isDebug
    { 
        get => _isDebug;
        set => _isDebug = value; 
    }
    [SerializeField] private bool _isDebug = false;
    
    [Header("Listening on:")]
    [SerializeField] private BoolEventChannelSO continuousMoveStateChannel;

    [SerializeField] private bool invertInputValue = true;
    private ActionBasedContinuousMoveProvider _continuousMoveProvider;
    private InputActionReference _continuousMoveInputReference;

    private void Awake()
    {
        _continuousMoveProvider = GetComponent<ActionBasedContinuousMoveProvider>();
        
        SetContinuousMoveInputReference();
    }

    private void OnEnable()
    {
        continuousMoveStateChannel.OnEventRaised += SetContiuousMoveState;
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        continuousMoveStateChannel.OnEventRaised -= null;
    }

    public void SetContiuousMoveState(bool state)
    {
        if (invertInputValue) state = !state;
        if (state) SetContinuousMoveInputReference();

        _continuousMoveProvider.enabled = state;
        if(isDebug) Debug.Log($"_continuousMoveProvider state: {state}");
    }
    private void SetContinuousMoveInputReference()
    {
        if(_continuousMoveProvider.leftHandMoveAction.reference != null)
        {
            if(isDebug) Debug.Log($"Re-assigning input reference {_continuousMoveProvider.leftHandMoveAction.reference.name}");

            _continuousMoveInputReference = _continuousMoveProvider.leftHandMoveAction.reference;
            
        }
        else
        {
            if(isDebug) Debug.Log("No Continuous Move Provider Input Action was found on the Left Hand. Please set it on your  Left hand Move Action found on the Continuous Move Provider use the Locomotion Manager");
        }
        _continuousMoveProvider.leftHandMoveAction = new InputActionProperty(_continuousMoveInputReference);
    }
}
