using System;
using jeanf.EventSystem;
using jeanf.universalplayer;
using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.validationTools;

/// <summary>
/// Blocks the FPS action map while a text field has focus or a scene is loading.
/// Both signals arrive over PlayerEvents (hub slots inputFieldFocused / sceneIsLoading).
/// </summary>
public class LocomotionManager : MonoBehaviour, IDebugBehaviour, IValidatable
{
    private const string LogPrefix = "[UniversalPlayer]";

    // These actions SURVIVE the input blackout: they are the player's only way
    // to close the UI that caused it. Disabling the whole FPS map used to kill
    // Escape itself — the menu froze movement and could never be closed again.
    // Deliberately NOT Pause: typing a 'p' into a focused input field (tablet
    // login) must not pause the game.
    private static readonly string[] AlwaysOnActions = { "MainMenu" };

    public bool isDebug
    {
        get => _isDebug;
        set => _isDebug = value;
    }
    [SerializeField] private bool _isDebug = false;

    public bool IsValid { get; private set; }

    [Validation("A reference to InputActionAsset is required.")]
    [SerializeField] private InputActionAsset inputActionAsset;


    #if UNITY_EDITOR
    private void OnValidate()
    {
        IsValid = inputActionAsset != null;
        if (!IsValid && Application.isPlaying) Debug.LogError("Error: No InputActionAsset set ", this.gameObject);
    }
    #endif
    // The two block sources are tracked separately: input stays blocked while
    // EITHER is active (a scene finishing its load must not re-enable WASD
    // under a focused input field, and vice versa).
    private bool _uiFocusBlock;
    private bool _loadingBlock;

    private void OnEnable()
    {
        PlayerEvents.InputFieldFocusChanged += OnUiFocusChanged;
        PlayerEvents.SceneLoadingChanged += OnLoadingChanged;
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        PlayerEvents.InputFieldFocusChanged -= OnUiFocusChanged;
        PlayerEvents.SceneLoadingChanged -= OnLoadingChanged;
    }

    private void OnUiFocusChanged(bool state) { _uiFocusBlock = state; ApplyInputBlock(); }
    private void OnLoadingChanged(bool state) { _loadingBlock = state; ApplyInputBlock(); }

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
