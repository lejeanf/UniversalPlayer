using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
#pragma warning disable 0618 // ActionBasedController: deprecated in XRI 3, still what the gaze rig runs on

namespace jeanf.universalplayer
{
    /// <summary>
    /// Lets the center-screen gaze ray CLICK, DRAG and SCROLL on desktop. The gaze
    /// controller ships with empty UI Press/Scroll actions (in VR the hand rays do
    /// the clicking): mouse clicks only worked because the locked cursor parks the
    /// mouse pointer at screen center — a pointer that can never MOVE, so presses it
    /// captured could never drag a slider, and gamepads had no click path at all.
    /// This injects desktop bindings into the gaze controller at startup and, while
    /// the cursor is locked, turns the UI module's mouse pointer OFF so the moving
    /// gaze ray is the one and only pointer (drags and scroll views work). The mouse
    /// pointer returns whenever the cursor is freed (menus, tablet).
    /// </summary>
    [RequireComponent(typeof(ActionBasedController))]
    public class GazeDesktopClick : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("Desktop control paths added to the gaze controller's UI Press action when it has none of its own.")]
        [SerializeField] private string[] uiPressBindings =
        {
            "<Mouse>/leftButton",
            "<Gamepad>/buttonSouth",
            "<Gamepad>/rightTrigger",
        };

        [Tooltip("Desktop control paths added to the gaze controller's UI Scroll action when it has none of its own.")]
        [SerializeField] private string[] uiScrollBindings =
        {
            "<Mouse>/scroll",
        };

        private XRUIInputModule uiModule;
        private bool moduleSearched;

        private void Awake()
        {
            var controller = GetComponent<ActionBasedController>();
            PrepareAction(controller.uiPressAction, uiPressBindings, "UI Press");
            PrepareAction(controller.uiScrollAction, uiScrollBindings, "UI Scroll");
        }

        private void PrepareAction(InputActionProperty property, string[] paths, string label)
        {
            var action = property.action;
            InjectBindings(action, paths, label);

            // REFERENCED actions are enabled by NOBODY unless the rig has an
            // InputActionManager listing the asset — XRI controllers only
            // auto-enable their DIRECT (embedded) actions. A disabled UI Press
            // is the silent half-broken UI: hover works (it is positional) but
            // no click/drag ever registers.
            if (action != null && property.reference != null && !action.enabled)
                action.Enable();
        }

        private void OnEnable()
        {
            PlayerEvents.MouselookStateChanged += OnMouselookStateChanged;
        }

        private void Start()
        {
            // The cursor starts locked in desktop modes (CursorStateController.Init).
            OnMouselookStateChanged(true);
        }

        private void OnDisable()
        {
            PlayerEvents.MouselookStateChanged -= OnMouselookStateChanged;
            OnMouselookStateChanged(false); // hand the pointer back to the mouse
        }

        private void InjectBindings(InputAction action, string[] paths, string label)
        {
            if (action == null)
            {
                Debug.LogWarning($"{LogPrefix} GazeDesktopClick on '{name}': the controller has no {label} action at all — " +
                    "that part of desktop ray interaction stays disabled.", this);
                return;
            }
            if (action.bindings.Count > 0) return; // project wired its own — leave it alone

            var wasEnabled = action.enabled;
            if (wasEnabled) action.Disable();
            foreach (var path in paths) action.AddBinding(path);
            if (wasEnabled) action.Enable();
        }

        // While mouselook is ON the cursor is locked: the frozen mouse pointer would
        // capture UI presses ahead of the gaze ray and drags would never move.
        private void OnMouselookStateChanged(bool canLook)
        {
            if (!moduleSearched)
            {
                moduleSearched = true;
                uiModule = FindFirstObjectByType<XRUIInputModule>(FindObjectsInactive.Include);
            }
            if (uiModule == null) return;
            uiModule.enableMouseInput = !canLook;
        }
    }
}
