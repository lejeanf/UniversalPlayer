using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using jeanf.tooltip;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

namespace jeanf.vrplayer
{
    [CreateAssetMenu(fileName = "ControlsTooltipSO", menuName = "Tooltips/ControlsTooltipSO", order = 1)]
    public class InputTooltipSO : TooltipSO
    {
        public string actionToAccomplish;

        public ActionSO actionSO;


        public override string Tooltip
        {
            get
            {
                if (actionSO != null)
                {
                    return $"{actionToAccomplish} {GetInputBindingsToDisplay()} to {actionSO.actionName}";
                }
                else
                {
                    return $"{actionToAccomplish}";
                }
            }
        }


        private string GetInputBindingsToDisplay()
        {
            string bindingsToDisplay = "";
            
            foreach(InputControl inputControl in actionSO.inputAction.controls)
            {
                
                if (inputControl.device is Gamepad)
                {
                    bindingsToDisplay += $"{inputControl.name}";
                }
                if (inputControl.device is Keyboard)
                {
                    bindingsToDisplay += $"{inputControl.name}";
                }
            }
            return bindingsToDisplay;
        }
    }

}
