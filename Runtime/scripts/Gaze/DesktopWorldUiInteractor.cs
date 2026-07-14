using System;
using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace jeanf.universalplayer
{
    // Same CS0118 trap as UiEventDebugOverlay: the bare name 'EventSystem' resolves to
    // the sibling jeanf.EventSystem NAMESPACE before the UGUI component. Alias it here,
    // inside jeanf.universalplayer, so the component type wins.
    using EventSystem = UnityEngine.EventSystems.EventSystem;

    /// <summary>
    /// Click and click&amp;drag for WORLD-SPACE canvases in M&amp;K / gamepad mode.
    ///
    /// WHY: world-space canvases carry a TrackedDeviceGraphicRaycaster (for the XR
    /// ray), which only answers TrackedDeviceEventData — the XRUIInputModule screen
    /// pointer that GazeDesktopClick drives can NEVER see them, in any desktop
    /// scheme. The reticle still tints (that hover comes from the demoted gaze
    /// ray), so the symptom is "hover works, clicks dead". This component casts the
    /// player-camera ray itself — through screen center while the cursor is locked
    /// (exactly where the reticle sits), through the OS-cursor position in
    /// free-cursor/tablet mode (GamepadScreenCursor stick-warps it, the reticle
    /// follows) — hit-tests via EventSystem.RaycastAll with a TrackedDeviceEventData
    /// (so the raycasters already on those canvases answer, with their occlusion
    /// rules), and synthesizes the full UGUI pointer lifecycle via ExecuteEvents.
    ///
    /// Ownership partition — this is the whole anti-double-fire design. It is
    /// CURSOR-STATE dependent, because most project world canvases carry BOTH
    /// raycasters (plain GraphicRaycaster added historically as the only way to
    /// click them; its eventCamera falls back to Camera.main on world canvases):
    ///  - Cursor LOCKED: this component owns EVERY world-space canvas, whichever
    ///    raycaster answered — the module's pointer is pinned at center and can
    ///    click but never drag, so it is fully stood down (GazeDesktopClick
    ///    disables its click action while locked). Genuine screen-space UI
    ///    (overlay/camera canvases) still makes this component yield.
    ///  - Cursor FREE (tablet/menu): the module's moving pointer works fine, so
    ///    it keeps every canvas a plain GraphicRaycaster can reach (dual-raycaster
    ///    world canvases included) and this component only drives canvases that
    ///    are TrackedDeviceGraphicRaycaster-only; any plain-GraphicRaycaster hit
    ///    at the point makes it yield.
    ///
    /// Locked-mode drag: the screen point never moves (always center), so the
    /// usual (position - pressPosition) threshold would never trip. Same fix as
    /// XRUIInputModule's tracked devices: remember the WORLD press position and
    /// re-project it through the live camera every held frame — camera turn makes
    /// pressPosition drift away from center, the threshold fires, and Slider /
    /// ScrollRect math (position + pressEventCamera) works unmodified.
    ///
    /// Scroll is out of scope: on gamepad the right stick is look (locked) or
    /// cursor (constrained), so there is no free axis — and ScrollRects stay fully
    /// usable through drag. If a project wants it later: add a runtime Vector2
    /// action here (dpad / shoulders) and Execute scrollHandler on the hover target.
    ///
    /// Auto-added by <see cref="CursorStateController.Init"/> — no prefab wiring
    /// needed. 3D interactables are NOT handled here (TakeObject / SitController
    /// already raycast for them); they consult <see cref="UiHoverActive"/> so a
    /// press on world UI can't also grab/sit through the canvas.
    /// </summary>
    public class DesktopWorldUiInteractor : MonoBehaviour, IDebugBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Header("Press input (scheme-mask-immune, GazeDesktopClick pattern)")]
        [Tooltip("Control paths that press world-space UI. The gamepad paths deliberately match FPS/Interact so the reticle's click flash stays in sync.")]
        [SerializeField] private string[] pressBindings =
        {
            "<Gamepad>/buttonSouth",
            "<Gamepad>/rightTrigger",
            "<Mouse>/leftButton",
        };

        [Header("Raycast")]
        [Tooltip("Hover raycasts per second while idle. Press, held and release frames always raycast — this only throttles passive hover.")]
        [Range(5f, 60f)]
        [SerializeField] private float hoverHz = 20f;
        [Tooltip("How far the UI ray reaches (meters).")]
        [SerializeField] private float maxRayDistance = 10f;
        [Tooltip("Passed to the canvases' TrackedDeviceGraphicRaycasters (their occlusion checks).")]
        [SerializeField] private LayerMask raycastMask = ~0;
        [Tooltip("Multiplies EventSystem.pixelDragThreshold before a press turns into a drag — camera-stick aim is jittery, so slightly more than the mouse threshold.")]
        [SerializeField] private float dragThresholdMultiplier = 1.4f;

        [Header("Gamepad press comfort")]
        [Tooltip("While a gamepad press is held on UI, the LEFT stick nudges the pointer instead of walking (locomotion is locked for the press) — this is how sliders are dragged precisely. Pixels per second at full tilt.")]
        [SerializeField] private float stickDragSpeed = 800f;
        [Tooltip("Left-stick deadzone for the press-held pointer nudge.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float stickDeadzone = 0.15f;

        [SerializeField] private bool _isDebug = false;
        public bool isDebug { get => _isDebug; set => _isDebug = value; }

        /// <summary>True while the ray hovers world-space UI this component owns.
        /// TakeObject / SitController early-out on it so a UI press can't also
        /// grab/sit through the canvas.</summary>
        public static bool UiHoverActive => _active != null && _active.HasUiHover;

        /// <summary>
        /// The world-UI element currently under the reticle (null when none).
        /// Callers need this to tell "UI in front of the thing I want" from "UI that IS
        /// the thing I want": a tablet's screen is UI, and it must still be pickable.
        /// </summary>
        public static GameObject UiHoverTarget => _active != null ? _active.hoverTarget : null;
        private static DesktopWorldUiInteractor _active;

        public bool HasUiHover => hoverTarget != null;
        public GameObject CurrentHoverTarget => hoverTarget;

        // Test seams (BroadcastControlsStatus.HmdMountedProbe pattern — public,
        // the tests assembly has no InternalsVisibleTo).
        public Func<bool> PressProbe;
        public Func<Vector2> ScreenPointProbe;
        public Func<bool> FadedProbe; // FadeMask.ScreenFaded defaults to Loading=true in scenes with no FadeMask (tests)
        public Func<bool> LockedProbe; // default: Cursor.lockState == Locked (tests can't set the real lock state)
        public Func<Vector2> DragStickProbe; // default: left stick when scheme == Gamepad
        public bool ForceActiveForTests;

        // Latched telemetry for the F8 overlay (GazeDesktopClick pattern).
        internal int DebugPressCount;
        internal int DebugClickCount;
        internal bool DebugDragging => eventData != null && eventData.dragging;
        internal string DebugHoverName => hoverTarget != null ? hoverTarget.name : "<none>";
        internal InputAction DebugPressAction => pressAction;
        internal bool DebugSchemeActive => schemeActive;

        private InputActionAsset runtimeAsset;
        private InputAction pressAction;
        private InputAction stickAction; // left stick — pointer nudge while a press is held (gamepad)
        private TrackedDeviceEventData eventData;
        private EventSystem eventDataSystem; // the EventSystem eventData was built for
        // Clear of every id another pipeline uses: mouse -1..-3, touch >= 0, and the
        // XRUIInputModule's registered-device ids (small non-negative). Collision would
        // make UGUI merge our pointer state with theirs.
        private const int PointerId = -1737;
        private Camera playerCamera;
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
        private readonly List<Vector3> rayPoints = new List<Vector3>(2) { Vector3.zero, Vector3.zero };
        private bool schemeActive;
        private bool pressedLastFrame;
        private float nextHoverTime;
        private GameObject hoverTarget;
        private Vector3 pressWorldPosition; // re-projected each held frame (locked-mode drag threshold)
        private Vector2 stickPointerOffset; // accumulated left-stick nudge while the press is held
        private PlayerMovement playerMovement;
        private bool playerMovementSearched;
        private bool lockedLocomotionForPress;
        private bool locomotionLockedBefore; // restore value — a seated player must stay locked after a UI press
        private FpsCameraFeel cameraFeel;
        private bool cameraFeelSearched;
        private bool suppressedFeelForDrag; // bob/roll weight zeroed while a draggable is held

        private void Awake()
        {
            // Scheme-immune press action (see GazeDesktopClick's class docs): it lives
            // in its own runtime asset, so PlayerInput can never mask or device-pair it
            // away, and menu/tablet action-map toggles can never disable it.
            runtimeAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            runtimeAsset.name = "DesktopWorldUi (runtime)";
            var map = runtimeAsset.AddActionMap("UI");
            pressAction = map.AddAction("press", InputActionType.Button);
            foreach (var path in pressBindings) pressAction.AddBinding(path);
            stickAction = map.AddAction("stickNudge", InputActionType.Value, expectedControlLayout: "Vector2");
            stickAction.AddBinding("<Gamepad>/leftStick");
        }

        private void OnEnable()
        {
            _active = this;
            BroadcastControlsStatus.SendControlScheme += OnSchemeChanged;
            // The scheme event is edge-triggered and may have fired long before us.
            ApplyScheme(BroadcastControlsStatus.controlScheme);
        }

        private void OnDisable()
        {
            BroadcastControlsStatus.SendControlScheme -= OnSchemeChanged;
            CancelInteraction();
            if (runtimeAsset != null) runtimeAsset.Disable();
            if (_active == this) _active = null;
        }

        private void OnDestroy()
        {
            if (runtimeAsset != null) Destroy(runtimeAsset);
        }

        private void OnSchemeChanged(BroadcastControlsStatus.ControlScheme scheme) => ApplyScheme(scheme);

        private void ApplyScheme(BroadcastControlsStatus.ControlScheme scheme)
        {
            var active = scheme == BroadcastControlsStatus.ControlScheme.Gamepad
                         || scheme == BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            if (active == schemeActive) return;
            schemeActive = active;
            if (active)
            {
                runtimeAsset.Enable();
            }
            else
            {
                CancelInteraction(); // never strand a press/drag across a scheme switch
                runtimeAsset.Disable();
            }
            if (_isDebug) Debug.Log($"{LogPrefix} DesktopWorldUiInteractor: scheme {scheme} -> {(active ? "active" : "idle")}", this);
        }

        private void Update()
        {
            if (!schemeActive && !ForceActiveForTests) return;
            if (FadedProbe?.Invoke() ?? FadeMask.ScreenFaded) { CancelInteraction(); return; }

            var eventSystem = EventSystem.current;
            if (eventSystem == null) return;
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
                if (playerCamera == null) return;
            }
            // Menu prefabs can ship their own EventSystem and steal 'current' (see
            // GazeDesktopClick.ResolveModule): pointer state built for the old one is
            // stale — cancel and rebuild.
            if (eventData == null || eventDataSystem != eventSystem)
            {
                if (eventData != null) CancelInteraction();
                eventData = new TrackedDeviceEventData(eventSystem) { pointerId = PointerId };
                eventDataSystem = eventSystem;
            }

            var cursorLocked = LockedProbe?.Invoke() ?? Cursor.lockState == CursorLockMode.Locked;
            var pressed = PressProbe?.Invoke() ?? pressAction.IsPressed();
            var pressEdge = pressed && !pressedLastFrame;
            var releaseEdge = !pressed && pressedLastFrame;
            pressedLastFrame = pressed;

            // While a gamepad press is held, the left stick nudges the pointer
            // (locomotion is locked for the duration, so the stick is free) — this
            // is the precise way to drag sliders without turning the whole camera.
            if (pressEdge) stickPointerOffset = Vector2.zero;
            if (pressed)
            {
                var stick = DragStickProbe?.Invoke() ?? ReadDragStick();
                stickPointerOffset += stick * (stickDragSpeed * Time.unscaledDeltaTime);
            }

            var screenPoint = (ScreenPointProbe?.Invoke() ?? ResolveScreenPoint(cursorLocked))
                              + (pressed || releaseEdge ? stickPointerOffset : Vector2.zero);

            // Passive hover is throttled; anything involving a press gets a fresh
            // raycast every frame (a click right after a camera turn must be accurate,
            // and drags are per-frame).
            if (pressEdge || pressed || releaseEdge || Time.unscaledTime >= nextHoverTime)
            {
                nextHoverTime = Time.unscaledTime + 1f / hoverHz;
                UpdatePointerFrame(eventSystem, screenPoint, cursorLocked);
            }

            if (pressEdge && hoverTarget != null) ProcessPress(eventSystem);
            if (pressed && (eventData.pointerPress != null || eventData.pointerDrag != null)) ProcessMoveAndDrag(eventSystem);
            if (releaseEdge) ProcessRelease();
        }

        private Vector2 ReadDragStick()
        {
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.Gamepad) return Vector2.zero;
            var stick = stickAction.ReadValue<Vector2>();
            return stick.magnitude < stickDeadzone ? Vector2.zero : stick;
        }

        private Vector2 ResolveScreenPoint(bool cursorLocked)
        {
            // Locked (CursorStateController's OnLocked) = reticle pinned at center; the
            // center ray IS the camera-forward ray. Otherwise (OnConstrained: tablet or
            // menu) the OS cursor is the aim point — GamepadScreenCursor stick-warps it
            // and the reticle image follows it. Reading lockState directly avoids the
            // missed-initial-event race a MouselookStateChanged subscription would have.
            if (cursorLocked || Mouse.current == null)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            return Mouse.current.position.ReadValue();
        }

        /// <summary>One raycast + hover-chain update: fills eventData with the fresh
        /// ray, applies the cursor-state ownership partition (see class docs), and
        /// moves the pointerEnter chain.</summary>
        private void UpdatePointerFrame(EventSystem eventSystem, Vector2 screenPoint, bool cursorLocked)
        {
            var ray = playerCamera.ScreenPointToRay(screenPoint);
            rayPoints[0] = ray.origin;
            rayPoints[1] = ray.origin + ray.direction * maxRayDistance;
            eventData.rayPoints = rayPoints;
            eventData.rayHitIndex = -1;
            eventData.layerMask = raycastMask;
            eventData.delta = screenPoint - eventData.position;
            eventData.position = screenPoint;

            // One RaycastAll answers BOTH raycaster kinds: TrackedDeviceGraphicRaycasters
            // via rayPoints, plain GraphicRaycasters via position (their eventCamera on a
            // world canvas falls back to Camera.main even when worldCamera is unset).
            eventSystem.RaycastAll(eventData, raycastResults);

            var covered = false;
            var worldHit = default(RaycastResult);
            foreach (var result in raycastResults)
            {
                if (result.gameObject == null) continue;
                var isTracked = result.module is TrackedDeviceGraphicRaycaster;
                var isPlain = !isTracked && result.module is GraphicRaycaster;
                if (!isTracked && !isPlain) continue;

                var canvas = result.gameObject.GetComponentInParent<Canvas>();
                var isWorld = canvas != null && canvas.rootCanvas.renderMode == RenderMode.WorldSpace;

                if (!isWorld)
                {
                    // Genuine screen-space UI under the point (open menu, dialog): the
                    // module pipeline owns this press in every cursor state — yield.
                    if (isPlain) covered = true;
                    continue;
                }

                if (cursorLocked)
                {
                    // Locked: we own every world canvas — the module's pointer is pinned
                    // and stood down. Nearest hit wins, whichever raycaster answered
                    // (dual-raycaster canvases report through both; same GameObject).
                    if (result.distance > maxRayDistance) continue;
                    if (worldHit.gameObject == null || result.distance < worldHit.distance) worldHit = result;
                }
                else
                {
                    // Free cursor: the module's moving pointer owns everything a plain
                    // GraphicRaycaster can reach (dual canvases included) — we only
                    // drive TrackedDevice-only canvases and yield under any plain hit.
                    if (isPlain)
                    {
                        covered = true;
                    }
                    else if (worldHit.gameObject == null)
                    {
                        // TrackedDeviceGraphicRaycaster derives from BaseRaycaster, NOT
                        // GraphicRaycaster — so this only ever finds a real plain one.
                        var plain = canvas.rootCanvas.GetComponent<GraphicRaycaster>();
                        if (plain == null || !plain.enabled) worldHit = result;
                    }
                }
            }

            eventData.pointerCurrentRaycast = covered ? default : worldHit;
            UpdateHover(covered ? null : worldHit.gameObject);
        }

        /// <summary>Reimplements BaseInputModule.HandlePointerExitAndEnter (protected,
        /// unreachable from here): exit up the old chain to the common root, enter down
        /// the new chain, keep eventData.hovered in sync.</summary>
        private void UpdateHover(GameObject newTarget)
        {
            var previous = eventData.pointerEnter;
            if (previous == newTarget)
            {
                hoverTarget = newTarget;
                return;
            }

            var commonRoot = FindCommonRoot(previous != null ? previous.transform : null,
                newTarget != null ? newTarget.transform : null);

            for (var t = previous != null ? previous.transform : null; t != null && t != commonRoot; t = t.parent)
            {
                ExecuteEvents.Execute(t.gameObject, eventData, ExecuteEvents.pointerExitHandler);
                eventData.hovered.Remove(t.gameObject);
            }

            eventData.pointerEnter = newTarget;
            for (var t = newTarget != null ? newTarget.transform : null; t != null && t != commonRoot; t = t.parent)
            {
                ExecuteEvents.Execute(t.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
                eventData.hovered.Add(t.gameObject);
            }

            hoverTarget = newTarget;
            if (_isDebug && newTarget != null) Debug.Log($"{LogPrefix} DesktopWorldUiInteractor: hover '{newTarget.name}'", this);
        }

        private static Transform FindCommonRoot(Transform a, Transform b)
        {
            if (a == null || b == null) return null;
            for (var t1 = a; t1 != null; t1 = t1.parent)
                for (var t2 = b; t2 != null; t2 = t2.parent)
                    if (t1 == t2) return t1;
            return null;
        }

        /// <summary>Press half of StandaloneInputModule.ProcessMousePress.</summary>
        private void ProcessPress(EventSystem eventSystem)
        {
            var target = hoverTarget;
            eventData.eligibleForClick = true;
            eventData.delta = Vector2.zero;
            eventData.dragging = false;
            eventData.useDragThreshold = true;
            eventData.pressPosition = eventData.position;
            eventData.pointerPressRaycast = eventData.pointerCurrentRaycast;
            pressWorldPosition = eventData.pointerPressRaycast.worldPosition;

            // Mouse-click semantics: pressing something that isn't the current selection
            // deselects it (Selectables then select themselves in their own OnPointerDown).
            if (ExecuteEvents.GetEventHandler<ISelectHandler>(target) != eventSystem.currentSelectedGameObject)
                eventSystem.SetSelectedGameObject(null, eventData);

            var newPressed = ExecuteEvents.ExecuteHierarchy(target, eventData, ExecuteEvents.pointerDownHandler);
            if (newPressed == null) newPressed = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);

            var time = Time.unscaledTime;
            if (newPressed == eventData.lastPress && time - eventData.clickTime < 0.3f) ++eventData.clickCount;
            else eventData.clickCount = 1;
            eventData.clickTime = time;

            eventData.pointerPress = newPressed;
            eventData.rawPointerPress = target;
            eventData.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(target);
            if (eventData.pointerDrag != null)
            {
                ExecuteEvents.Execute(eventData.pointerDrag, eventData, ExecuteEvents.initializePotentialDrag);
                // Bob/roll wiggle the camera; with the aim ray pinned to screen center
                // that wiggle becomes slider jitter — ease the feel layer to zero for
                // the duration of the (potential) drag.
                SuppressCameraFeelForDrag();
            }

            LockLocomotionForPress();

            DebugPressCount++;
            if (_isDebug) Debug.Log($"{LogPrefix} DesktopWorldUiInteractor: press on '{target.name}' (pressHandler: {(newPressed != null ? newPressed.name : "<none>")})", this);
        }

        /// <summary>Per held frame: locked-mode-aware drag threshold, then the
        /// beginDrag/drag loop of StandaloneInputModule.ProcessDrag (minus its
        /// Cursor.lockState early-out — locked IS our nominal mode).</summary>
        private void ProcessMoveAndDrag(EventSystem eventSystem)
        {
            if (eventData.pointerDrag == null) return;

            // Locked mode: position is pinned at screen center, so drift the press
            // ANCHOR instead — re-project the world press point through the live
            // camera. Turning the camera moves pressPosition away from center and the
            // pixel threshold fires, exactly like XRUIInputModule's tracked devices.
            eventData.pressPosition = (Vector2)playerCamera.WorldToScreenPoint(pressWorldPosition);

            if (!eventData.dragging)
            {
                var threshold = eventSystem.pixelDragThreshold * dragThresholdMultiplier;
                if (!eventData.useDragThreshold
                    || (eventData.position - eventData.pressPosition).sqrMagnitude >= threshold * threshold)
                {
                    if (eventData.pointerPress != eventData.pointerDrag)
                    {
                        ExecuteEvents.Execute(eventData.pointerPress, eventData, ExecuteEvents.pointerUpHandler);
                        eventData.eligibleForClick = false;
                        eventData.pointerPress = null;
                        eventData.rawPointerPress = null;
                    }
                    ExecuteEvents.Execute(eventData.pointerDrag, eventData, ExecuteEvents.beginDragHandler);
                    eventData.dragging = true;
                    eventData.eligibleForClick = false;
                    if (_isDebug) Debug.Log($"{LogPrefix} DesktopWorldUiInteractor: drag begins on '{eventData.pointerDrag.name}'", this);
                }
            }

            if (eventData.dragging)
                ExecuteEvents.Execute(eventData.pointerDrag, eventData, ExecuteEvents.dragHandler);
        }

        /// <summary>Release half of StandaloneInputModule (ReleaseMouse): up, then
        /// click XOR drop, then endDrag, then clear.</summary>
        private void ProcessRelease()
        {
            var target = hoverTarget;
            ExecuteEvents.Execute(eventData.pointerPress, eventData, ExecuteEvents.pointerUpHandler);

            // Deliberate deviation from mouse semantics: a click still fires when the
            // ray merely slid OFF the pressed control (clickHandler == null) — gamepad
            // look-smoothing keeps the camera settling during a quick A-press, and a
            // small toggle is easy to drift off between press and release. Sliding onto
            // a DIFFERENT control still cancels, like a mouse.
            var clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            if (eventData.pointerPress != null && eventData.eligibleForClick
                && (clickHandler == eventData.pointerPress || clickHandler == null))
            {
                ExecuteEvents.Execute(eventData.pointerPress, eventData, ExecuteEvents.pointerClickHandler);
                DebugClickCount++;
                if (_isDebug) Debug.Log($"{LogPrefix} DesktopWorldUiInteractor: click on '{eventData.pointerPress.name}'", this);
            }
            else if (eventData.pointerDrag != null && eventData.dragging)
            {
                ExecuteEvents.ExecuteHierarchy(target, eventData, ExecuteEvents.dropHandler);
            }

            if (eventData.pointerDrag != null && eventData.dragging)
                ExecuteEvents.Execute(eventData.pointerDrag, eventData, ExecuteEvents.endDragHandler);

            // Desktop UI is pointer-driven: never leave the control SELECTED after the
            // press — any surviving navigation source (joystick, arrow keys) would keep
            // editing it while the player just walks around.
            if (eventDataSystem != null && eventDataSystem.currentSelectedGameObject != null)
                eventDataSystem.SetSelectedGameObject(null, eventData);

            eventData.pointerPress = null;
            eventData.rawPointerPress = null;
            eventData.pointerDrag = null;
            eventData.dragging = false;
            eventData.eligibleForClick = false;
            stickPointerOffset = Vector2.zero;
            RestoreLocomotionAfterPress();
            RestoreCameraFeelAfterDrag();
            nextHoverTime = 0f; // fresh hover next frame — release may have moved UI (e.g. closed a panel)
        }

        /// <summary>Scheme exit / disable / fade mid-interaction: release without a
        /// click, end any drag, exit the hover chain, forget the press.</summary>
        private void CancelInteraction()
        {
            if (eventData == null)
            {
                pressedLastFrame = false;
                return;
            }
            if (pressedLastFrame)
            {
                eventData.eligibleForClick = false;
                ExecuteEvents.Execute(eventData.pointerPress, eventData, ExecuteEvents.pointerUpHandler);
                if (eventData.pointerDrag != null && eventData.dragging)
                    ExecuteEvents.Execute(eventData.pointerDrag, eventData, ExecuteEvents.endDragHandler);
                // Same as ProcessRelease: a canceled press must not leave a selected
                // control behind for navigation to keep editing.
                if (eventDataSystem != null && eventDataSystem.currentSelectedGameObject != null)
                    eventDataSystem.SetSelectedGameObject(null, eventData);
            }
            eventData.pointerPress = null;
            eventData.rawPointerPress = null;
            eventData.pointerDrag = null;
            eventData.dragging = false;
            eventData.eligibleForClick = false;
            stickPointerOffset = Vector2.zero;
            RestoreLocomotionAfterPress();
            RestoreCameraFeelAfterDrag();
            UpdateHover(null);
            pressedLastFrame = false;
        }

        /// <summary>Gamepad only: a press held on UI locks walking — the left stick is
        /// the pointer nudge for the duration (dragging a slider must not stroll the
        /// player away from the panel). Restores the PREVIOUS lock so a seated player
        /// (SitController holds the lock) stays locked after the press.</summary>
        private void LockLocomotionForPress()
        {
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.Gamepad) return;
            if (lockedLocomotionForPress) return;
            if (playerMovement == null && !playerMovementSearched)
            {
                playerMovementSearched = true;
                playerMovement = FindFirstObjectByType<PlayerMovement>();
            }
            if (playerMovement == null) return;
            locomotionLockedBefore = playerMovement.LocomotionLocked;
            playerMovement.LocomotionLocked = true;
            lockedLocomotionForPress = true;
        }

        private void RestoreLocomotionAfterPress()
        {
            if (!lockedLocomotionForPress) return;
            lockedLocomotionForPress = false;
            if (playerMovement != null) playerMovement.LocomotionLocked = locomotionLockedBefore;
        }

        /// <summary>Eases FpsCameraFeel (bob/roll/dip) to zero while a draggable is
        /// held — the feel motion wiggles the locked aim ray, which reads as slider
        /// jitter. Both schemes: M&K drags steer by mouselook and wiggle the same way.</summary>
        private void SuppressCameraFeelForDrag()
        {
            if (suppressedFeelForDrag) return;
            if (cameraFeel == null && !cameraFeelSearched)
            {
                cameraFeelSearched = true;
                cameraFeel = FindFirstObjectByType<FpsCameraFeel>();
            }
            if (cameraFeel == null) return;
            cameraFeel.TargetWeight = 0f;
            suppressedFeelForDrag = true;
        }

        private void RestoreCameraFeelAfterDrag()
        {
            if (!suppressedFeelForDrag) return;
            suppressedFeelForDrag = false;
            if (cameraFeel != null) cameraFeel.TargetWeight = 1f;
        }
    }
}
