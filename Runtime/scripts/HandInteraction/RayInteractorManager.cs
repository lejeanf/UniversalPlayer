using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(XRInteractorLineVisual))]
public class RayInteractorManager : MonoBehaviour
{
    [Space(10)]
    private XRInteractorLineVisual rayInteractor;
    [SerializeField] private InputActionReference selectAction;
    [Space(10)]
    [SerializeField] private Gradient _white;
    [SerializeField] private Gradient _transparent;

    private void Awake()
    {
        rayInteractor = GetComponent<XRInteractorLineVisual>();
    }

    private void OnEnable()
    {
        selectAction.action.Enable();
        selectAction.action.performed += ctx => SetPreviewRay(rayInteractor, true);
        selectAction.action.canceled += ctx => SetPreviewRay(rayInteractor, false);
    }
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();
    private void Unsubscribe()
    {
        selectAction.action.performed -= null;
        selectAction.action.canceled -= null;
        selectAction.action.Disable();
    }

    public void SetPreviewRay(XRInteractorLineVisual interactorLineVisual, bool state)
    {
        interactorLineVisual.invalidColorGradient = state ? _white : _transparent;
    }

}
