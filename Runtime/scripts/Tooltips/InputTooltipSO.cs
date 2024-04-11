using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using jeanf.tooltip;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.Users;

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
                    return $"{actionToAccomplish} Bindings to {actionSO.actionName}";
                }
                else
                {
                    return $"{actionToAccomplish}";
                }
            }
        }
    }

}
