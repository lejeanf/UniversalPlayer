using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using jeanf.vrplayer;
using jeanf.EventSystem;

namespace jeanf.vrplayer 
{
    public class PlayerInputInterface : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActionAsset;
        [SerializeField] private ActionBasedContinuousMoveProvider continuousMoveProvider;
        [SerializeField] private MouseLook mouseLook;
        [SerializeField] private ActionBasedSnapTurnProvider snapTurnProvider;
        [SerializeField] private ActionRebindEventChannelSO actionRebindedListener;
        [SerializeField] private BoolEventChannelSO uiActivationChannel;
        [SerializeField] private GetPrimaryInHandItemWithVRController controller;
        [SerializeField] private MainMenuController _mainMenuController;



        private void OnEnable()
        {
            actionRebindedListener.OnEventRaised += (InputAction, Int) => ChangeActionBindingOnDeltaScript(InputAction, Int);
            uiActivationChannel.OnEventRaised += state => SetUIState(state);
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();


        private void Unsubscribe()
        {
            actionRebindedListener.OnEventRaised -= (InputAction, Int) => ChangeActionBindingOnDeltaScript(InputAction, Int);
            uiActivationChannel.OnEventRaised -= state => SetUIState(state);            

        }

        private void ChangeActionBindingOnDeltaScript(InputAction action, int bindingIndex)
        {
            string actionToRebind = action.name;

            switch (actionToRebind)
            {
                case "Move":
                    continuousMoveProvider.leftHandMoveAction.action.ChangeBinding(action.bindings[bindingIndex]);
                    break;
                case "Snap Turn":
                    snapTurnProvider.rightHandSnapTurnAction.action.ChangeBinding(action.bindings[bindingIndex]);
                    break;
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

        private void SetUIState(bool state)
        {
            if (state)
            {
                foreach (var actionMap in inputActionAsset.actionMaps)
                {
                    if (actionMap == inputActionAsset.FindActionMap("XRI LeftHand Locomotion") || actionMap == inputActionAsset.FindActionMap("FPS"))
                    {
                        actionMap.Disable();
                        Cursor.visible = true;
                    }
                }
            }

            else
            {
                foreach (var actionMap in inputActionAsset.actionMaps)
                {
                    if (actionMap == inputActionAsset.FindActionMap("XRI LeftHand Locomotion") || actionMap == inputActionAsset.FindActionMap("FPS"))
                    {
                        actionMap.Enable();
                        Cursor.visible = false;
                    }
                }
            }
        }
    }

}
