using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class TeleportationManager : MonoBehaviour
{
    [SerializeField] private InputActionAsset actionAsset;
    [SerializeField] private XRRayInteractor rightRayInteractor;
    //[SerializeField] private XRRayInteractor leftRayInteractor;
    [SerializeField] private TeleportationProvider provider;
    private InputAction _thumbstick;
    private bool _isActive;

    // Start is called before the first frame update
    void Start()
    {
        rightRayInteractor.enabled = false;
        //Debug.Log($"rayInteractor.enabled : {rightRayInteractor.enabled}");
        /*leftRayInteractor.enabled = false;
        Debug.Log($"rayInteractor.enabled : {leftRayInteractor.enabled}");*/

        var selectRight = actionAsset.FindActionMap("XRI RightHand Locomotion").FindAction("Teleport Select");
        selectRight.Enable();
        selectRight.performed += OnTeleportSelect;

        var activateRight = actionAsset.FindActionMap("XRI RightHand Locomotion").FindAction("Teleport Mode Activate");
        activateRight.Enable();
        activateRight.performed += OnTeleportActivate;

        /*var activateLeft = actionAsset.FindActionMap("XRI LeftHand Locomotion").FindAction("Teleport Mode Activate");
        activateLeft.Enable();
        activateLeft.performed += OnTeleportActivate;*/

        var cancelRight = actionAsset.FindActionMap("XRI RightHand Locomotion").FindAction("Teleport Mode Cancel");
        cancelRight.Enable();
        cancelRight.performed += OnTeleportCancel;

        /*var cancelLeft = actionAsset.FindActionMap("XRI LeftHand Locomotion").FindAction("Teleport Mode Cancel");
        cancelLeft.Enable();
        cancelLeft.performed += OnTeleportCancel;*/

        _thumbstick = actionAsset.FindActionMap("XRI RightHand Locomotion").FindAction("Move");
    }

    // Update is called once per frame
    void Update()
    {
        if (!_isActive) return;

        if(!rightRayInteractor.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            rightRayInteractor.enabled = false;
            _isActive = false;
            return;
        }

        TeleportRequest request = new TeleportRequest()
        {
            destinationPosition = hit.point,
            //destinationRotation = ?,
        };

        provider.QueueTeleportRequest(request);
        rightRayInteractor.enabled = false;
        _isActive = false;

        //Debug.Log($"RequestTeleportation");
    }

    private void OnTeleportSelect(InputAction.CallbackContext context)
    {
        //Debug.Log($"OnTeleportSelect");
        rightRayInteractor.enabled = true;
    }

    private void OnTeleportActivate(InputAction.CallbackContext context)
    {
        //Debug.Log($"OnTeleportActivate");
        //rightRayInteractor.enabled = true;
        _isActive = true;
    }

    private void OnTeleportCancel(InputAction.CallbackContext context)
    {
        //Debug.Log($"OnTeleportCancel");
        rightRayInteractor.enabled = false;
        _isActive = false;
    }
}
