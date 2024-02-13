using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.EventSystem;

namespace jeanf.vrplayer
{
    public class ActionRebinder : MonoBehaviour
    {
        
        [SerializeField] private InputActionAsset inputActionAsset;
        private InputActionRebindingExtensions.RebindingOperation rebindingOperation;
        private const string RebindsKey = "rebinds";

        #region EventChannels
        [SerializeField] private ActionRebindEventChannelSO rebindedActionSenderSO;
        [SerializeField] private ActionRebindEventChannelSO actionRebindListenerSO;
        [SerializeField] private BoolEventChannelSO uiActivationEventChannelSO;
        #endregion

        private void Start()
        {
            string rebinds = PlayerPrefs.GetString(RebindsKey, string.Empty);
            if (string.IsNullOrEmpty(rebinds)) { return; }
            inputActionAsset.LoadBindingOverridesFromJson(rebinds);
        }

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
            Debug.Log(action.bindings[bindingIndex]);
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

