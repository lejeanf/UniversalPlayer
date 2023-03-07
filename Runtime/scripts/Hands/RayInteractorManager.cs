using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class RayInteractorManager : MonoBehaviour
{
    [SerializeField] private InputActionAsset actionAsset;
    [SerializeField] private XRInteractorLineVisual rightInteractorLineVisual, leftInteractorLineVisual;
    [SerializeField] private Gradient _white;
    [SerializeField] private Gradient _transparent;

    // Start is called before the first frame update
    void Start()
    {
        var selectRight = actionAsset.FindActionMap("XRI RightHand Interaction").FindAction("Preview Raycast");
        selectRight.Enable();
        selectRight.performed += RightPreviewRayEnable;
        selectRight.canceled += RightPreviewRayDisable;

        var selectLeft = actionAsset.FindActionMap("XRI LeftHand Interaction").FindAction("Preview Raycast");
        selectLeft.Enable();
        selectLeft.performed += LeftPreviewRayEnable;
        selectLeft.canceled += LeftPreviewRayDisable;
    }

    public void RightPreviewRayEnable(InputAction.CallbackContext context)
    {
        rightInteractorLineVisual.invalidColorGradient = _white;
    }

    public void RightPreviewRayDisable(InputAction.CallbackContext context)
    {
        rightInteractorLineVisual.invalidColorGradient = _transparent;
    }

    public void LeftPreviewRayEnable(InputAction.CallbackContext context)
    {
        leftInteractorLineVisual.invalidColorGradient = _white;
    }

    public void LeftPreviewRayDisable(InputAction.CallbackContext context)
    {
        leftInteractorLineVisual.invalidColorGradient = _transparent;
    }
}
