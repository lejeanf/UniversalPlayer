using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.validationTools;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Applies a completed rebind (<see cref="PlayerEvents.ActionRebound"/>) to the
    /// components that hold their own copy of an action reference.
    /// </summary>
    public class PlayerInputInterface : MonoBehaviour, IValidatable
    {

        #region Debug tools
        public bool IsValid { get; private set; }
        #endregion

        [Validation("A reference to InputActionAsset is required.")]
        [SerializeField] private InputActionAsset inputActionAsset;
        [Validation("A reference to mouseLook is required.")]
        [SerializeField] private FPSCameraMovement mouseLook;
        [Validation("A reference to GetPrimaryInHandItemWithVRController is required.")]
        [SerializeField] private GetPrimaryInHandItemWithVRController controller;
        [Validation("A reference to MainMenuController is required.")]
        [SerializeField] private MainMenuController _mainMenuController;


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

            if (mouseLook == null)
            {
                invalidObjects.Add(mouseLook);
                errorMessages.Add("No mouseLook set");
                validityCheck = false;
            }

            if (controller == null)
            {
                invalidObjects.Add(controller);
                errorMessages.Add("No PrimaryInHandItemWithVRController set");
                validityCheck = false;
            }

            if (_mainMenuController == null)
            {
                invalidObjects.Add(_mainMenuController);
                errorMessages.Add("No MainMenuController set");
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
            PlayerEvents.ActionRebound += ChangeActionBindingOnDeltaScript;
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();


        private void Unsubscribe()
        {
            PlayerEvents.ActionRebound -= ChangeActionBindingOnDeltaScript;
        }

        private void ChangeActionBindingOnDeltaScript(InputAction action, int bindingIndex)
        {
            string actionToRebind = action.name;

            switch (actionToRebind)
            {
                case "Look Around":
                    mouseLook.mouseXYInputAction.action.ChangeBinding(action.bindings[bindingIndex]);
                    break;
                case "Draw Primary Item":
                    controller.GetActiveHand().action.ChangeBinding(action.bindings[bindingIndex]);
                    break;
                case "Main Menu":
                    _mainMenuController._openMainMenuAction.action.ChangeBinding(action.bindings[bindingIndex]);
                    break;
                default:
                    break;
            }
        }
    }

}
