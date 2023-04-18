using jeanf.EventSystem;
using Unity.VectorGraphics;
using UnityEngine;

namespace jeanf.vrplayer
{
    public class CursorStateController : MonoBehaviour
    {
        private bool _isCursorOn = false;
        private bool _isIpadOn = false;
        [SerializeField] private SVGImage cursorImage;
        private static SVGImage _cursorImage;

        [Header("Broadcasting on:")]
        //[SerializeField] private IntEventChannelSO cursorStateChannel;
        [SerializeField] private BoolEventChannelSO mouselookStateChannel;
        

        public enum CursorState
        {
            OnLocked,
            OnConstrained,
            Off,
        }
        private CursorState _cursorState = CursorState.OnLocked;
        private  void Awake() => Init();

        public void Init()
        {
            _cursorImage = cursorImage;
            _cursorState = CursorState.OnLocked;
            _isIpadOn = false;
            _isCursorOn = true;

            SetCursorState(_cursorState);
        }

        public void SetCursorAccordingToHmdState(bool state)
        {
            SetCursorState(state ? CursorState.Off : CursorState.OnLocked);
        }
        
        public void SetCursorAccordingToPrimaryItemState(bool state)
        {
            SetCursorState(state ? CursorState.OnConstrained : CursorState.OnLocked);
        }
        
        public void SetCursorAccordingToMainMenuState(bool state)
        {
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
            if (BroadcastHmdStatus.hmdCurrentState) state = CursorState.Off;
            switch (state)
            {
                case CursorState.OnConstrained:
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.Confined;
                    _cursorImage.enabled = false;
                    mouselookStateChannel.RaiseEvent(false);
                    break;
                case CursorState.OnLocked:
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = true;
                    mouselookStateChannel.RaiseEvent(true);
                    break;
                case CursorState.Off:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = false;
                    mouselookStateChannel.RaiseEvent(false);
                    break;
                default:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = true;
                    mouselookStateChannel.RaiseEvent(true);
                    break;
            }
            //cursorStateChannel.RaiseEvent((int)state);
        }
    }
}