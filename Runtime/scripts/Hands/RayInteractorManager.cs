using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

public class RayInteractorManager : MonoBehaviour
{
    [SerializeField] private InputActionAsset actionAsset;
    [SerializeField] private XRInteractorLineVisual rightInteractorLineVisual, leftInteractorLineVisual;
    [SerializeField] private Gradient _white;
    [SerializeField] private Gradient _transparent;

    [SerializeField] private InputActionReference selectLeft;
    [SerializeField] private InputActionReference selectRight;

    private void Start()
    {
        actionAsset.Enable();
        selectRight.action.performed += RightPreviewRayEnable;
        selectRight.action.canceled += RightPreviewRayDisable;
        selectLeft.action.performed += LeftPreviewRayEnable;
        selectLeft.action.canceled += LeftPreviewRayDisable;
    }

    private void OnDestroy()
    {
        selectRight.action.performed -= null;
        selectRight.action.canceled -= null;
        selectLeft.action.performed -= null;
        selectLeft.action.canceled -= null;
        actionAsset.Disable();
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
