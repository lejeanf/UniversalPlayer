using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(LocomotionSystem))]
public class TeleportationManager : MonoBehaviour
{
    [SerializeField] private InputActionAsset actionAsset;
    [SerializeField] private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rightRayInteractor;
    
    private UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider provider;
    private InputAction _thumbstick;
    private bool _isActive;

    Vector3 m_ReticlePos;
    Vector3 m_ReticleNormal;
    int m_EndPositionInLine;

    private void Awake()
    {
        provider = this.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();
    }

    private void Start()
    {
        rightRayInteractor.enabled = false;

        var selectRight = actionAsset.FindActionMap("XRI RightHand Locomotion").FindAction("Teleport Select");
        selectRight.Enable();
        selectRight.performed += OnTeleportSelectPerformed;
        selectRight.canceled += OnTeleportSelectCanceled;

        var cancelRight = actionAsset.FindActionMap("XRI RightHand Locomotion").FindAction("Teleport Mode Cancel");
        cancelRight.Enable();
        cancelRight.performed += OnTeleportCancel;
    }
    
    private void Update()
    {
        if (!_isActive) return;
        
        if (!rightRayInteractor.TryGetHitInfo(out m_ReticlePos, out m_ReticleNormal, out m_EndPositionInLine, out var isValidTarget))
        {
            rightRayInteractor.enabled = false;
            _isActive = false;
            return;
        }

        var request = new UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportRequest()
        {
            destinationPosition = m_ReticlePos,
        };
        
        if (isValidTarget)
        {
            provider.QueueTeleportRequest(request);
        }
        
        DisableTeleportation();
    }

    public void DisableTeleportation()
    {
        rightRayInteractor.enabled = false;
        _isActive = false;
    }

    private void OnTeleportSelectPerformed(InputAction.CallbackContext context)
    {
        rightRayInteractor.enabled = true;
    }

    private void OnTeleportSelectCanceled(InputAction.CallbackContext context)
    {
        _isActive = true;
    }

    private void OnTeleportCancel(InputAction.CallbackContext context)
    {
        DisableTeleportation();
    }
}
