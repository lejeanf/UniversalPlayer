using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.EventSystem;
using jeanf.validationTools;

namespace jeanf.vrplayer
{
    public class ActionRebinder : MonoBehaviour, IValidatable
    {

        #region Debug tools
        public bool IsValid { get; private set; }
        #endregion

        [Validation("A reference to InputActionAsset is required.")]
        [SerializeField] private InputActionAsset inputActionAsset;
        private InputActionRebindingExtensions.RebindingOperation rebindingOperation;
        private const string RebindsKey = "rebinds";

        #region EventChannels
        [Validation("A reference to an ActionRebindEventChannelSO is required.")]
        [SerializeField] private ActionRebindEventChannelSO rebindedActionSenderSO;
        [Validation("A reference to an ActionRebindEventChannelSO is required.")]
        [SerializeField] private ActionRebindEventChannelSO actionRebindListenerSO;
        [Validation("A reference to BoolEventChannelSO is required.")]
        [SerializeField] private BoolEventChannelSO uiActivationEventChannelSO;
        #endregion

        private void Start()
        {
            string rebinds = PlayerPrefs.GetString(RebindsKey, string.Empty);
            if (string.IsNullOrEmpty(rebinds)) { return; }
            inputActionAsset.LoadBindingOverridesFromJson(rebinds);
        }


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

            if (rebindedActionSenderSO == null)
            {
                invalidObjects.Add(rebindedActionSenderSO);
                errorMessages.Add("No ActionRebindEventChannelSO set");
                validityCheck = false;
            }

            if (actionRebindListenerSO == null)
            {
                invalidObjects.Add(actionRebindListenerSO);
                errorMessages.Add("No ActionRebindEventChannelSO set");
                validityCheck = false;
            }

            if (uiActivationEventChannelSO == null)
            {
                invalidObjects.Add(uiActivationEventChannelSO);
                errorMessages.Add("No BoolEventChannelSO set");
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
            actionRebindListenerSO.OnEventRaised += (input, index) => StartRebinding(input, index);
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            actionRebindListenerSO.OnEventRaised -= (input, index) => StartRebinding(input, index);

        }

        private void StartRebinding(InputAction action, int bindingIndex)
        {
            uiActivationEventChannelSO.RaiseEvent(true);
            rebindingOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("Mouse")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation => RebindComplete(action, bindingIndex))
                .Start();
        }


        private void RebindComplete(InputAction action, int index)
        {
            rebindingOperation.Dispose();
            rebindedActionSenderSO.RaiseEvent(action, index);
            uiActivationEventChannelSO.RaiseEvent(false);
            SaveBindings();

        }

        private void SaveBindings()
        {
            string rebinds = inputActionAsset.SaveBindingOverridesAsJson();
            PlayerPrefs.SetString("rebinds", rebinds);
        }


    }
}

