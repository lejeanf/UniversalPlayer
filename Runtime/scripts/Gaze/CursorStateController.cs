using jeanf.EventSystem;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
namespace jeanf.universalplayer
{
    public class CursorStateController : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        private bool _isCursorOn = false;
        private bool _isIpadOn = false;
        [SerializeField] private SVGImage cursorImage;
        private static SVGImage _cursorImage;
        [SerializeField] private SVGImage validationFeedbackImage;

        [Header("Broadcasting on:")]
        //[SerializeField] private IntEventChannelSO cursorStateChannel;
        [SerializeField] private BoolEventChannelSO mouselookStateChannel;

        [Header("Listening on:")] 
        [SerializeField] private BoolEventChannelSO PrimaryItemState;
        [SerializeField] private BoolEventChannelSO MainMenuState;
        [SerializeField] private VoidEventChannelSO currentControlSchemeChannelSO;

        public enum CursorState
        {
            OnLocked,
            OnConstrained,
            Off,
        }
        private CursorState _cursorState;
        private  void Awake() => Init();

        private void OnEnable()
        {
            PrimaryItemState.OnEventRaised += SetCursorAccordingToPrimaryItemState;
            MainMenuState.OnEventRaised += SetCursorAccordingToMainMenuState;
            currentControlSchemeChannelSO.OnEventRaised += SetCursorAccordingToControlScheme;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            PrimaryItemState.OnEventRaised -= SetCursorAccordingToPrimaryItemState;
            MainMenuState.OnEventRaised -= SetCursorAccordingToMainMenuState;
            currentControlSchemeChannelSO.OnEventRaised -= SetCursorAccordingToControlScheme;

        }

        public void Init()
        {
            _cursorImage = cursorImage;
            _cursorState = CursorState.OnLocked;
            _isIpadOn = false;
            _isCursorOn = true;

            if (isDebug)
            {
                Debug.Log("Changing cursor in init");
            }
            SetCursorAccordingToControlScheme();
        }


        public void SetCursorAccordingToControlScheme()
        {

            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                SetCursorState(CursorState.Off);
            }
            else
            {
                SetCursorState(CursorState.OnLocked);
            }
        }


        public void SetCursorAccordingToPrimaryItemState(bool state)
        {
            if (isDebug)
            {
                Debug.Log("Changing cursor because of primary item state ");
            }
            SetCursorState(state ? CursorState.OnConstrained : CursorState.OnLocked);
        }
        
        public void SetCursorAccordingToMainMenuState(bool state)
        {
            if (isDebug)
            {
                Debug.Log("Changing cursor because of main menu state ");
            }

            SetCursorState(state ? CursorState.OnConstrained : CursorState.OnLocked);
        }

        public void SetCursorState(CursorState state)
        {
            SetCursor(state);
        }
        public void SetCursorState(int state)
        {
            SetCursor((CursorState)state);
        }

        private void SetCursor(CursorState state)
        {
            if (isDebug)
            {
                Debug.Log("Setting cursor to " + state.ToString());
            }
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR) state = CursorState.Off;
            switch (state)
            {
                case CursorState.OnConstrained:
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.Confined;
                    _cursorImage.enabled = false;
                    if (validationFeedbackImage != null)
                    {
                        validationFeedbackImage.enabled = false;
                    }
                    mouselookStateChannel.RaiseEvent(false);
                    break;
                case CursorState.OnLocked:
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = true;
                    if (validationFeedbackImage != null)
                    {
                        validationFeedbackImage.enabled = true;
                    }
                    mouselookStateChannel.RaiseEvent(true);
                    break;
                case CursorState.Off:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = false;
                    if (validationFeedbackImage != null)
                    {
                        validationFeedbackImage.enabled = false;
                    }
                    mouselookStateChannel.RaiseEvent(false);
                    break;
                default:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = true;
                    if (validationFeedbackImage != null)
                    {
                        validationFeedbackImage.enabled = true;
                    }
                    mouselookStateChannel.RaiseEvent(true);
                    break;
            }
            //cursorStateChannel.RaiseEvent((int)state);
        }
    }
}