using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.validationTools;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Interactive rebinding. The project's rebind UI asks over
    /// <see cref="PlayerEvents.RebindRequested"/> (hub slot rebindRequested); the result
    /// goes out on <see cref="PlayerEvents.ActionRebound"/> (hub slot actionRebound) —
    /// PlayerInputInterface / PlayerActionManager apply it, the project UI refreshes its labels.
    /// </summary>
    public class ActionRebinder : MonoBehaviour, IValidatable
    {
        #region Debug tools
        public bool IsValid { get; private set; }
        #endregion

        [Validation("A reference to InputActionAsset is required.")]
        [SerializeField] private InputActionAsset inputActionAsset;
        private InputActionRebindingExtensions.RebindingOperation rebindingOperation;
        private const string RebindsKey = "rebinds";

        private void Start()
        {
            string rebinds = PlayerPrefs.GetString(RebindsKey, string.Empty);
            if (string.IsNullOrEmpty(rebinds)) { return; }
            inputActionAsset.LoadBindingOverridesFromJson(rebinds);
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            IsValid = inputActionAsset != null;
            if (!IsValid && Application.isPlaying) Debug.LogError("Error: No InputActionAsset set ", this.gameObject);
        }
        #endif

        private void OnEnable()
        {
            PlayerEvents.RebindRequested += StartRebinding;
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            PlayerEvents.RebindRequested -= StartRebinding;
        }

        private void StartRebinding(InputAction action, int bindingIndex)
        {
            // While the UI waits for a key the cursor must be free. The packaged prefab
            // wired this "UI active" signal to the primary item state, so the same state
            // is raised here (drawn = free cursor, holstered = locked again).
            PlayerEvents.RaisePrimaryItemState(true);
            rebindingOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation => RebindComplete(action, bindingIndex))
                .Start();
        }

        private void RebindComplete(InputAction action, int index)
        {
            rebindingOperation.Dispose();
            PlayerEvents.RaiseActionRebound(action, index);
            PlayerEvents.RaisePrimaryItemState(false);
            SaveBindings();
        }

        private void SaveBindings()
        {
            string rebinds = inputActionAsset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString(RebindsKey, rebinds);
        }
    }
}
