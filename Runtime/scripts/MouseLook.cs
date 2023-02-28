using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.InputSystem.UI;

namespace jeanf.vrplayer
{
    public class MouseLook : MonoBehaviour
    {
        private Vector2 _inputView;

        [Range(0,2)]
        public float mouseSensitivity = 1.2f;
        [SerializeField] private InputActionReference mouseXY;
        [SerializeField] private bool disableMouseLookWhenPrimaryItemDrawn = true; 
        [SerializeField] private InputActionReference drawPrimaryItem;
        private static bool _canLook = true;
        private bool _isPrimaryItemInUse = false;
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

        private void Awake()
        {
            _originalCameraOffset = cameraOffset;
        }
        private void Update()
        {
            var targetMouseDelta = Mouse.current.delta.ReadValue() * Time.smoothDeltaTime;
            LookAround(targetMouseDelta);
        }

        private void OnEnable()
        {
            BroadcastHmdStatus.hmdStatus += SetCursor;
            drawPrimaryItem.action.performed += ctx=> DisableMouseLook();
            ResetCamera += Reset;
        }

        private void OnDestroy() => Unsubscribe();
        private void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            BroadcastHmdStatus.hmdStatus -= SetCursor;
            drawPrimaryItem.action.performed -= null;
            ResetCamera -= Reset;
        }

        private void DisableMouseLook()
        {
            if (disableMouseLookWhenPrimaryItemDrawn && !_isPrimaryItemInUse && _canLook)
            {
                _canLook = false;
                _isPrimaryItemInUse = true;
            }

            else
            {
                _canLook = true;
                _isPrimaryItemInUse = false;
            }
        }

        public void InfosMouse()
        {
            //Debug.Log($"Left click !");
        }

        private void Reset()
        {
            camera.fieldOfView = 60f;
            _rotation = Vector2.zero;
            cameraOffset.localPosition = _originalCameraOffset.localPosition;
            cameraOffset.localRotation = _originalCameraOffset.localRotation;
        }

        private void SetCursor(bool state)
        {
            //Debug.Log($"SetCursor");
            //Debug.Log($"state: {state}");
            Reset();
            _isHmdActive = state;
        }

        private void LookAround(Vector2 inputView)
        {
            if (!_canLook) return;
            _rotation.y += inputView.x * mouseSensitivity;
            _rotation.x += -inputView.y * mouseSensitivity;
            _rotation.x = Mathf.Clamp(_rotation.x, min, max);

            cameraOffset.transform.localRotation = Quaternion.Euler(_rotation.x, _rotation.y, 0);
        }
        
        public static void CanLook(bool state)
        {
            Debug.Log($"CanLook: {state}");
            _canLook = state;
        }
    }
}