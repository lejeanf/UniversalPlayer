using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem.UI;
using UnityEngine.Serialization;

namespace jeanf.vrplayer
{
    public class MouseLook : MonoBehaviour
    {
        private Vector2 _inputView;

        [Range(0,2)]
        public float mouseSensitivity = 1.2f;
        [SerializeField] private InputActionReference mouseXY;
        private static bool _useInputAction = true; 
        [FormerlySerializedAs("disableMouseLookWhenPrimaryItemDrawn")] [SerializeField] private bool useInputAction = true; 
        [SerializeField] private InputActionReference drawPrimaryItem;
        private static bool _canLook = true;
        private static bool _isPrimaryItemInUse = false;
        [Space(10)]
        [SerializeField] Camera camera;
        [SerializeField] Transform cameraOffset;
        private Transform _originalCameraOffset;
        [SerializeField] private bool _isHmdActive = false;
        [SerializeField] private float min = -60.0f;
        [SerializeField] private float max = 75.0f;

        private Vector2 _rotation = Vector2.zero;
        private bool _cameraOffsetReset = false;

        //events
        public delegate void ResetCameraOffset();
        public static ResetCameraOffset ResetCamera;
        public delegate void SetPrimaryItemState(bool state);
        public static SetPrimaryItemState setPrimaryItemState;

        private void Awake()
        {
            _originalCameraOffset = cameraOffset;
            _useInputAction = useInputAction;
            Init();
        }
        private void Update()
        {
            var targetMouseDelta = Mouse.current.delta.ReadValue() * Time.smoothDeltaTime;
            LookAround(targetMouseDelta);
        }

        private void OnEnable()
        {
            BroadcastHmdStatus.hmdStatus += SetCursor;
            if(useInputAction) drawPrimaryItem.action.performed += ctx=> InvertMouseLookState();
            ResetCamera += ResetCameraSettings;
        }

        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            BroadcastHmdStatus.hmdStatus -= SetCursor;
            if(useInputAction) drawPrimaryItem.action.performed -= null;
            ResetCamera -= null;
        }

        private void Init()
        {
            _canLook = !_isHmdActive;
            ResetCameraSettings();
        }

        private void ResetCameraSettings()
        {
            if(!_isHmdActive) SetMouseState(true);
            camera.fieldOfView = 60f;
            _rotation = Vector2.zero;
            cameraOffset.localPosition = _originalCameraOffset.localPosition;
            cameraOffset.localRotation = _originalCameraOffset.localRotation;
        }

        private void SetCursor(bool state)
        {
            Init();
            _isHmdActive = state;
        }

        private void LookAround(Vector2 inputView)
        {
            if(BroadcastHmdStatus.hmdCurrentState) return;
            if (!_canLook) return;
            _rotation.y += inputView.x * mouseSensitivity;
            _rotation.x += -inputView.y * mouseSensitivity;
            _rotation.x = Mathf.Clamp(_rotation.x, min, max);

            cameraOffset.transform.localRotation = Quaternion.Euler(_rotation.x, _rotation.y, 0);
        }
        
        public static void SetMouseState(bool state)
        {
            Debug.Log($"CanLook: {state}");
            _canLook = state;
            _isPrimaryItemInUse = !state;
            setPrimaryItemState?.Invoke(_isPrimaryItemInUse);
        }

        public static void InvertMouseLookState()
        {
            _canLook = !_canLook;
            _isPrimaryItemInUse = !_isPrimaryItemInUse;
            setPrimaryItemState?.Invoke(_isPrimaryItemInUse);
        }
    }
}