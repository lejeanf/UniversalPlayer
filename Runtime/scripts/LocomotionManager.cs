using System;
using System.Collections;
using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(ContinuousMoveProviderBase))]
[RequireComponent(typeof(TeleportationProvider))]
public class LocomotionManager : MonoBehaviour
{
    [Header("Listening on:")]
    [SerializeField] private BoolEventChannelSO continuousMoveStateChannel;
    [SerializeField] private BoolEventChannelSO teleportationMoveStateChannel;

    [SerializeField] private bool invertInputValue = true;
    private ContinuousMoveProviderBase _continuousMoveProvider;
    private TeleportationProvider _teleportationProvider;

    private void Awake()
    {
        _continuousMoveProvider = GetComponent<ContinuousMoveProviderBase>();
        _teleportationProvider = GetComponent<TeleportationProvider>();
    }

    private void OnEnable()
    {
        continuousMoveStateChannel.OnEventRaised += SetContiuousMoveState;
        teleportationMoveStateChannel.OnEventRaised += SetTeleportationsMoveState;
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        continuousMoveStateChannel.OnEventRaised -= null;
        teleportationMoveStateChannel.OnEventRaised -= null;
    }

    private void SetContiuousMoveState(bool state)
    {
        if (invertInputValue) state = !state;
        _continuousMoveProvider.CancelInvoke();    
        _continuousMoveProvider.enabled = state;
    }
    private void SetTeleportationsMoveState(bool state)
    {
        if (invertInputValue) state = !state;
        _teleportationProvider.CancelInvoke();   
        _teleportationProvider.enabled = state;
    }
}
