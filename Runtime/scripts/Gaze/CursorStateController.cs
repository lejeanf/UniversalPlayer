using jeanf.EventSystem;
using jeanf.validationTools;
using Unity.VectorGraphics;
using UnityEngine;
using UnityEngine.InputSystem;
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

        [Validation("Cursor image is required — it IS the pointer in free-cursor mode (menu/tablet). Without it there is no visible cursor.")]
        [SerializeField] private SVGImage cursorImage;
        private static SVGImage _cursorImage;
        [SerializeField] private SVGImage validationFeedbackImage;

        // Mouselook state is raised on PlayerEvents; the PlayerEventBridge forwards it.

        [Header("Listening on:")]
        [Validation("Primary-item state channel is required — the cursor frees for the tablet from it (and it is subscribed unguarded at startup).")]
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

        [Header("Cursor look (free-cursor pointer)")]
        [Tooltip("Optional override for the DEFAULT cursor sprite (every non-tablet state). Empty = keep the SVGImage's authored sprite.")]
        [SerializeField] private Sprite normalCursorSprite;
        [Tooltip("Optional override for the cursor sprite while the primary item (tablet) is drawn. Empty = use the normal cursor sprite.")]
        [SerializeField] private Sprite tabletCursorSprite;
        [SerializeField] private Color normalCursorColor = Color.white;
        [SerializeField] private Color tabletCursorColor = Color.white;
        [Tooltip("Cursor size while the primary item is drawn (lerped in and back out).")]
        [Range(0.1f, 1f)][SerializeField] private float tabletCursorScale = 0.5f;
        [SerializeField] private float cursorScaleLerpSeconds = 0.12f;
        [Tooltip("While the click is HELD, the reticle shrinks to this FRACTION of its size — and stays there until the button is released.")]
        [Range(0.1f, 1f)][SerializeField] private float clickPulseScale = 0.75f;
        [Tooltip("Seconds to ease into the pressed size (and back out again on release).")]
        [SerializeField] private float clickPulseSeconds = 0.09f;

        [Header("Cursor hover / click (interactables & tooltips)")]
        [Tooltip("Reticle color while aiming at anything usable — interactables, seats, pickables and tooltip objects. ReticleHoverFeedback reads this.")]
        [SerializeField] private Color hoverCursorColor = new Color(0.35f, 0.95f, 0.6f);
        [Tooltip("Reticle color while the interact/take button is held on something usable (the click flash). ReticleHoverFeedback reads this.")]
        [SerializeField] private Color clickCursorColor = new Color(1f, 0.85f, 0.25f);
        [Tooltip("Reticle color flashed when a click/interaction is REJECTED (invalid request). Trigger with PlayerEvents.RaiseInvalidAction().")]
        [SerializeField] private Color invalidCursorColor = new Color(1f, 0.28f, 0.38f); // pinkish red
        [Tooltip("How long (seconds) the invalid-request flash stays on the reticle before it returns to normal.")]
        [SerializeField] private float invalidFlashSeconds = 0.35f;

        // The cursor manager is the single source of truth for every reticle color:
        // normal / tablet (resting), hover and click. ReticleHoverFeedback queries
        // these so all four live on this one component under _settings.
        public Color HoverColor => hoverCursorColor;
        public Color ClickColor => clickCursorColor;
        /// <summary>The reticle's resting color for the CURRENT state (tablet vs normal) — what ReticleHoverFeedback returns to when it stops hovering.</summary>
        public Color RestingColor => _followPointer && _primaryItemOut ? tabletCursorColor : normalCursorColor;

        /// <summary>True while the invalid-request flash owns the reticle color — ReticleHoverFeedback yields to it.</summary>
        public bool IsFlashingInvalid => Time.unscaledTime < _invalidFlashUntil;

        /// <summary>
        /// Flash the reticle its "invalid request" (pinkish-red) color — call when a
        /// click or interaction is rejected. Also invoked by PlayerEvents.RaiseInvalidAction().
        /// Desktop (M&amp;K / gamepad) reticle feedback; VR hides the reticle.
        /// </summary>
        public void FlashInvalidAction()
        {
            _invalidFlashUntil = Time.unscaledTime + Mathf.Max(0.05f, invalidFlashSeconds);
            if (_cursorImage != null) _cursorImage.color = invalidCursorColor;
        }

        // The package reticle IS the pointer in free-cursor mode: the OS arrow is
        // hidden and the cursor image follows the (warped) mouse position — one
        // consistent icon in M&K and gamepad, scalable and tintable.
        private bool _followPointer;
        private float _targetScale = 1f;
        private float _baseScale = 1f;  // eased toward _targetScale (resting/tablet size)
        private float _pulseScale = 1f; // pressed-size factor, multiplied on top of _baseScale
        private bool _clickHeld;        // true for as long as the click is held down
        private Vector3 _cursorHomePosition;
        private Sprite _authoredCursorSprite; // whatever the SVGImage shipped with (fallback when no override)
        private bool _homeCaptured;
        private float _invalidFlashUntil = -1f;
        private bool _wasFlashingInvalid;

        // Effective sprites: the serialized overrides win; tablet falls back to the
        // normal sprite, which falls back to whatever the SVGImage was authored with.
        private Sprite EffectiveNormalSprite => normalCursorSprite != null ? normalCursorSprite : _authoredCursorSprite;
        private Sprite EffectiveTabletSprite => tabletCursorSprite != null ? tabletCursorSprite : EffectiveNormalSprite;

        private  void Awake() => Init();

        private void Update()
        {
            if (_cursorImage == null) return;

            // The invalid-request flash owns the reticle color while active (over hover,
            // click and resting); when it ends, snap back to the current resting color.
            var flashingInvalid = Time.unscaledTime < _invalidFlashUntil;
            if (flashingInvalid) _cursorImage.color = invalidCursorColor;
            else if (_wasFlashingInvalid) _cursorImage.color = RestingColor;
            _wasFlashingInvalid = flashingInvalid;

            // Smooth size in/out (tablet shrinks the pointer, exiting restores it),
            // composed with the one-shot click pulse. Base and pulse are tracked
            // separately so the pulse can multiply on top without fighting the lerp.
            _baseScale = Mathf.Lerp(_baseScale, _targetScale, cursorScaleLerpSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / cursorScaleLerpSeconds));
            UpdateClickPulse(Time.unscaledDeltaTime);
            _cursorImage.rectTransform.localScale = Vector3.one * (_baseScale * _pulseScale);

            if (!_followPointer) return;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return;
            var parentRect = _cursorImage.rectTransform.parent as RectTransform;
            if (parentRect == null) return;

            // LOCAL-space follow: convert the mouse position to a point in the parent
            // rect and drive localPosition. World-space math breaks here — the cursor
            // canvas rides the MOVING Main Camera and may be Screen Space - Camera
            // with no camera assigned (renders as overlay), so a world-point
            // projection lands off the visible plane. Local space is immune to the
            // canvas render mode, camera assignment and parent motion alike.
            var canvas = _cursorImage.canvas;
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera // null for camera-mode-without-camera = overlay fallback, which wants null
                : null;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, mouse.position.ReadValue(), eventCamera, out var localPoint))
            {
                var local = _cursorImage.rectTransform.localPosition;
                _cursorImage.rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, local.z);
            }
        }

        private void CaptureCursorHomeOnce()
        {
            if (_homeCaptured || _cursorImage == null) return;
            _homeCaptured = true;
            _cursorHomePosition = _cursorImage.rectTransform.localPosition;
            _authoredCursorSprite = _cursorImage.sprite;
            _baseScale = _cursorImage.rectTransform.localScale.x; // start from the authored size, no first-frame pop
        }

        /// <summary>
        /// Press state of the click, NOT a one-shot: the reticle eases down to
        /// <see cref="clickPulseScale"/> and STAYS there for as long as the button is
        /// held, easing back only on release — a press you keep holding (dragging a
        /// slider) must keep looking pressed. ReticleHoverFeedback drives this from the
        /// same press that turns the reticle its click color, so size and color agree.
        /// </summary>
        public void SetClickHeld(bool held) => _clickHeld = held;

        private void UpdateClickPulse(float dt)
        {
            var target = _clickHeld ? clickPulseScale : 1f;
            var t = clickPulseSeconds <= 0f ? 1f : 1f - Mathf.Exp(-dt / clickPulseSeconds);
            _pulseScale = Mathf.Lerp(_pulseScale, target, t);
        }

        // Free cursor: the reticle becomes the pointer. Locked: it returns to the
        // screen-center home, and the hardware pointer recenters so the NEXT
        // unlock starts from the default position (tablet unequip reset).
        private void SetPointerFollow(bool follow, bool tabletMode)
        {
            CaptureCursorHomeOnce();
            _followPointer = follow;
            _targetScale = follow && tabletMode ? tabletCursorScale : 1f;
            if (_cursorImage == null) return;

            _cursorImage.color = follow && tabletMode ? tabletCursorColor : normalCursorColor;
            var targetSprite = follow && tabletMode ? EffectiveTabletSprite : EffectiveNormalSprite;
            if (targetSprite != null) _cursorImage.sprite = targetSprite;

            if (!follow)
            {
                _cursorImage.rectTransform.localPosition = _cursorHomePosition;
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null && BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR)
                    mouse.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }
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

        public void Init()
        {
            _cursorImage = cursorImage;

            // Guard a footgun: the validation-fill image MUST be a different image from
            // the cursor. If they're the same object, disabling the fill (tablet/menu)
            // would disable the cursor itself — an invisible reticle that never follows
            // or shrinks. Ignore it so the cursor can never turn itself off.
            if (validationFeedbackImage != null && validationFeedbackImage == cursorImage)
            {
                Debug.LogWarning($"[UniversalPlayer] CursorStateController on '{name}': validationFeedbackImage is the SAME " +
                    "SVGImage as cursorImage — that would make the reticle disable itself in tablet/menu mode. Ignoring it; " +
                    "assign a SEPARATE fill image or leave the field empty.", this);
                validationFeedbackImage = null;
            }

            // Gamepad users get a stick-driven screen cursor whenever the cursor
            // frees up (menu, tablet) — no prefab wiring needed.
            if (GetComponent<GamepadScreenCursor>() == null) gameObject.AddComponent<GamepadScreenCursor>();
            if (GetComponent<UiEventDebugOverlay>() == null) gameObject.AddComponent<UiEventDebugOverlay>();
            // World-space canvases (TrackedDeviceGraphicRaycaster) are invisible to the
            // module's screen pointer — this component is their desktop click/drag path.
            if (GetComponent<DesktopWorldUiInteractor>() == null) gameObject.AddComponent<DesktopWorldUiInteractor>();

            // Locked-mode UI (click, drag, scroll) rides the gaze ray through
            // GazeDesktopClick — it is the GAMEPAD'S ONLY UI path. Player variants
            // whose gaze rig predates the component miss it silently (M&K limps
            // along on the frozen center mouse pointer, gamepad UI is dead), so
            // self-heal it onto the gaze controller like the pieces above.
            if (FindFirstObjectByType<GazeDesktopClick>(FindObjectsInactive.Include) == null)
            {
                var gate = FindFirstObjectByType<TrackedPoseSchemeGate>(FindObjectsInactive.Include);
                var gazeController = gate != null
                    ? gate.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>(true)
                    : null;
                if (gazeController != null)
                {
                    gazeController.gameObject.AddComponent<GazeDesktopClick>();
                    Debug.Log($"[UniversalPlayer] CursorStateController: added GazeDesktopClick to '{gazeController.name}' — " +
                        "this Player (variant)'s gaze rig predates it. It is the locked-mode ray's click/drag/scroll and " +
                        "the gamepad's only UI path. Add it to the variant's Gaze Interactor to silence this.", gazeController);
                }
            }

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
                    // The OS arrow stays HIDDEN: the package cursor image is the
                    // pointer (same icon in M&K and gamepad, scalable/tintable).
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Confined;
                    _cursorImage.enabled = true;
                    SetPointerFollow(true, _primaryItemOut);
                    if (validationFeedbackImage != null)
                    {
                        validationFeedbackImage.enabled = false;
                    }
                    PlayerEvents.RaiseMouselookState(false);
                    break;
                case CursorState.OnLocked:
                    Cursor.visible = false; // the OS arrow is NEVER shown — the reticle is the pointer
                    Cursor.lockState = CursorLockMode.Locked;
                    _cursorImage.enabled = true;
                    SetPointerFollow(false, false);
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
                    _followPointer = false;
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