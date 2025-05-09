using System;
using System.Collections;
using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using jeanf.validationTools;

public class LocomotionManager : MonoBehaviour, IDebugBehaviour, IValidatable
{
    public bool isDebug
    { 
        get => _isDebug;
        set => _isDebug = value; 
    }
    [SerializeField] private bool _isDebug = false;

    public bool IsValid { get; private set; }

    [Validation("A reference to InputActionAsset is required.")]
    [SerializeField] private InputActionAsset inputActionAsset;
    [Validation("A reference to bool event channel SO (from UI opening) is required.")]
    [Header("Listening on:")]
    [SerializeField] private BoolEventChannelSO isInputFieldFocused;
    [SerializeField] private BoolEventChannelSO isLoadingScene;


    #if UNITY_EDITOR
    private void OnValidate()
    {
        var invalidObjects = new List<object>();
        var errorMessages = new List<string>();
        var validityCheck = true;

        invalidObjects.Clear();

        if (inputActionAsset == null)
        {
            invalidObjects.Add(inputActionAsset);
            errorMessages.Add("No InputActionAsset set");
            validityCheck = false;
        }

        if (isInputFieldFocused == null)
        {
            invalidObjects.Add(isInputFieldFocused);
            errorMessages.Add("No Bool Event Channel set");
            validityCheck = false;
        }

        if (isLoadingScene == null)
        {
            invalidObjects.Add(isLoadingScene);
            errorMessages.Add("No Bool Event Channel set");
            validityCheck = false;
        }

        IsValid = validityCheck;
        if (!IsValid) return;

        if (IsValid && !Application.isPlaying) return;
        for (int i = 0; i < invalidObjects.Count; i++)
        {
            Debug.LogError($"Error: {errorMessages[i]} ", this.gameObject);
        }
    }
    #endif
    private void OnEnable()
    {

        isInputFieldFocused.OnEventRaised += state => SetInputActivation(state);
        isLoadingScene.OnEventRaised += state => SetInputActivation(state);
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        isInputFieldFocused.OnEventRaised -= state => SetInputActivation(state);
        isLoadingScene.OnEventRaised += state => SetInputActivation(state);
    }


    private void SetInputActivation(bool state)
    {
        if (state)
        {
            foreach (var actionMap in inputActionAsset.actionMaps)
            {
                if (actionMap == inputActionAsset.FindActionMap("FPS"))
                {
                    actionMap.Disable();
                }
            }
        }

        else
        {
            foreach (var actionMap in inputActionAsset.actionMaps)
            {
                if (actionMap == inputActionAsset.FindActionMap("FPS"))
                {
                    actionMap.Enable();
                }
            }
        }
    }
}
