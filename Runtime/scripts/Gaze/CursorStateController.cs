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

        [Tooltip("LEGACY optional. The cursor is now the ScreenspaceUI HUD's #Cursor element (pure styling, no sprite). " +
                 "Leave empty once the old cursor canvas is deleted; assign only to keep the old SVG reticle alive too.")]
        [SerializeField] private SVGImage cursorImage;
        private static SVGImage _cursorImage;
        [Tooltip("LEGACY optional — the validation fill moves to the HUD cursor's radial fill.")]
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
        [Tooltip("The cursor's RESTING colour, in every mode (tablet included — tablet changes shape, not colour). " +
                 "Hover / click / invalid below take over on top of it.")]
        [SerializeField] private Color normalCursorColor = Color.white;
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
        /// <summary>
        /// The reticle's resting color — what ReticleHoverFeedback returns to when it stops
        /// hovering. ONE colour scheme for every mode: tablet mode differs in SHAPE (filled
        /// and smaller), never in colour, so hover/click/invalid read identically wherever
        /// the cursor is.
        /// </summary>
        public Color RestingColor => normalCursorColor;

        // The resolved cursor, held HERE rather than read back off the SVGImage. That is
        // what lets the legacy cursor canvas be deleted: the HUD renders from these, and
        // the image (while it still exists) is just another output.
        private Color _resolvedColor = Color.white;
        private bool _cursorVisible = true;
        // Eased toward the resolved state so default <-> tablet is a transition, not a snap.
        private Color _displayedColor = Color.white;
        private float _fill;

        /// <summary>
        /// The FINAL reticle colour. ReticleHoverFeedback stays the hover/click authority
        /// and pushes its result here; this component forwards it to the HUD (and to the
        /// legacy image while one is assigned).
        /// </summary>
        public void SetResolvedColor(Color color)
        {
            _resolvedColor = color;
            if (_cursorImage != null) _cursorImage.color = color;
        }

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
            // The invalid-request flash owns the reticle color while active (over hover,
            // click and resting); when it ends, snap back to the current resting color.
            // Routed through SetResolvedColor so the HUD sees it — the image is optional.
            var flashingInvalid = Time.unscaledTime < _invalidFlashUntil;
            if (flashingInvalid) SetResolvedColor(invalidCursorColor);
            else if (_wasFlashingInvalid) SetResolvedColor(RestingColor);
            _wasFlashingInvalid = flashingInvalid;

            // Smooth size in/out (tablet shrinks the pointer, exiting restores it),
            // composed with the one-shot click pulse. Base and pulse are tracked
            // separately so the pulse can multiply on top without fighting the lerp.
            _baseScale = Mathf.Lerp(_baseScale, _targetScale, cursorScaleLerpSeconds <= 0f ? 1f : 1f - Mathf.Exp(-Time.unscaledDeltaTime / cursorScaleLerpSeconds));
            UpdateClickPulse(Time.unscaledDeltaTime);

            // The HUD IS the cursor: it positions and scales itself from this state, so it
            // must be driven before (and independently of) any legacy image.
            PushToHud();

            // ---- everything below only drives the LEGACY SVG cursor canvas ----
            // It is optional: with the canvas deleted, the HUD above is the whole cursor.
            if (_cursorImage == null) return;
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

        /// <summary>
        /// Mirrors the resolved cursor onto the UI Toolkit HUD (ScreenspaceHud).
        ///
        /// It reads the image's FINAL colour/enabled state on purpose: ReticleHoverFeedback
        /// and the invalid flash already resolve those, so the HUD renders what they decided
        /// instead of duplicating the rules. Tablet mode (free pointer + item out) also FILLS
        /// the ring's background, per design.
        ///
        /// This is the migration bridge: while the legacy cursor canvas still exists it is
        /// the state source. Cutting the canvas means moving the colour authority off the
        /// SVGImage onto this component — see the HUD migration notes.
        /// </summary>
        private void PushToHud()
        {
            var hud = ScreenspaceHud.Active;
            if (hud == null) return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            Vector2? position = _followPointer && mouse != null ? mouse.position.ReadValue() : (Vector2?)null;

            // Ease default <-> tablet on ONE curve (cursorScaleLerpSeconds), so the size,
            // the colour and the ring->filled-dot change all travel together instead of
            // snapping. Frame-rate independent (exponential), like the size lerp already was.
            var t = cursorScaleLerpSeconds <= 0f
                ? 1f
                : 1f - Mathf.Exp(-Time.unscaledDeltaTime / cursorScaleLerpSeconds);
            _displayedColor = Color.Lerp(_displayedColor, _resolvedColor, t);
            _fill = Mathf.Lerp(_fill, _followPointer && _primaryItemOut ? 1f : 0f, t);

            // _baseScale is already eased toward _targetScale (1 or the tablet size) and
            // defaults to 1, so it needs no legacy image to be correct.
            hud.ApplyCursor(
                visible: _cursorVisible,
                color: _displayedColor,
                fill: _fill,
                screenPosition: position,
                scale: Mathf.Max(0.01f, _baseScale * _pulseScale));
        }

        private void CaptureCursorHomeOnce()
        {
            if (_homeCaptured || _cursorImage == null) return;
            _homeCaptured = true;
            _cursorHomePosition = _cursorImage.rectTransform.localPosition;
            _authoredCursorSprite = _cursorImage.sprite;
            // NOTE: _baseScale is deliberately NOT seeded from the image's authored scale.
            // It is the logical size (1 = default, tabletCursorScale = tablet) that the HUD
            // eases on, and must stay correct once the legacy canvas is gone.
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

            // Resting colour — through SetResolvedColor so the HUD gets it with or without
            // a legacy image. Same colour in every mode (see RestingColor); tablet only
            // changes shape. ReticleHoverFeedback overrides it while hovering; this is the
            // base it returns to.
            SetResolvedColor(RestingColor);

            // Recentring the hardware pointer is image-independent: the next unlock must
            // start from the default position whether or not the old canvas exists.
            if (!follow)
            {
                var mouse = UnityEngine.InputSystem.Mouse.current;
                if (mouse != null && BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR)
                    mouse.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }

            // ---- legacy SVG cursor only (sprite swap + home position) ----
            if (_cursorImage == null) return;
            var targetSprite = follow && tabletMode ? EffectiveTabletSprite : EffectiveNormalSprite;
            if (targetSprite != null) _cursorImage.sprite = targetSprite;
            if (!follow) _cursorImage.rectTransform.localPosition = _cursorHomePosition;
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
        ///  VR / FreeCam                → Off (headset owns the pointer in VR; FreeCam
        ///                                is a free-fly camera with no cursor)
        ///  menu open                   → OnConstrained (the menu UI needs the
        ///                                cursor, even over the menu's black fade)
        ///  world black (load/teleport) → Off (nothing to point at)
        ///  primary item out            → OnConstrained (free cursor for the tablet)
        ///  otherwise                   → OnLocked (first-person look)
        /// </summary>
        private void ResolveCursor()
        {
            var scheme = BroadcastControlsStatus.controlScheme;
            if (scheme == BroadcastControlsStatus.ControlScheme.XR
                || scheme == BroadcastControlsStatus.ControlScheme.Freecam)
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
            var scheme = BroadcastControlsStatus.controlScheme;
            if (scheme == BroadcastControlsStatus.ControlScheme.XR
                || scheme == BroadcastControlsStatus.ControlScheme.Freecam) state = CursorState.Off;
            // Held here (not read back off the image) so the HUD keeps working once the
            // legacy cursor canvas is gone.
            _cursorVisible = state != CursorState.Off;
            switch (state)
            {
                case CursorState.OnConstrained:
                    // The OS arrow stays HIDDEN: the package cursor image is the
                    // pointer (same icon in M&K and gamepad, scalable/tintable).
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Confined;
                    if (_cursorImage != null) _cursorImage.enabled = true;
                    SetPointerFollow(true, _primaryItemOut);
                    if (validationFeedbackImage != null)
                    {
                        if (validationFeedbackImage != null) validationFeedbackImage.enabled = false;
                    }
                    PlayerEvents.RaiseMouselookState(false);
                    break;
                case CursorState.OnLocked:
                    Cursor.visible = false; // the OS arrow is NEVER shown — the reticle is the pointer
                    Cursor.lockState = CursorLockMode.Locked;
                    if (_cursorImage != null) _cursorImage.enabled = true;
                    SetPointerFollow(false, false);
                    if (validationFeedbackImage != null)
                    {
                        if (validationFeedbackImage != null) validationFeedbackImage.enabled = true;
                    }
                    PlayerEvents.RaiseMouselookState(true);
                    break;
                case CursorState.Off:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    if (_cursorImage != null) _cursorImage.enabled = false;
                    _followPointer = false;
                    if (validationFeedbackImage != null)
                    {
                        if (validationFeedbackImage != null) validationFeedbackImage.enabled = false;
                    }
                    PlayerEvents.RaiseMouselookState(false);
                    break;
                default:
                    Cursor.visible = false;
                    Cursor.lockState = CursorLockMode.Locked;
                    if (_cursorImage != null) _cursorImage.enabled = true;
                    if (validationFeedbackImage != null)
                    {
                        if (validationFeedbackImage != null) validationFeedbackImage.enabled = true;
                    }
                    PlayerEvents.RaiseMouselookState(true);
                    break;
            }
            //cursorStateChannel.RaiseEvent((int)state);
        }
    }
}