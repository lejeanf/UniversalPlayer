using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Lets the center-screen gaze ray CLICK, DRAG and SCROLL on desktop — for
    /// M&amp;K and gamepad alike (one ray, one pointer; only the press button
    /// differs per device).
    ///
    /// XRI 3 note: the ray interactor reads its OWN <c>uiPressInput</c> reader —
    /// injecting bindings into the ActionBasedController's deprecated uiPressAction
    /// fires the action but NOBODY consumes it (verified live: PRESSED NOW true,
    /// zero clicks). So this component switches the ray's press/scroll readers to
    /// ManualValue and drives them directly from its own desktop bindings — the
    /// same mechanism UiInteractionTests validate against a standard UGUI Button.
    ///
    /// While the cursor is locked, the UI module's mouse pointer is turned OFF so
    /// the moving gaze ray is the one and only pointer (a frozen center pointer
    /// would capture presses and could never drag). The mouse pointer returns
    /// whenever the cursor frees up (menus, tablet).
    /// </summary>
    public class GazeDesktopClick : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("Desktop control paths that PRESS the gaze ray (click, drag).")]
        [SerializeField] private string[] uiPressBindings =
        {
            "<Mouse>/leftButton",
            "<Gamepad>/buttonSouth",
            "<Gamepad>/rightTrigger",
        };

        [Tooltip("Desktop control paths whose Vector2 feeds the gaze ray's UI scroll.")]
        [SerializeField] private string[] uiScrollBindings =
        {
            "<Mouse>/scroll",
        };

        private XRRayInteractor rayInteractor;
        private InputAction pressAction;
        private InputAction scrollAction;
        private XRUIInputModule uiModule;
        private bool moduleSearched;
        private bool rayIsPointer; // cursor locked: the gaze ray owns UI presses

        private void Awake()
        {
            rayInteractor = GetComponentInChildren<XRRayInteractor>(true);
            if (rayInteractor == null)
            {
                Debug.LogWarning($"{LogPrefix} GazeDesktopClick on '{name}': no XRRayInteractor on or under this object — " +
                    "desktop ray click/drag/scroll stays disabled.", this);
                enabled = false;
                return;
            }

            // The ray's readers become manually driven: our press/scroll actions
            // below are their only source on desktop. (In VR the hand rays do the
            // clicking — nothing queues while a desktop scheme isn't active.)
            rayInteractor.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
            rayInteractor.uiScrollInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;

            pressAction = new InputAction("GazeDesktopUiPress", InputActionType.Button);
            foreach (var path in uiPressBindings) pressAction.AddBinding(path);
            pressAction.performed += OnPressPerformed;
            pressAction.canceled += OnPressCanceled;

            scrollAction = new InputAction("GazeDesktopUiScroll", InputActionType.Value, expectedControlType: "Vector2");
            foreach (var path in uiScrollBindings) scrollAction.AddBinding(path);
        }

        private void OnEnable()
        {
            PlayerEvents.MouselookStateChanged += OnMouselookStateChanged;
            pressAction?.Enable();
            scrollAction?.Enable();
            // Re-assert on EVERY enable: Start only runs once per lifetime, so a
            // disable->enable cycle of the gaze rig (scheme gates) would otherwise
            // leave the module's mouse pointer on and the ray press-less.
            OnMouselookStateChanged(Cursor.lockState == CursorLockMode.Locked);
        }

        private void Start()
        {
            // Awake/OnEnable order across components is not guaranteed — by Start
            // the CursorStateController has locked the cursor for desktop modes.
            OnMouselookStateChanged(Cursor.lockState == CursorLockMode.Locked);
        }

        private void OnDisable()
        {
            PlayerEvents.MouselookStateChanged -= OnMouselookStateChanged;
            if (rayInteractor != null) rayInteractor.uiPressInput.QueueManualState(false, 0f); // never leave a press latched
            pressAction?.Disable();
            scrollAction?.Disable();
            OnMouselookStateChanged(false); // hand the pointer back to the mouse
        }

        private void OnDestroy()
        {
            pressAction?.Dispose();
            scrollAction?.Dispose();
        }

        private void OnPressPerformed(InputAction.CallbackContext _)
        {
            if (rayIsPointer && rayInteractor != null)
                rayInteractor.uiPressInput.QueueManualState(true, 1f);
        }

        private void OnPressCanceled(InputAction.CallbackContext _)
        {
            // Always release, even if the pointer role changed mid-press.
            if (rayInteractor != null)
                rayInteractor.uiPressInput.QueueManualState(false, 0f);
        }

        private void Update()
        {
            if (rayInteractor == null) return;
            var scroll = rayIsPointer ? scrollAction.ReadValue<Vector2>() : Vector2.zero;
            // Hardware wheels report ±120 per notch on some platforms; UGUI scroll
            // expects roughly ±1 "lines" per tick (thumbstick range).
            if (Mathf.Abs(scroll.y) > 10f || Mathf.Abs(scroll.x) > 10f) scroll /= 120f;
            rayInteractor.uiScrollInput.manualValue = scroll;
        }

        // While mouselook is ON the cursor is locked: the gaze ray is the pointer
        // and the module's frozen mouse pointer must stand down entirely.
        private void OnMouselookStateChanged(bool canLook)
        {
            rayIsPointer = canLook
                && BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR;
            if (!rayIsPointer && rayInteractor != null)
                rayInteractor.uiPressInput.QueueManualState(false, 0f);

            var module = ResolveModule();
            if (module != null) module.enableMouseInput = !canLook;
        }

        // The LIVE module first: a scene can hold more than one EventSystem (menu
        // prefabs, demo benches ship their own) — a cached FindFirstObjectByType
        // that landed on an inactive one would toggle a module nobody uses while
        // the real pointer keeps stealing presses.
        private XRUIInputModule ResolveModule()
        {
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null && eventSystem.currentInputModule is XRUIInputModule active)
                return active;
            if (uiModule == null && !moduleSearched)
            {
                moduleSearched = true;
                uiModule = FindFirstObjectByType<XRUIInputModule>();
            }
            return uiModule;
        }
    }
}
