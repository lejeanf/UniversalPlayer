using System;
using System.Collections.Generic;
using System.Reflection;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using jeanf.validationTools;

namespace jeanf.vrplayer
{
    public class FPSCameraMovement : MonoBehaviour, IDebugBehaviour, IValidatable
    {
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        public bool IsValid { get; private set; }

        private Vector2 _inputView;

        public float mouseSensitivity
        {
            get { return _mouseSensitivity; }
            set
            {
                if (isDebug) Debug.Log($"Mouse sensitivity set to {value}");
                _mouseSensitivity = value;
            }
        }

        [Range(0, 100.0f)][SerializeField] private float _mouseSensitivity = 45.0f;
        [Range(0, 500.0f)][SerializeField] private float _gamepadSensitivity = 45.0f;
        float sensitivity;
        [SerializeField] private InputActionReference mouseXY;
        public InputActionReference mouseXYInputAction {
            get { return mouseXY; }
            set { mouseXY = value; }
        }
        private static bool _canLook = true;
        [Space(10)]
        [SerializeField][Validation("A reference to the Player's Camera is required.")]
        public Camera playerCamera;
        [SerializeField][Validation("A reference to the cameraOffset is required.")]
        private Transform cameraOffset;
        [SerializeField][Validation("A reference to the player input component is required")] private PlayerInput playerInput;

        public Transform CameraOffset { get { return cameraOffset; } }
        private Transform _originalCameraOffset;
        [SerializeField] private bool _isHmdActive = false;
        [SerializeField] private float min = -60.0f;
        [SerializeField] private float max = 75.0f;


        private Vector2 _rotation = Vector2.zero;
        private bool _cameraOffsetReset = false;
        /*
        [Header("Broadcasting on:")]
        [SerializeField] private BoolEventChannelSO _canLookStateChannel;
        //[SerializeField] private VoidEventChannelSO _invertPrimaryItemStateChannel;
        */

        [Header("Listening on:")]
        [SerializeField] private BoolEventChannelSO mouselookStateChannel;
        [SerializeField] private VoidEventChannelSO mouselookCameraReset;
        [SerializeField] private TeleportEventChannelSO teleportEventChannel;
        [SerializeField] private StringEventChannelSO controlSchemeChangeEventChannel;
        private bool cameraIsMoving;
        private Vector2 moveValue;

        private void Awake()
        {
            _originalCameraOffset = cameraOffset;
            Init();
        }

        private void OnEnable()
        {
            mouseXY.action.performed += ctx => SetCameraMovement(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * .25f, true);
            mouseXY.action.canceled += ctx => SetCameraMovement(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * .25f, false);
            mouselookStateChannel.OnEventRaised += SetMouseState;
            mouselookCameraReset.OnEventRaised += ResetCameraSettings;
            teleportEventChannel.OnEventRaised += _ => ResetCameraSettings();
            controlSchemeChangeEventChannel.OnEventRaised += ResetCameraOffset;

        }

        private void SetCameraMovement(Vector2 movement, bool cameraIsMoving)
        {
            moveValue = movement;
            this.cameraIsMoving = cameraIsMoving;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            mouseXY.action.performed += ctx => SetCameraMovement(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * .25f, true);
            mouseXY.action.canceled += ctx => SetCameraMovement(ctx.ReadValue<Vector2>() * Time.smoothDeltaTime * .25f, false);
            mouselookStateChannel.OnEventRaised -= SetMouseState;
            mouselookCameraReset.OnEventRaised -= ResetCameraSettings;
            teleportEventChannel.OnEventRaised -= _ => ResetCameraSettings();
            controlSchemeChangeEventChannel.OnEventRaised -= ResetCameraOffset;

        }


        public void Init()
        {
            _canLook = !_isHmdActive;

            ResetCameraSettings();

        }



        public void ResetCameraSettings()
        {
            if (!BroadcastHmdStatus.hmdCurrentState) SetMouseState(true);
            playerCamera.fieldOfView = 60f;
            _rotation = Vector2.zero;
            cameraOffset.localPosition = _originalCameraOffset.localPosition;
            cameraOffset.localRotation = _originalCameraOffset.localRotation;
        }

        public void ResetCameraOffset(string controlScheme)
        {
            if (controlScheme == "XR")
            {
                cameraOffset.localPosition = Vector3.zero;
                cameraOffset.localRotation = Quaternion.identity;
            }
        }

        private void SetCursor(bool state)
        {
            Init();
            _isHmdActive = state;
        }

        private void LateUpdate()
        {

            if (cameraIsMoving && (moveValue.x != 0 || moveValue.y != 0))
            {
                LookAround(moveValue);
            }
        }
        private void LookAround(Vector2 inputView)
        {
            if (playerInput.currentControlScheme == "Gamepad")
            {
                sensitivity = _gamepadSensitivity;
            }
            if (playerInput.currentControlScheme == "Keyboard&Mouse")
            {
                sensitivity = _mouseSensitivity;
            }
            else if (playerInput.currentControlScheme == "XR" || !_canLook) return;

            if(isDebug) Debug.Log($"Mouse inputView value : ({inputView.x}:{inputView.y})");
            _rotation.y += inputView.x * sensitivity;
            _rotation.x += -inputView.y * sensitivity;
            _rotation.x = Mathf.Clamp(_rotation.x, min, max);

            cameraOffset.transform.localRotation = Quaternion.Euler(_rotation.x, _rotation.y, 0);
        }
        
        public void SetMouseState(bool state)
        {
            if((_isDebug)) Debug.Log($"CanLook: {state}");
            _canLook = state;
            //_canLookStateChannel.RaiseEvent(!state);
        }


        public void InvertMouseLookState()
        {
            _canLook = !_canLook;
            //_invertPrimaryItemStateChannel.RaiseEvent();
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            var invalidObjects = new List<object>();
            var errorMessages = new List<string>();
            var validityCheck = true;
            
            invalidObjects.Clear();
            if (playerCamera == null)
            {
                invalidObjects.Add(playerCamera);
                errorMessages.Add("No Camera set");
                validityCheck = false;
            }
            else if (cameraOffset == null)
            {
                invalidObjects.Add(cameraOffset);
                errorMessages.Add("No CameraOffset set");
                validityCheck = false;
            }
            
            if (playerInput == null)
            {
                invalidObjects.Add(playerInput);
                errorMessages.Add("No player input set");
                validityCheck = false;
            }

            IsValid = validityCheck;
            if(!IsValid) return;

            if (IsValid && !Application.isPlaying) return;
            for(var i = 0 ; i < invalidObjects.Count ; i++)
            {
                Debug.LogError($"Error: {errorMessages[i]} " , this.gameObject);
            }
        }
        #endif
    }
}