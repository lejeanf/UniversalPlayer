using System;
using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual))]
public class RayInteractorManager : MonoBehaviour
{
    [Space(10)]
    private UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual rayInteractor;
    [SerializeField] private InputActionReference selectAction;
    [Space(10)]
    [SerializeField] private Gradient _white;
    [SerializeField] private Gradient _transparent;

    private void Awake()
    {
        rayInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual>();
    }

    private void OnEnable()
    {
        selectAction.action.Enable();
        selectAction.action.performed += OnSelectPerformed;
        selectAction.action.canceled += OnSelectCanceled;
    }
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();
    private void Unsubscribe()
    {
        if (selectAction == null) return;
        selectAction.action.performed -= OnSelectPerformed;
        selectAction.action.canceled -= OnSelectCanceled;
        selectAction.action.Disable();
    }

    private void OnSelectPerformed(InputAction.CallbackContext _) => SetPreviewRay(rayInteractor, true);
    private void OnSelectCanceled(InputAction.CallbackContext _) => SetPreviewRay(rayInteractor, false);

    public void SetPreviewRay(UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals.XRInteractorLineVisual interactorLineVisual, bool state)
    {
        interactorLineVisual.invalidColorGradient = state ? _white : _transparent;
    }

}
