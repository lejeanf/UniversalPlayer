using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using jeanf.EventSystem;
using jeanf.vrplayer;


namespace jeanf.vrplayer
{
    public class PlayerActionManager : MonoBehaviour
    {
        #region Debug variables
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;
        #endregion

        #region input action variables
        //Action listing variables
        [SerializeField] private List<InputAction> _inputActionList = new List<InputAction>();
        [SerializeField] private InputActionAsset m_InputActionAsset;
        [SerializeField] private ActionContainerSO _actionContainer;
        [SerializeField] private ActionRebindEventChannelSO actionRebindedListener;
        #endregion


        private void Awake()
        {
            ListAllInputs();
        }
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

        public void CreatePlayerActions()
        {
            foreach (var action in m_InputActionAsset)
            {
                ActionSO actionSO = (ActionSO)AssetDatabase.LoadAssetAtPath($"Assets/VR_Player/Runtime/Scripts/ActionManagement/PlayerActions/{action.actionMap.name}_{action.name}.asset", typeof(ActionSO));
                if (actionSO == null)
                {
                    actionSO = (ActionSO)ScriptableObject.CreateInstance($"ActionSO");
                    actionSO.name = $"{action.actionMap.name}_{action.name}";
                    AssetDatabase.CreateAsset(actionSO, $"Assets/VR_Player/Runtime/Scripts/ActionManagement/PlayerActions/{actionSO.name}.asset");
                }
                actionSO.actionName = action.name;
                actionSO.inputAction = action;

                Debug.Log("CreatePlayerActions" + action);

            }

        }


        private void ListAllInputs()
        {
            foreach (var action in m_InputActionAsset)
            {

                ActionSO actionSO = (ActionSO)AssetDatabase.LoadAssetAtPath($"Assets/VR_Player/Runtime/Scripts/ActionManagement/PlayerActions/{action.actionMap.name}_{action.name}.asset", typeof(ActionSO));
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
            if (_actionContainer._actions[inputAction] != null && _actionContainer._actions[inputAction].canRebind)
            {
                _actionContainer._actions[inputAction].inputAction.ChangeBinding(index)
                    .WithPath(inputAction.bindings[index].path);
            }
        }


        private Action ForwardActionToSO(ActionSO action, InputAction.CallbackContext ctx)
        {
            Action functionToCall = null;
            if (action.eventChannel == null) return null;
            var actionType = action.eventChannel.GetType().ToString(); 
            Debug.Log(actionType);
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

