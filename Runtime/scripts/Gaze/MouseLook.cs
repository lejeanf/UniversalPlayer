using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace jeanf.vrplayer
{
    public class MouseLook : MonoBehaviour, IDebugBehaviour 
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;
        
        private Vector2 _inputView;

        [Range(0,2)]
        public float mouseSensitivity = 1.2f;
        [SerializeField] private InputActionReference mouseXY;
        private static bool _canLook = true;
        [Space(10)]
        [SerializeField] Camera camera;
        [SerializeField] Transform cameraOffset;
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

        private void Awake()
        {
            _originalCameraOffset = cameraOffset;
            //_useInputAction = useInputAction;
            Init();
        }
        private void Update()
        {
            var targetMouseDelta = Mouse.current.delta.ReadValue() * Time.smoothDeltaTime;
            LookAround(targetMouseDelta);
        }

        public void Init()
        {
            _canLook = !_isHmdActive;
            //_canLookStateChannel.RaiseEvent(_canLook);
            
            ResetCameraSettings();
        }

        public void ResetCameraSettings()
        {
            if(!BroadcastHmdStatus.hmdCurrentState) SetMouseState(true);
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

    }
}