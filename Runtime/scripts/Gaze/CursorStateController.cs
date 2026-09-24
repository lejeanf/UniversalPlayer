using jeanf.EventSystem;
using jeanf.validationTools;
using UnityEngine;
#pragma warning disable 0618 // ActionBasedController: deprecated in XRI 3, still what the gaze rig runs on
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
        [Header("Listening on:")]
        [Validation("Primary-item state channel is required — the cursor frees for the tablet from it (and it is subscribed unguarded at startup).")]
        [SerializeField] private BoolEventChannelSO PrimaryItemState;

        public enum CursorState
        {
            OnLocked,
            OnConstrained,
            Off,
        }

        private bool _menuOpen;
        private bool _primaryItemOut;

        [Header("Cursor look (free-cursor pointer)")]
        [Tooltip("The ONE palette for pointer colours — resting / hover / click / invalid. The VR interaction ray (InteractionRayHoverVisual) reads the SAME asset through this component, so cursor and ray can never drift apart. Duplicate the packaged CursorPalette for a project-specific look.")]
        [Validation("No CursorPaletteSO — cursor and interaction ray fall back to the packaged code-default colours and the project cannot restyle them.")]
        [SerializeField] private CursorPaletteSO palette;
        [Tooltip("Cursor size while the primary item is drawn (lerped in and back out).")]
        [Range(0.1f, 1f)][SerializeField] private float tabletCursorScale = 0.5f;
        [SerializeField] private float cursorScaleLerpSeconds = 0.12f;
        [Tooltip("While the click is HELD, the reticle shrinks to this FRACTION of its size — and stays there until the button is released.")]
        [Range(0.1f, 1f)][SerializeField] private float clickPulseScale = 0.75f;
        [Tooltip("Seconds to ease into the pressed size (and back out again on release).")]
        [SerializeField] private float clickPulseSeconds = 0.09f;

        [Header("Cursor hover / click (interactables & tooltips)")]
        [Tooltip("How long (seconds) the invalid-request flash stays on the reticle before it returns to normal.")]
        [SerializeField] private float invalidFlashSeconds = 0.35f;

        /// <summary>The shared pointer palette (cursor AND interaction ray). Null only on a mis-wired rig; the accessors then fall back to the packaged defaults.</summary>
        public CursorPaletteSO Palette => palette;
        private CursorPaletteSO EffectivePalette => palette != null ? palette : CursorPaletteSO.Fallback;
        public Color HoverColor => EffectivePalette.hover;
        public Color ClickColor => EffectivePalette.click;
        public Color RestingColor => EffectivePalette.resting;
        public Color InvalidColor => EffectivePalette.invalid;

        private Color _resolvedColor = Color.white;
        private bool _cursorVisible = true;
        private Color _displayedColor = Color.white;
        private float _fill;

        public void SetResolvedColor(Color color)
        {
            _resolvedColor = color;
        }

        public bool IsFlashingInvalid => Time.unscaledTime < _invalidFlashUntil;
        public void FlashInvalidAction()
        {
            _invalidFlashUntil = Time.unscaledTime + Mathf.Max(0.05f, invalidFlashSeconds);
        }
        private bool _followPointer;
        private float _targetScale = 1f;
        private float _baseScale = 1f;
        private float _pulseScale = 1f;
        private bool _clickHeld;
        private float _invalidFlashUntil = -1f;
        private bool _wasFlashingInvalid;

        private void Awake() => Init();

        private void Update()
        {
            var flashingInvalid = Time.unscaledTime < _invalidFlashUntil;
            if (flashingInvalid) SetResolvedColor(InvalidColor);
            else if (_wasFlashingInvalid) SetResolvedColor(RestingColor);
            _wasFlashingInvalid = flashingInvalid;
            _baseScale = Mathf.Lerp(_baseScale, _targetScale, cursorScaleLerpSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / cursorScaleLerpSeconds));
            UpdateClickPulse(Time.unscaledDeltaTime);

            PushToHud();
        }

        private void PushToHud()
        {
            var hud = ScreenspaceHud.Active;
            if (hud == null) return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            Vector2? position = _followPointer && mouse != null ? mouse.position.ReadValue() : (Vector2?)null;

            var t = cursorScaleLerpSeconds <= 0f
                ? 1f
                : 1f - Mathf.Exp(-Time.unscaledDeltaTime / cursorScaleLerpSeconds);
            _displayedColor = Color.Lerp(_displayedColor, _resolvedColor, t);
            _fill = Mathf.Lerp(_fill, _followPointer && _primaryItemOut ? 1f : 0f, t);

            hud.ApplyCursor(
                visible: _cursorVisible,
                color: _displayedColor,
                fill: _fill,
                screenPosition: position,
                scale: Mathf.Max(0.01f, _baseScale * _pulseScale));
        }

        public void SetClickHeld(bool held) => _clickHeld = held;

        private void UpdateClickPulse(float dt)
        {
            var target = _clickHeld ? clickPulseScale : 1f;
            var t = clickPulseSeconds <= 0f ? 1f : 1f - Mathf.Exp(-dt / clickPulseSeconds);
            _pulseScale = Mathf.Lerp(_pulseScale, target, t);
        }

        private void SetPointerFollow(bool follow, bool tabletMode)
        {
            _followPointer = follow;
            _targetScale = follow && tabletMode ? tabletCursorScale : 1f;

            SetResolvedColor(RestingColor);

            if (follow) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR)
                mouse.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }

        private void OnEnable()
        {
            PrimaryItemState.OnEventRaised += SetCursorAccordingToPrimaryItemState;
            PlayerEvents.MenuStateChanged += SetCursorAccordingToMainMenuState;
            PlayerEvents.ScreenFadeChanged += OnScreenFadeChanged;
            PlayerEvents.InvalidActionSignaled += FlashInvalidAction;
            BroadcastControlsStatus.SendControlScheme += OnSchemeChangedSetCursor;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            PrimaryItemState.OnEventRaised -= SetCursorAccordingToPrimaryItemState;
            PlayerEvents.MenuStateChanged -= SetCursorAccordingToMainMenuState;
            PlayerEvents.ScreenFadeChanged -= OnScreenFadeChanged;
            PlayerEvents.InvalidActionSignaled -= FlashInvalidAction;
            BroadcastControlsStatus.SendControlScheme -= OnSchemeChangedSetCursor;
        }

        private void OnScreenFadeChanged(bool _) => ResolveCursor();

        private void Init()
        {
            if (GetComponent<GamepadScreenCursor>() == null) gameObject.AddComponent<GamepadScreenCursor>();
            if (GetComponent<UiEventDebugOverlay>() == null) gameObject.AddComponent<UiEventDebugOverlay>();
            if (GetComponent<DesktopWorldUiInteractor>() == null) gameObject.AddComponent<DesktopWorldUiInteractor>();
            if (FindAnyObjectByType<GazeDesktopClick>(FindObjectsInactive.Include) == null)
            {
                var gate = FindAnyObjectByType<TrackedPoseSchemeGate>(FindObjectsInactive.Include);
                var gazeController = gate != null
                    ? gate.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>(true)
                    : null;
                if (gazeController != null)
                {
                    gazeController.gameObject.AddComponent<GazeDesktopClick>();
                    if(isDebug) Debug.Log($"[CursorStateController] added GazeDesktopClick to '{gazeController.name}' — " +
                        "this Player (variant)'s gaze rig predates it. It is the locked-mode ray's click/drag/scroll and " +
                        "the gamepad's only UI path. Add it to the variant's Gaze Interactor to silence this.", gazeController);
                }
            }

            if (isDebug) Debug.Log("[CursorStateController] Changing cursor in init");
            ResolveCursor();
        }

        private void OnSchemeChangedSetCursor(BroadcastControlsStatus.ControlScheme _) => ResolveCursor();

        private void SetCursorAccordingToPrimaryItemState(bool state)
        {
            if (isDebug) Debug.Log("[CursorStateController] Changing cursor because of primary item state ");
            _primaryItemOut = state;
            ResolveCursor();
        }

        private void SetCursorAccordingToMainMenuState(bool state)
        {
            if (isDebug) Debug.Log("[CursorStateController] Changing cursor because of main menu state ");
            _menuOpen = state;
            ResolveCursor();
        }

        private void ResolveCursor()
        {
            var scheme = BroadcastControlsStatus.controlScheme;
            if (scheme == BroadcastControlsStatus.ControlScheme.XR
                || scheme == BroadcastControlsStatus.ControlScheme.Freecam)
                SetCursor(CursorState.Off);
            else if (_menuOpen)
                SetCursor(CursorState.OnConstrained);
            else if (FadeMask.ScreenFaded)
                SetCursor(CursorState.Off);
            else if (_primaryItemOut)
                SetCursor(CursorState.OnConstrained);
            else
                SetCursor(CursorState.OnLocked);
        }

        private void SetCursor(CursorState state)
        {
            if (isDebug) Debug.Log("[CursorStateController] Setting cursor to " + state);
            _cursorVisible = state != CursorState.Off;
            Cursor.visible = false;
            switch (state)
            {
                case CursorState.OnConstrained:
                    Cursor.lockState = CursorLockMode.Confined;
                    SetPointerFollow(true, _primaryItemOut);
                    PlayerEvents.RaiseMouselookState(false);
                    break;
                case CursorState.OnLocked:
                    Cursor.lockState = CursorLockMode.Locked;
                    SetPointerFollow(false, false);
                    PlayerEvents.RaiseMouselookState(true);
                    break;
                case CursorState.Off:
                    Cursor.lockState = CursorLockMode.Locked;
                    _followPointer = false;
                    PlayerEvents.RaiseMouselookState(false);
                    break;
            }
        }
    }
}