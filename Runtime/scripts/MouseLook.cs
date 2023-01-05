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
        private Vector2 inputView;

        [Range(0,2)]
        [SerializeField] private float mouseSensitivity = 1.2f;
        [SerializeField] private InputActionReference mouseXY;
        [Space(10)]
        [SerializeField] Camera camera;
        [SerializeField] Transform cameraOffset;
        private Transform originalCameraOffset;
        [SerializeField] private bool _isHmdActive = false;
        [SerializeField] private float min = -60.0f;
        [SerializeField] private float max = 75.0f;

        private Vector2 rotation = Vector2.zero;
        private bool cameraOffsetReset = false;

        //events
        public delegate void ResetCameraOffset();
        public static ResetCameraOffset ResetCamera;

        private void Awake()
        {
            originalCameraOffset = cameraOffset;
            SetMouse(false);
        }
        private void Update()
        {
            Vector2 targetMouseDelta = Mouse.current.delta.ReadValue() * Time.smoothDeltaTime;
            LookAround(targetMouseDelta);
        }

        void OnEnable()
        {
            BroadcastHmdStatus.hmdStatus += SetHmd;
            ResetCamera += Reset;
        }
        void OnDestroy() => Unsubscribe();
        void OnDisable() => Unsubscribe();
        void Unsubscribe()
        {
            BroadcastHmdStatus.hmdStatus -= SetHmd;
            ResetCamera -= Reset;
        }

        void Reset()
        {
            camera.fieldOfView = 60f;
            rotation = Vector2.zero;
            cameraOffset.localPosition = originalCameraOffset.localPosition;
            cameraOffset.localRotation = originalCameraOffset.localRotation;
        }

        void SetHmd(bool state)
        {
            Reset();
            SetMouse(state);
            _isHmdActive = state;
        }

        void SetMouse(bool state)
        {
            Cursor.visible = !state;
            if (!state) Cursor.lockState = CursorLockMode.Locked;
            else { Cursor.lockState = CursorLockMode.Confined; }
        }

        private void LookAround(Vector2 inputView)
        {
            rotation.y += inputView.x * mouseSensitivity;
            rotation.x += -inputView.y * mouseSensitivity;
            rotation.x = Mathf.Clamp(rotation.x, min, max);

            cameraOffset.transform.localRotation = Quaternion.Euler(rotation.x, rotation.y, 0);
        }
    }
}