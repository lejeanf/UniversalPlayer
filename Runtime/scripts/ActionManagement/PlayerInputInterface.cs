using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using jeanf.vrplayer;
using jeanf.EventSystem;
using jeanf.validationTools;

namespace jeanf.vrplayer 
{
    public class PlayerInputInterface : MonoBehaviour, IValidatable
    {

        #region Debug tools
        public bool IsValid { get; private set; }
        #endregion

        [Validation("A reference to InputActionAsset is required.")]
        [SerializeField] private InputActionAsset inputActionAsset;
        //[Validation("A reference to ContinuousMoveProvider is required.")]
        //[SerializeField] private ActionBasedContinuousMoveProvider continuousMoveProvider;
        [Validation("A reference to mouseLook is required.")]
        [SerializeField] private FPSCameraMovement mouseLook;
        //[Validation("A reference to ActionBasedSnapTurnProvider is required.")]
        //[SerializeField] private ActionBasedSnapTurnProvider snapTurnProvider;
        [Validation("A reference to GetPrimaryInHandItemWithVRController is required.")]
        [SerializeField] private GetPrimaryInHandItemWithVRController controller;
        [Validation("A reference to MainMenuController is required.")]
        [SerializeField] private MainMenuController _mainMenuController;
        [Validation("A reference to ActionRebind event channel SO is required.")]
        [SerializeField] private ActionRebindEventChannelSO actionRebindedListener;


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

            //if (continuousMoveProvider == null)
            //{
            //    invalidObjects.Add(continuousMoveProvider);
            //    errorMessages.Add("No continuousMoveProvider set");
            //    validityCheck = false;
            //}

            if (mouseLook == null)
            {
                invalidObjects.Add(mouseLook);
                errorMessages.Add("No mouseLook set");
                validityCheck = false;
            }

            //if (snapTurnProvider == null)
            //{
            //    invalidObjects.Add(snapTurnProvider);
            //    errorMessages.Add("No snapTurnProvider set");
            //    validityCheck = false;
            //}

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
            if (actionRebindedListener == null)
            {
                invalidObjects.Add(actionRebindedListener);
                errorMessages.Add("No Action Rebind SO set");
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
            actionRebindedListener.OnEventRaised += (InputAction, Int) => ChangeActionBindingOnDeltaScript(InputAction, Int);
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();


        private void Unsubscribe()
        {
            actionRebindedListener.OnEventRaised -= (InputAction, Int) => ChangeActionBindingOnDeltaScript(InputAction, Int);

        }

        private void ChangeActionBindingOnDeltaScript(InputAction action, int bindingIndex)
        {
            string actionToRebind = action.name;

            switch (actionToRebind)
            {
                //case "Move":
                //    continuousMoveProvider.leftHandMoveAction.action.ChangeBinding(action.bindings[bindingIndex]);
                //    break;
                //case "Snap Turn":
                //    snapTurnProvider.rightHandSnapTurnAction.action.ChangeBinding(action.bindings[bindingIndex]);
                //    break;
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
