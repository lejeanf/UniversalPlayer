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

        public delegate void SetCurrentCursorState(CursorState cursorState);
        public static SetCurrentCursorState CurrentCursorState;

        public enum CursorState
        {
            OnLocked,
            OnConstrained,
            Off,
        }
        private CursorState _cursorState = CursorState.OnLocked;
        private  void Awake() => Init();
        private  void OnEnable()
        {
            BroadcastHmdStatus.hmdStatus += SetCursorAccordingToHmdState;
            CurrentCursorState += SetCursorState;
        }

        private  void OnDestroy() => Unsubscribe();
        private  void OnDisable() => Unsubscribe();

        private void Unsubscribe()
        {
            BroadcastHmdStatus.hmdStatus -= SetCursorAccordingToHmdState;
            CurrentCursorState -= SetCursorState;
        }

        private void Init()
        {
            _cursorImage = cursorImage;
            _cursorState = CursorState.OnLocked;
            _isIpadOn = false;
            _isCursorOn = true;

            SetCursorState(_cursorState);
        }

        private static void SetCursorAccordingToHmdState(bool state)
        {
            SetCursorState(state ? CursorState.Off : CursorState.OnLocked);
        }
        
        public static void SetCursorState(CursorState state)
        {
            if (BroadcastHmdStatus.hmdCurrentState) state = CursorState.Off;
            
            switch (state)
            {
                case CursorState.OnConstrained:
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.Confined;
                    _cursorImage.enabled = false;
                    break;
                case CursorState.OnLocked:
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = true;
                    break;
                case CursorState.Off:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = false;
                    break;
                default:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = true;
                    break;
            }
        }
    }
}