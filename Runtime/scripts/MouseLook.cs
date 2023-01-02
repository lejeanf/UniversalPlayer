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

        [SerializeField] private float mouseSensitivity = 100f;
        [SerializeField] private InputActionReference mouseXY;
        [SerializeField] float smoothing = .1f;
        [Space(10)]
        [SerializeField] private Transform cameraOffset;
        [SerializeField] private Camera camera;
        [SerializeField] private bool _isHmdActive = false;
        [SerializeField] private float min = -60.0f;
        [SerializeField] private float max = 75.0f;

        private Vector2 rotation = Vector2.zero;
        private bool cameraOffsetReset = false;

        private void Awake()
        {
            SetMouse(false);
        }

        void OnEnable()
        {
            mouseXY.action.performed += ctx => LookAround(ctx.ReadValue<Vector2>());
            BroadcastHmdStatus.hmdStatus += SetHmd;
        }
        void OnDestroy() => Unsubscribe();
        void OnDisable() => Unsubscribe();
        void Unsubscribe()
        {
            BroadcastHmdStatus.hmdStatus -= SetHmd;
            mouseXY.action.performed -= ctx => LookAround(ctx.ReadValue<Vector2>());
        }

        void SetHmd(bool state)
        {
            ResetCameraOffset();
            SetMouse(state);
            _isHmdActive = state;
        }

        void SetMouse(bool state)
        {
            Cursor.visible = !state;
            if (!state) Cursor.lockState = CursorLockMode.Locked;
            else { Cursor.lockState = CursorLockMode.Confined; }
        }

        private void ResetCameraOffset()
        {
            cameraOffset.transform.position = Vector3.zero;
            cameraOffset.transform.rotation = Quaternion.Euler(Vector3.zero);
            camera.fieldOfView = 60f;
        }

        private void LookAround(Vector2 inputView)
        {
            rotation.y += inputView.x * mouseSensitivity * Time.deltaTime;
            rotation.x += -inputView.y * mouseSensitivity * Time.deltaTime;
            rotation.x = Mathf.Clamp(rotation.x, min, max);

            cameraOffset.transform.localRotation = Quaternion.Euler(rotation.x, rotation.y, 0);
        }
    }
}