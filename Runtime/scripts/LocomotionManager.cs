using System;
using System.Collections;
using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(ContinuousMoveProviderBase))]
[RequireComponent(typeof(TeleportationProvider))]
public class LocomotionManager : MonoBehaviour
{
    [Header("Listening on:")]
    [SerializeField] private BoolEventChannelSO continuousMoveStateChannel;

    [SerializeField] private bool invertInputValue = true;
    private ActionBasedContinuousMoveProvider _continuousMoveProvider;
    private LocomotionSystem _locomotionSystem;
    
    
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

    private void SetContiuousMoveState(bool state)
    {
        if (invertInputValue) state = !state;
        if (state) SetContinuousMoveInputReference();
        _continuousMoveProvider.enabled = state;
    }
    private void SetContinuousMoveInputReference()
    {
        if(_continuousMoveProvider.leftHandMoveAction.reference != null)
        {
            _continuousMoveInputReference = _continuousMoveProvider.leftHandMoveAction.reference;
        }
        else
        {
            Debug.Log("No Continuous Move Provider Input Action was found on the Left Hand. Please set it on your  Left hand Move Action found on the Continuous Move Provider use the Locomotion Manager");
        }
    }
}
