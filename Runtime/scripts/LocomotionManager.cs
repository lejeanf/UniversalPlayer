using System;
using System.Collections;
using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using jeanf.validationTools;

public class LocomotionManager : MonoBehaviour, IDebugBehaviour, IValidatable
{
    private const string LogPrefix = "[UniversalPlayer]";

    // These actions SURVIVE the input blackout: they are the player's only way
    // to close the UI that caused it. Disabling the whole FPS map used to kill
    // Escape itself — the menu froze movement and could never be closed again.
    private static readonly string[] AlwaysOnActions = { "MainMenu", "Pause" };

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
    // The two block sources are tracked separately: input stays blocked while
    // EITHER is active (a scene finishing its load must not re-enable WASD
    // under a focused input field, and vice versa).
    private bool _uiFocusBlock;
    private bool _loadingBlock;
    private UnityAction<bool> _onUiFocusChanged;
    private UnityAction<bool> _onLoadingChanged;

    private void OnEnable()
    {
        // Stored once so unsubscribing removes the REAL handlers (a `-= lambda`
        // removes a fresh instance and silently leaks the subscription).
        _onUiFocusChanged = state => { _uiFocusBlock = state; ApplyInputBlock(); };
        _onLoadingChanged = state => { _loadingBlock = state; ApplyInputBlock(); };
        isInputFieldFocused.OnEventRaised += _onUiFocusChanged;
        isLoadingScene.OnEventRaised += _onLoadingChanged;
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        if (_onUiFocusChanged != null) isInputFieldFocused.OnEventRaised -= _onUiFocusChanged;
        if (_onLoadingChanged != null) isLoadingScene.OnEventRaised -= _onLoadingChanged;
    }

    private void ApplyInputBlock()
    {
        var fpsMap = inputActionAsset != null ? inputActionAsset.FindActionMap("FPS") : null;
        if (fpsMap == null)
        {
            Debug.LogWarning($"{LogPrefix} LocomotionManager on '{name}': no FPS action map in " +
                $"'{(inputActionAsset != null ? inputActionAsset.name : "<null>")}' — cannot block/unblock input.", this);
            return;
        }

        var block = _uiFocusBlock || _loadingBlock;
        foreach (var action in fpsMap.actions)
        {
            if (Array.IndexOf(AlwaysOnActions, action.name) >= 0)
            {
                if (!action.enabled) action.Enable(); // Escape/pause always reachable
                continue;
            }
            if (block) action.Disable();
            else action.Enable();
        }

        if (_isDebug) Debug.Log($"{LogPrefix} FPS input {(block ? "BLOCKED" : "unblocked")} " +
            $"(uiFocus: {_uiFocusBlock}, loading: {_loadingBlock}); {string.Join("/", AlwaysOnActions)} stay on.", this);
    }
}
