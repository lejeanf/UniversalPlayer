using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
#pragma warning disable 0618 // ActionBasedController: deprecated in XRI 3, still what the gaze rig runs on

namespace jeanf.universalplayer
{
    /// <summary>
    /// Lets the center-screen gaze ray CLICK, DRAG and SCROLL on desktop — for
    /// M&amp;K and gamepad alike (one ray, one pointer; only the press button
    /// differs per device).
    ///
    /// XRI 3 note — there are TWO possible press paths and which one is live
    /// depends on the rig (XRRayInteractor.UpdateUIModel):
    ///   - deprecated path (forceDeprecatedInput, active when an
    ///     ActionBasedController sits on the rig): select comes from the
    ///     CONTROLLER's uiPressAction;
    ///   - modern path: select comes from the ray's OWN uiPressInput reader.
    /// This component drives BOTH — bindings injected into the controller action
    /// AND ManualValue queuing on the ray's readers — so the ray clicks on any
    /// variant regardless of which mode XRI resolves. (Each path alone was
    /// observed dead on a rig using the other one.)
    ///
    /// While the cursor is locked, the UI module's mouse pointer is turned OFF so
    /// the moving gaze ray is the one and only pointer (a frozen center pointer
    /// would capture presses and could never drag). The mouse pointer returns
    /// whenever the cursor frees up (menus, tablet) — and both press paths stand
    /// down so the ray cannot ghost-click world UI behind an open menu.
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
        private InputAction controllerPressAction;  // deprecated path (rig's ActionBasedController)
        private InputAction controllerScrollAction;
        private XRUIInputModule uiModule;
        private bool moduleSearched;
        private bool rayIsPointer; // cursor locked: the gaze ray owns UI presses
        private float nextRegistrationCheck;
        private bool registrationHealed;

        // Latched telemetry for the F9 overlay (screenshot timing can't hide events).
        internal int DebugPressEventCount;
        internal float DebugLastPressEventTime = -1f;
        internal bool DebugRayIsPointer => rayIsPointer;
        internal XRRayInteractor DebugRay => rayInteractor;
        internal InputAction DebugControllerPress => controllerPressAction;

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

            // Deprecated path: with an ActionBasedController on the rig, XRI reads
            // select from the CONTROLLER's uiPressAction and ignores the reader we
            // queue above — inject the same desktop bindings there too.
            var controller = GetComponent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>()
                             ?? GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.ActionBasedController>();
            if (controller != null)
            {
                controllerPressAction = PrepareControllerAction(controller.uiPressAction, uiPressBindings);
                controllerScrollAction = PrepareControllerAction(controller.uiScrollAction, uiScrollBindings);
            }
        }

        // Injects desktop bindings into a controller action that has none of its
        // own, and makes sure it is enabled (REFERENCED actions are enabled by
        // nobody unless an InputActionManager lists their asset).
        private static InputAction PrepareControllerAction(InputActionProperty property, string[] paths)
        {
            var action = property.action;
            if (action == null) return null;
            if (action.bindings.Count == 0)
            {
                var wasEnabled = action.enabled;
                if (wasEnabled) action.Disable();
                foreach (var path in paths) action.AddBinding(path);
                if (wasEnabled) action.Enable();
            }
            if (property.reference != null && !action.enabled) action.Enable();
            return action;
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
            DebugPressEventCount++; // counted BEFORE gating: distinguishes "action dead" from "gated out"
            DebugLastPressEventTime = Time.unscaledTime;
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

            // A ray that never REGISTERED with the module is a pointer the
            // EventSystem has never heard of: hover impossible, presses go
            // nowhere no matter what is queued. Registration happens in the
            // interactor's OnEnable and silently fails when the module wasn't
            // locatable at that moment (enable order, inactive EventSystem) —
            // check periodically and repair.
            if (rayIsPointer && Time.unscaledTime >= nextRegistrationCheck)
            {
                nextRegistrationCheck = Time.unscaledTime + 1f;
                var module = ResolveModule();
                if (module != null && !module.GetTrackedDeviceModel(rayInteractor, out _))
                {
                    module.RegisterInteractor(rayInteractor);
                    if (!registrationHealed)
                    {
                        registrationHealed = true;
                        Debug.LogWarning($"{LogPrefix} GazeDesktopClick: the gaze ray was NOT registered with the " +
                            "XRUIInputModule — registered it now. UI hover/click through the ray was impossible before this.", this);
                    }
                }
            }
        }

        // While mouselook is ON the cursor is locked: the gaze ray is the pointer
        // and the module's frozen mouse pointer must stand down entirely.
        private void OnMouselookStateChanged(bool canLook)
        {
            rayIsPointer = canLook
                && BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR;
            if (!rayIsPointer && rayInteractor != null)
                rayInteractor.uiPressInput.QueueManualState(false, 0f);

            // The deprecated (controller) path has no per-press gate — enable it
            // only while the ray owns the pointer, or it would ghost-click world
            // UI at screen center while a menu/tablet is open.
            if (controllerPressAction != null)
            {
                if (rayIsPointer) controllerPressAction.Enable();
                else controllerPressAction.Disable();
            }
            if (controllerScrollAction != null)
            {
                if (rayIsPointer) controllerScrollAction.Enable();
                else controllerScrollAction.Disable();
            }

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
