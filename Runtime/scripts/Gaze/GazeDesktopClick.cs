using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
#pragma warning disable 0618 // inputCompatibilityMode: XRI 3 compat surface, needed to demote the rig's controller path

namespace jeanf.universalplayer
{
    /// <summary>
    /// ONE screen pointer for all desktop UI — the same method for M&amp;K and
    /// gamepad ("same aim, same information, different mappings"): the
    /// XRUIInputModule's screen pointer. Its position is the OS cursor —
    /// pinned at screen center while the cursor is locked (exactly where the
    /// reticle points), stick-warped by <see cref="GamepadScreenCursor"/> in
    /// free-cursor modes — and its click is mouse left / gamepad A / right
    /// trigger. This is the transport that has always worked for M&amp;K;
    /// gamepad now rides the identical one.
    ///
    /// WHY a runtime-built action asset: the module's serialized point/click
    /// actions live in the player's input asset, which PlayerInput scheme-masks
    /// and device-pairs — under the Gamepad scheme every Mouse binding stops
    /// matching and that pointer silently dies (the historical "works in M&amp;K,
    /// dead on gamepad"). The replacement actions here belong to no user, no
    /// scheme and no mask: they cannot be turned off by a scheme switch.
    ///
    /// The gaze ray is demoted to hover/visual feedback (reticle tint): its UI
    /// readers are forced to ManualValue and never queued, so it can never
    /// double-fire a click on top of the screen pointer.
    /// </summary>
    public class GazeDesktopClick : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("Control paths that CLICK the screen pointer (all devices — scheme-mask-immune).")]
        [SerializeField] private string[] clickBindings =
        {
            "<Mouse>/leftButton",
            "<Gamepad>/buttonSouth",
            "<Gamepad>/rightTrigger",
        };

        [Tooltip("Control paths whose Vector2 scrolls the UI under the pointer.")]
        [SerializeField] private string[] scrollBindings =
        {
            "<Mouse>/scroll",
        };

        private InputActionAsset runtimeAsset;
        private InputAction pointAction;
        private InputAction clickAction;
        private InputAction scrollAction;
        private XRRayInteractor rayInteractor;
        private XRUIInputModule uiModule;
        private bool moduleSearched;
        private bool moduleWired;
        private float nextRegistrationCheck;
        private bool registrationHealed;

        // Latched telemetry for the F9 overlay (screenshot timing can't hide events).
        internal int DebugPressEventCount;
        internal float DebugLastPressEventTime = -1f;
        internal InputAction DebugClickAction => clickAction;
        internal InputAction DebugPointAction => pointAction;
        internal XRRayInteractor DebugRay => rayInteractor;

        private void Awake()
        {
            // Scheme-immune UI actions (see class docs). They live in their own
            // runtime asset because InputActionReference requires asset membership —
            // and precisely because it is NOT the player's asset, PlayerInput can
            // never mask or device-pair them away.
            runtimeAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            runtimeAsset.name = "GazeDesktopUI (runtime)";
            var map = runtimeAsset.AddActionMap("UI");
            pointAction = map.AddAction("point", InputActionType.Value, expectedControlLayout: "Vector2");
            pointAction.AddBinding("<Mouse>/position");
            // Code-created actions skip the initial-state check that asset actions
            // get: with the cursor LOCKED the position control never changes, so
            // without this the action reads (0,0) forever and every click lands in
            // the screen corner (observed live).
            pointAction.wantsInitialStateCheck = true;
            clickAction = map.AddAction("click", InputActionType.Button);
            foreach (var path in clickBindings) clickAction.AddBinding(path);
            scrollAction = map.AddAction("scroll", InputActionType.Value, expectedControlLayout: "Vector2");
            foreach (var path in scrollBindings) scrollAction.AddBinding(path);
            clickAction.performed += OnClickPerformed;

            // Demote the gaze ray to hover-only: ManualValue readers that are never
            // queued cannot press, and ForceInputReaders stops the rig's (scheme-
            // gated, desktop-suppressed) ActionBasedController from being consulted.
            rayInteractor = GetComponentInChildren<XRRayInteractor>(true);
            if (rayInteractor != null)
            {
                rayInteractor.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;
                rayInteractor.uiScrollInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
                rayInteractor.inputCompatibilityMode = XRBaseInputInteractor.InputCompatibilityMode.ForceInputReaders;
            }
        }

        private void OnEnable()
        {
            BroadcastControlsStatus.SendControlScheme += OnSchemeChanged;
            ApplySchemeState(BroadcastControlsStatus.controlScheme);
            WireModule();
        }

        private void OnDisable()
        {
            BroadcastControlsStatus.SendControlScheme -= OnSchemeChanged;
            if (runtimeAsset != null) runtimeAsset.Disable();
        }

        private void OnDestroy()
        {
            if (clickAction != null) clickAction.performed -= OnClickPerformed;
            if (runtimeAsset != null) Destroy(runtimeAsset);
        }

        private void OnClickPerformed(InputAction.CallbackContext _)
        {
            DebugPressEventCount++;
            DebugLastPressEventTime = Time.unscaledTime;
        }

        private void OnSchemeChanged(BroadcastControlsStatus.ControlScheme scheme) => ApplySchemeState(scheme);

        // Desktop schemes own the screen pointer. In VR the hand rays drive UI —
        // a stray gamepad press must not click whatever sits under the parked cursor.
        private void ApplySchemeState(BroadcastControlsStatus.ControlScheme scheme)
        {
            if (runtimeAsset == null) return;
            if (scheme == BroadcastControlsStatus.ControlScheme.XR) runtimeAsset.Disable();
            else runtimeAsset.Enable();
        }

        // Hand the module our scheme-immune actions. The module's property setters
        // handle unhooking the old references while running.
        private void WireModule()
        {
            if (moduleWired) return;
            var module = ResolveModule();
            if (module == null) return;
            moduleWired = true;
            module.pointAction = InputActionReference.Create(pointAction);
            module.leftClickAction = InputActionReference.Create(clickAction);
            module.scrollWheelAction = InputActionReference.Create(scrollAction);
            Debug.Log($"{LogPrefix} GazeDesktopClick: screen-pointer actions wired to '{module.gameObject.name}' — " +
                "point/click/scroll are now scheme-mask-immune (same transport for M&K and gamepad).", this);
        }

        private void Update()
        {
            if (!moduleWired) WireModule(); // EventSystem may come up after us

            // NOTE: no cursor warping here — the package must never move the
            // user's real OS cursor. The point action's initial-state check
            // carries the mouse position; in locked mode the pointer sits where
            // the OS pinned it (near center).

            // The reticle tint reads the ray's UI raycast, which requires the ray
            // to be REGISTERED with the module; registration happens in the ray's
            // OnEnable and silently fails when the module wasn't locatable at that
            // instant — check periodically and repair.
            if (rayInteractor != null && Time.unscaledTime >= nextRegistrationCheck)
            {
                nextRegistrationCheck = Time.unscaledTime + 1f;
                var module = ResolveModule();
                if (module != null && !module.GetTrackedDeviceModel(rayInteractor, out _))
                {
                    module.RegisterInteractor(rayInteractor);
                    if (!registrationHealed)
                    {
                        registrationHealed = true;
                        Debug.LogWarning($"{LogPrefix} GazeDesktopClick: the gaze ray was not registered with the " +
                            "XRUIInputModule — registered it now (hover feedback needs it).", this);
                    }
                }
            }
        }

        // The LIVE module first: a scene can hold more than one EventSystem (menu
        // prefabs, demo benches ship their own) — a cached FindFirstObjectByType
        // that landed on an inactive one would wire actions nobody reads.
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
