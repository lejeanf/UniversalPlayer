using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using jeanf.tooltip;

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
                    return $"{actionToAccomplish} {GetInputBindingsToDisplay()} to {actionSO.name}";
                }
                else
                {
                    return $"{actionToAccomplish}";
                }
            }
        }


        private string GetInputBindingsToDisplay()
        {
            //actionSO.inputAction.controls
            return string.Empty;
        }
    }

}
