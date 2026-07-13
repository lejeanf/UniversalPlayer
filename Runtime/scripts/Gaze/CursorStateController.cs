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

        [SerializeField] private SVGImage cursorImage;
        private static SVGImage _cursorImage;
        [SerializeField] private SVGImage validationFeedbackImage;

        // Mouselook state is raised on PlayerEvents; the PlayerEventBridge forwards it.

        [Header("Listening on:")]
        [SerializeField] private BoolEventChannelSO PrimaryItemState;
        // Main-menu state arrives over PlayerEvents (bridge slot: mainMenuState).

        public enum CursorState
        {
            OnLocked,
            OnConstrained,
            Off,
        }
        // The cursor state is RESOLVED from all inputs (control scheme, menu,
        // primary item) instead of following the last event — closing the menu
        // while the tablet is still out must keep the cursor free, and a scheme
        // switch must not forget either state.
        private bool _menuOpen;
        private bool _primaryItemOut;

        private  void Awake() => Init();

        private void OnEnable()
        {
            PrimaryItemState.OnEventRaised += SetCursorAccordingToPrimaryItemState;
            PlayerEvents.MenuStateChanged += SetCursorAccordingToMainMenuState;
            PlayerEvents.ScreenFadeChanged += OnScreenFadeChanged;
            BroadcastControlsStatus.SendControlScheme += OnSchemeChangedSetCursor;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            PrimaryItemState.OnEventRaised -= SetCursorAccordingToPrimaryItemState;
            PlayerEvents.MenuStateChanged -= SetCursorAccordingToMainMenuState;
            PlayerEvents.ScreenFadeChanged -= OnScreenFadeChanged;
            BroadcastControlsStatus.SendControlScheme -= OnSchemeChangedSetCursor;

        }

        private void OnScreenFadeChanged(bool _) => ResolveCursor();

        public void Init()
        {
            _cursorImage = cursorImage;

            if (isDebug)
            {
                Debug.Log("Changing cursor in init");
            }
            SetCursorAccordingToControlScheme();
        }


        private void OnSchemeChangedSetCursor(BroadcastControlsStatus.ControlScheme _) => ResolveCursor();

        public void SetCursorAccordingToControlScheme() => ResolveCursor();

        public void SetCursorAccordingToPrimaryItemState(bool state)
        {
            if (isDebug) Debug.Log("Changing cursor because of primary item state ");
            _primaryItemOut = state;
            ResolveCursor();
        }

        public void SetCursorAccordingToMainMenuState(bool state)
        {
            if (isDebug) Debug.Log("Changing cursor because of main menu state ");
            _menuOpen = state;
            ResolveCursor();
        }

        /// <summary>
        /// One rule for the whole cursor:
        ///  VR                          → Off (nothing changes when items equip)
        ///  menu open                   → OnConstrained (the menu UI needs the
        ///                                cursor, even over the menu's black fade)
        ///  world black (load/teleport) → Off (nothing to point at)
        ///  primary item out            → OnConstrained (free cursor for the tablet)
        ///  otherwise                   → OnLocked (first-person look)
        /// </summary>
        private void ResolveCursor()
        {
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
                SetCursorState(CursorState.Off);
            else if (_menuOpen)
                SetCursorState(CursorState.OnConstrained);
            else if (FadeMask.ScreenFaded)
                SetCursorState(CursorState.Off);
            else if (_primaryItemOut)
                SetCursorState(CursorState.OnConstrained);
            else
                SetCursorState(CursorState.OnLocked);
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
                    PlayerEvents.RaiseMouselookState(false);
                    break;
                case CursorState.OnLocked:
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = true;
                    if (validationFeedbackImage != null)
                    {
                        validationFeedbackImage.enabled = true;
                    }
                    PlayerEvents.RaiseMouselookState(true);
                    break;
                case CursorState.Off:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = false;
                    if (validationFeedbackImage != null)
                    {
                        validationFeedbackImage.enabled = false;
                    }
                    PlayerEvents.RaiseMouselookState(false);
                    break;
                default:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = true;
                    if (validationFeedbackImage != null)
                    {
                        validationFeedbackImage.enabled = true;
                    }
                    PlayerEvents.RaiseMouselookState(true);
                    break;
            }
            //cursorStateChannel.RaiseEvent((int)state);
        }
    }
}