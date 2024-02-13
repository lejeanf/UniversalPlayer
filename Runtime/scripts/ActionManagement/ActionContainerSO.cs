using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.propertyDrawer;

namespace jeanf.vrplayer
{
    [CreateAssetMenu(fileName = "ActionContainerSO", menuName = "PlayerActions/ActionContainerSO", order = 3)]
    [ScriptableObjectDrawer]
    public class ActionContainerSO : ScriptableObject, ISerializationCallbackReceiver
    {
        public Dictionary<InputAction, ActionSO> _actions = new Dictionary<InputAction, ActionSO>();
        [SerializeField] private List<InputAction> _keys = new List<InputAction>();
        [SerializeField] private List<ActionSO> _values = new List<ActionSO>();    

        public void OnBeforeSerialize()
        {
            _keys.Clear();
            _values.Clear();
            foreach (var key in _actions.Keys)
            {
                if (!_keys.Contains(key))
                {
                    _keys.Add(key);
                }

            }

            foreach (var value in _actions.Values)
            {
                if (!_values.Contains(value))
                {
                    _values.Add(value);
                }
            }
        }

        public void OnAfterDeserialize()
        {
            _actions = new Dictionary<InputAction, ActionSO>();

            for (int i = 0; i != Mathf.Min(_keys.Count, _values.Count); i++)
            {
                _actions.Add(_keys[i], _values[i]);
            }
        }
    }
}

