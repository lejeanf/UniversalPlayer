using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.propertyDrawer;
using jeanf.EventSystem;

namespace jeanf.vrplayer
{
    [CreateAssetMenu(fileName = "ActionSO", menuName = "PlayerActions/ActionSO", order = 2)]

    [ScriptableObjectDrawer]
    public class ActionSO : ScriptableObject, ISerializationCallbackReceiver
    {

        public InputAction inputAction;

        public string actionName;


        public List<InputBinding> bindings = new List<InputBinding>();

        public DescriptionBaseSO eventChannel;

        public bool canRebind = false;
        
       
        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            foreach (var binding in bindings)
            {
                if (inputAction.bindings.Count >= bindings.Count)
                {
                    return;
                }
                inputAction.AddBinding(binding);
            }
        }

        void SaveData()
        {

        }
    }
}
