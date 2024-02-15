using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using jeanf.EventSystem;
using jeanf.vrplayer;
using jeanf.validationTools;


namespace jeanf.vrplayer
{
    public class PlayerActionManager : MonoBehaviour, IValidatable
    {
        #region Debug variables
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;
        public bool IsValid { get; private set; }
        #endregion

        #region input action variables
        //Action listing variables
        [SerializeField] private List<InputAction> _inputActionList = new List<InputAction>();
        [Validation("A reference to InputActionAsset is required.")]
        [SerializeField] private InputActionAsset m_InputActionAsset;
        [Validation("A reference to ActionContainerSO is required.")]
        [SerializeField] private ActionContainerSO _actionContainer;
        [Validation("A reference to ActionRebindEventChannelSO is required.")]
        [SerializeField] private ActionRebindEventChannelSO actionRebindedListener;
        #endregion

        private void Awake()
        {
            ListAllInputs();
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            var invalidObjects = new List<object>();
            var errorMessages = new List<string>();
            var validityCheck = true;

            invalidObjects.Clear();

            if(_actionContainer == null)
            {
                invalidObjects.Add(_actionContainer);
                errorMessages.Add("No action container SO set");
                validityCheck = false;
            }

            if(actionRebindedListener == null)
            {
                invalidObjects.Add(actionRebindedListener);
                errorMessages.Add("No ActionRebindEventChannelSO set");
                validityCheck = false;
            }

            if (m_InputActionAsset == null)
            {
                invalidObjects.Add(m_InputActionAsset);
                errorMessages.Add("No InputActionAsset set");
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
            foreach (var action in _inputActionList)
            {
                action.Enable();
                action.performed += ctx => ForwardActionToSO(_actionContainer._actions[action], ctx);
            }
            actionRebindedListener.OnEventRaised += (input, value) => RebindInput(input, value);
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            foreach (var action in _inputActionList)
            {
                action.performed -= ctx => ForwardActionToSO(_actionContainer._actions[action], ctx);
                action.Disable();
            }
            _actionContainer._actions.Clear();
            _actionContainer._actions.TrimExcess();
            _inputActionList.Clear();
            _inputActionList.TrimExcess();
            actionRebindedListener.OnEventRaised -= (input, value) => RebindInput(input, value);
        }

//#if UNITY_EDITOR
        public void CreatePlayerActions()
        {
            foreach (var action in m_InputActionAsset)
            {
                ActionSO actionSO = Resources.Load<ActionSO>($"Player/Actions/{action.actionMap.name}_{action.name}");
                if (actionSO == null)
                {
                    actionSO = (ActionSO)ScriptableObject.CreateInstance($"ActionSO");
                    actionSO.name = $"{action.actionMap.name}_{action.name}";
                    AssetDatabase.CreateAsset(actionSO, $"Assets/Resources/Player/Actions/{actionSO.name}.asset");
                    
                }
                actionSO.actionName = action.name;
                actionSO.inputAction = action;
                actionSO.bindings.Clear();
                actionSO.bindings.TrimExcess();
                foreach(var binding in action.bindings)
                {
                    actionSO.bindings.Add(binding);
                }
            }
        }
//#endif

        private void ListAllInputs()
        {
            foreach (var action in m_InputActionAsset)
            {
                ActionSO actionSO = Resources.Load<ActionSO>($"Player/Actions/{action.actionMap.name}_{action.name}");
                if (actionSO == null)
                {
                    Debug.LogError("Did not found actionSO. Press the \"Create Player Actions\" button in the PlayerActionManager component on the Player prefab ");
                }
                if (!_actionContainer._actions.ContainsKey(action))
                {
                    _actionContainer._actions.Add(action, actionSO);
                }
            }
        }

        void RebindInput(InputAction inputAction, int index)
        {
            Debug.Log($"This is RebindInput and this is your old path {_actionContainer._actions[inputAction].inputAction.bindings[index]}");
            if (_actionContainer._actions[inputAction] != null && _actionContainer._actions[inputAction].canRebind)
            {
                _actionContainer._actions[inputAction].inputAction.ApplyBindingOverride(index, inputAction.bindings[index].overridePath);
            }
            Debug.Log($"This is RebindInput and this is your new path {_actionContainer._actions[inputAction].inputAction.bindings[index]}");
            Debug.Log($"This is RebindInput and this is to make sure {_actionContainer._actions[inputAction].inputAction.bindings[index - 1]}");
        }

        private Action ForwardActionToSO(ActionSO action, InputAction.CallbackContext ctx)
        {
            Action functionToCall = null;
            if (action.eventChannel == null) return null;
            var actionType = action.eventChannel.GetType().ToString(); 
            switch (actionType)
            {
                case "IntEventChannelSO":
                    IntEventChannelSO intEventChannelSO = (IntEventChannelSO)action.eventChannel;
                    functionToCall = delegate { intEventChannelSO.RaiseEvent(ctx.ReadValue<int>()); };
                    break;
                case "VoidEventChannelSO":
                    VoidEventChannelSO voidEventChannelSO = (VoidEventChannelSO)action.eventChannel;
                    functionToCall = delegate { voidEventChannelSO.RaiseEvent(); };
                    break;
                case "BoolEventChannelSO":
                    BoolEventChannelSO boolEventChannelSO = (BoolEventChannelSO)action.eventChannel;
                    functionToCall = delegate { boolEventChannelSO.RaiseEvent(ctx.ReadValue<bool>()); };
                    break;
                case "FloatEventChannelSO":
                    FloatEventChannelSO floatEventChannelSO = (FloatEventChannelSO)action.eventChannel;
                    functionToCall = delegate { floatEventChannelSO.RaiseEvent(ctx.ReadValue<float>()); };
                    break;
                case "Vector2EventChannelSO":
                    Vector3EventChannelSO vector2EventChannelSO = (Vector3EventChannelSO)action.eventChannel;
                    functionToCall = delegate { vector2EventChannelSO.RaiseEvent(ctx.ReadValue<Vector2>()); };
                    break;
                case "Vector3EventChannelSO":
                    Vector3EventChannelSO vector3EventChannelSO = (Vector3EventChannelSO)action.eventChannel;
                    functionToCall = delegate { vector3EventChannelSO.RaiseEvent(ctx.ReadValue<Vector3>()); };
                    break;
                case "QuaternionEventChannelSO":
                    QuaternionEventChannelSO quaternionEventChannelSO = (QuaternionEventChannelSO)action.eventChannel;
                    functionToCall = delegate { quaternionEventChannelSO.RaiseEvent(ctx.ReadValue<Quaternion>()); };
                    break;
                //case "XRBaseInteractorEventChannelSO":
                //    XRBaseInteractorEventChannelSO xRBaseInteractorEventChannelSO = (XRBaseInteractorEventChannelSO)action.eventChannel;
                //    XRBaseInteractor xrBaseInteractor = xRBaseInteractorEventChannelSO.OnEventRaised(ctx.ReadValue<XRBaseInteractor>());
                //    functionToCall = delegate { xRBaseInteractorEventChannelSO.RaiseEvent(xrBaseInteractor); };
                //    break;
                default:
                    Debug.Log("Null");
                    break;
                }
            return functionToCall;
        }
    }
}

