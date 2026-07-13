using System;
using System.Collections.Generic;
using System.Reflection;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using jeanf.validationTools;

namespace jeanf.universalplayer
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
        [Tooltip("Time constant (seconds) for look smoothing: the camera eases toward the aimed rotation instead of snapping. 0 = raw input.")]
        [Range(0f, 0.3f)][SerializeField] private float lookSmoothingSeconds = 0.05f;
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

        public Transform CameraOffset { get { return cameraOffset; } }
        private Transform _originalCameraOffset;
        // VALUES, not a transform reference: _originalCameraOffset points at cameraOffset
        // itself, so restoring from it was always a no-op.
        private Vector3 _initialCameraOffsetPosition;
        private Quaternion _initialCameraLocalRotation;
        private Vector3 _initialCameraLocalPosition;
        [SerializeField] private bool _isHmdActive = false;
        [SerializeField] private float min = -60.0f;
        [SerializeField] private float max = 75.0f;
        [SerializeField] private PrimaryItemController primaryItemController;
        private Vector2 _rotation = Vector2.zero;
        private Vector2 _smoothedRotation = Vector2.zero;
        /*
        [Header("Broadcasting on:")]
        [SerializeField] private BoolEventChannelSO _canLookStateChannel;
        //[SerializeField] private VoidEventChannelSO _invertPrimaryItemStateChannel;
        */

        // Channel wiring lives on the PlayerEventBridge; this component listens on the
        // internal PlayerEvents delegate surface only.
        private bool cameraIsMoving;
        private Vector2 moveValue;

        private void Awake()
        {
            _originalCameraOffset = cameraOffset;
            if (cameraOffset != null) _initialCameraOffsetPosition = cameraOffset.localPosition;
            if (playerCamera != null)
            {
                _initialCameraLocalPosition = playerCamera.transform.localPosition;
                _initialCameraLocalRotation = playerCamera.transform.localRotation;
            }
            Init();
        }

        private void Start()
        {
            // Apply the tracking/reset state for whatever scheme we started in — with a
            // headset plugged in but on the desk, the game starts in Keyboard&Mouse and
            // the TrackedPoseDriver must not keep aiming the camera at the floor.
            ResetCameraOffset();
        }

        private void OnEnable()
        {
            mouseXY.action.performed += OnMouseXYPerformed;
            mouseXY.action.canceled += OnMouseXYCanceled;
            PlayerEvents.MouselookStateChanged += SetMouseState;
            PlayerEvents.CameraResetRequested += ResetCameraSettings;
            PlayerEvents.PlayerTeleported += OnTeleported;
            BroadcastControlsStatus.SendControlScheme += OnControlSchemeChanged;
        }

        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme _) => ResetCameraOffset();

        private void OnMouseXYPerformed(InputAction.CallbackContext ctx) =>
            SetCameraMovement(ShapeLookInput(ctx.ReadValue<Vector2>()) * Time.smoothDeltaTime * .25f, true);
        private void OnMouseXYCanceled(InputAction.CallbackContext ctx) =>
            SetCameraMovement(ShapeLookInput(ctx.ReadValue<Vector2>()) * Time.smoothDeltaTime * .25f, false);

        // Gamepad response curve on the RAW stick (magnitude 0..1): a small tilt aims
        // precisely, full deflection turns fast. Must happen before the deltaTime
        // pre-scale — squaring the scaled value would crush it to nothing. Mouse
        // deltas pass through untouched.
        private static Vector2 ShapeLookInput(Vector2 value)
        {
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.Gamepad) return value;
            return value * Mathf.Min(value.magnitude, 1f);
        }
        private void OnTeleported(TeleportInformation _) => ResetCameraSettings();

        private void SetCameraMovement(Vector2 movement, bool cameraIsMoving)
        {
            moveValue = movement;
            this.cameraIsMoving = cameraIsMoving;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            mouseXY.action.performed -= OnMouseXYPerformed;
            mouseXY.action.canceled -= OnMouseXYCanceled;
            PlayerEvents.MouselookStateChanged -= SetMouseState;
            PlayerEvents.CameraResetRequested -= ResetCameraSettings;
            PlayerEvents.PlayerTeleported -= OnTeleported;
            BroadcastControlsStatus.SendControlScheme -= OnControlSchemeChanged;
        }


        public void Init()
        {
            _canLook = !_isHmdActive;

            ResetCameraSettings();

        }



        public void ResetCameraSettings()
        {
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR && !primaryItemController.PrimaryItemState) SetMouseState(true);
            playerCamera.fieldOfView = 60f;
            _rotation = Vector2.zero;
            _smoothedRotation = Vector2.zero;
            // In XR the offset stays zeroed (the HMD provides the head pose); on desktop
            // restore the authored eye height. (The old restore read from
            // _originalCameraOffset — the same transform — and never did anything.)
            cameraOffset.localPosition = BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR
                ? Vector3.zero
                : _initialCameraOffsetPosition;
            cameraOffset.localRotation = Quaternion.identity;
        }

        public void ResetCameraOffset()
        {
            var trackedPoseDriver = playerCamera != null
                ? playerCamera.GetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>()
                : null;

            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                if (trackedPoseDriver != null) trackedPoseDriver.enabled = true;
                cameraOffset.localPosition = Vector3.zero;
                cameraOffset.localRotation = Quaternion.identity;
            }
            else
            {
                // Back to desktop: a plugged-in headset keeps STREAMING tracking data (a
                // desk headset aims the camera at the floor), so stop driving the camera
                // from it and restore the authored first-person pose. Look rotation is
                // reapplied by ApplySmoothedRotation every frame.
                if (trackedPoseDriver != null) trackedPoseDriver.enabled = false;
                if (playerCamera != null)
                {
                    playerCamera.transform.localPosition = _initialCameraLocalPosition;
                    playerCamera.transform.localRotation = _initialCameraLocalRotation;
                }
                cameraOffset.localPosition = _initialCameraOffsetPosition;
            }
        }

        private void SetCursor(bool state)
        {
            Init();
            _isHmdActive = state;
        }

        private void LateUpdate()
        {
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR) return;

            if (cameraIsMoving && (moveValue.x != 0 || moveValue.y != 0))
            {
                LookAround(moveValue);
            }

            ApplySmoothedRotation(Time.deltaTime);
        }
        private void LookAround(Vector2 inputView)
        {
            if (!_canLook) return;
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.Gamepad)
            {
                sensitivity = _gamepadSensitivity;
            }
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.KeyboardMouse)
            {
                sensitivity = _mouseSensitivity;


            }
            else if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR) return;

            if(isDebug) Debug.Log($"Mouse inputView value : ({inputView.x}:{inputView.y})");
            _rotation.y += inputView.x * sensitivity;
            _rotation.x += -inputView.y * sensitivity;
            _rotation.x = Mathf.Clamp(_rotation.x, min, max);
        }

        // Look smoothing: the applied rotation eases toward the target accumulated in LookAround,
        // mimicking the way eyes settle on a focus point instead of snapping mechanically.
        // Runs every frame (not only on input) so the ease-out tail plays after the mouse stops.
        private void ApplySmoothedRotation(float dt)
        {
            if (lookSmoothingSeconds <= 0f || dt <= 0f)
            {
                _smoothedRotation = _rotation;
            }
            else
            {
                var t = 1f - Mathf.Exp(-dt / lookSmoothingSeconds);
                _smoothedRotation = Vector2.Lerp(_smoothedRotation, _rotation, t);
            }

            cameraOffset.transform.localRotation = Quaternion.Euler(_smoothedRotation.x, _smoothedRotation.y, 0);
        }
        
        /// <summary>
        /// User-defined eye height in meters above the player root (M&amp;K/gamepad).
        /// Setting it becomes the AUTHORED height: camera resets, scheme switches
        /// and standing up from a seat all restore to THIS value, so a settings
        /// UI can set it once and it sticks. XR ignores it — the HMD is the head.
        /// </summary>
        public float CameraHeight
        {
            get => _initialCameraOffsetPosition.y;
            set
            {
                _initialCameraOffsetPosition.y = value;
                if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR) return;
                if (cameraOffset == null) return;
                var position = cameraOffset.localPosition;
                position.y = value;
                cameraOffset.localPosition = position;
            }
        }

        /// <summary>Accumulated look angles (pitch x, yaw y) in degrees.</summary>
        public Vector2 LookRotation => _rotation;

        /// <summary>
        /// Overrides the accumulated look (raw + smoothed). The sit transition
        /// BLENDS the view toward the seat facing with this — calling
        /// ResetCameraSettings instead snaps the rotation AND wipes the seated
        /// camera height back to the standing offset.
        /// </summary>
        public void OverrideLook(Vector2 rotation)
        {
            _rotation = rotation;
            _smoothedRotation = rotation;
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