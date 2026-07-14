using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
#pragma warning disable 0618 // ActionBasedController: deprecated in XRI 3, still what the gaze rig runs on

namespace jeanf.universalplayer
{
    // This assembly references the sibling namespace jeanf.EventSystem (SO channels).
    // Resolving the bare name 'EventSystem' climbs out to 'jeanf' and finds that
    // NAMESPACE before any file-level alias, giving CS0118. The alias must live HERE,
    // inside jeanf.universalplayer, so it is found first and wins.
    using EventSystem = UnityEngine.EventSystems.EventSystem;

    /// <summary>
    /// F8 (or LB+Select) in play mode: on-screen dump of the whole UGUI event
    /// pipeline, built to answer "the hover ring reacts but the UI does nothing"
    /// in M&amp;K / gamepad. It shows, top to bottom:
    ///  1. the active input module and whether it processes MOUSE / GAMEPAD input
    ///     (an XRUIInputModule with mouse OFF, or a module with no gamepad-bound
    ///     click, cannot drive desktop UI no matter what the canvas does);
    ///  2. every root Canvas and which RAYCASTER it carries, with a verdict —
    ///     a world-space canvas with only a TrackedDeviceGraphicRaycaster (VR
    ///     ray/poke) and NO GraphicRaycaster is invisible to the mouse pointer,
    ///     so the desktop cursor can never click it (the reticle still tints,
    ///     because that tint comes from the separate physics ray);
    ///  3. EventSystem.RaycastAll from the mouse position AND the screen center,
    ///     naming the raycaster that answered.
    /// Reading it: 0 hits at the mouse over a control that visibly tints => a
    /// raycaster problem (case 2). Hits show but clicks do nothing => the press
    /// pipeline (case 1: module mouse/gamepad input, click bindings).
    /// </summary>
    public class UiEventDebugOverlay : MonoBehaviour
    {
        private bool _visible;
        private readonly List<RaycastResult> _results = new List<RaycastResult>();
        private readonly StringBuilder _text = new StringBuilder(4096);
        private GUIStyle _style;

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame) _visible = !_visible;
            if (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame
                && Gamepad.current.leftShoulder.isPressed) _visible = !_visible; // LB+Select on gamepad
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false };

            _text.Length = 0;
            var eventSystem = EventSystem.current;
            _text.AppendLine("== UI EVENT DEBUG (F8 / LB+Select) ==");
            _text.AppendLine($"scheme: {BroadcastControlsStatus.controlScheme}   cursor lock: {Cursor.lockState}   visible: {Cursor.visible}");
            var mouse = Mouse.current;
            _text.AppendLine($"mouse: {(mouse != null ? mouse.position.ReadValue().ToString("F0") : "<no Mouse device>")}   gamepad: {(Gamepad.current != null ? "present" : "none")}");
            _text.AppendLine("");

            if (eventSystem == null)
            {
                _text.AppendLine("NO EventSystem in the scene — no UGUI event can EVER fire. Add one (EventSystem + an input module).");
            }
            else
            {
                AppendModule(eventSystem);
                _text.AppendLine("");
                AppendGazePressPipeline();
                _text.AppendLine("");
                AppendWorldUiInteractor();
                _text.AppendLine("");
                AppendPickup();
                _text.AppendLine("");
                AppendCanvasInventory();
                _text.AppendLine("");
                var mousePosition = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
                AppendRaycast(eventSystem, "MOUSE pointer", mousePosition);
                AppendRaycast(eventSystem, "screen center (gaze)", new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            }

            var rect = new Rect(10, 10, 940, 640);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12), _text.ToString(), _style);
        }

        // ---- 1. the input module and whether it can process desktop input ----
        private void AppendModule(EventSystem eventSystem)
        {
            var module = eventSystem.currentInputModule;
            _text.AppendLine($"EventSystem: '{eventSystem.gameObject.name}'   module: {(module != null ? module.GetType().Name : "NULL — none active!")}");
            _text.AppendLine($"selected: {(eventSystem.currentSelectedGameObject != null ? eventSystem.currentSelectedGameObject.name : "<none>")}");

            switch (module)
            {
                case XRUIInputModule xr:
                    _text.AppendLine($"  XRUIInputModule — enableMouseInput: {(xr.enableMouseInput ? "yes" : "NO (desktop mouse cannot drive UI!)")}"
                        + $"   enableGamepadInput: {xr.enableGamepadInput}   enableTouchInput: {xr.enableTouchInput}   enableXRInput: {xr.enableXRInput}");
                    AppendClickAction("  leftClick", xr.leftClickAction != null ? xr.leftClickAction.action : null);
                    break;
                case InputSystemUIInputModule ui:
                    _text.AppendLine("  InputSystemUIInputModule (standard). Note: GamepadScreenCursor only injects gamepad clicks into an XRUIInputModule — "
                        + "gamepad A/trigger won't click through THIS module unless its Left Click action is gamepad-bound.");
                    AppendClickAction("  point", ui.point != null ? ui.point.action : null);
                    AppendClickAction("  leftClick", ui.leftClick != null ? ui.leftClick.action : null);
                    break;
                case null:
                    _text.AppendLine("  (no active module — the EventSystem is disabled or its module component is missing)");
                    break;
                default:
                    _text.AppendLine("  (unrecognized module type — mouse/gamepad capability unknown)");
                    break;
            }
        }

        private void AppendClickAction(string label, InputAction action)
        {
            if (action == null)
            {
                _text.AppendLine($"{label}: <unbound>");
                return;
            }
            var hasGamepad = false;
            var hasMouse = false;
            foreach (var b in action.bindings)
            {
                if (b.effectivePath != null && b.effectivePath.Contains("Gamepad")) hasGamepad = true;
                if (b.effectivePath != null && b.effectivePath.Contains("Mouse")) hasMouse = true;
            }
            _text.AppendLine($"{label}: '{action.name}'  enabled:{action.enabled}  bindings:{action.bindings.Count}  mouse:{(hasMouse ? "yes" : "no")}  gamepad:{(hasGamepad ? "yes" : "NO")}");
        }

        // ---- 1b. the gaze-ray press pipeline (the pointer in locked desktop mode) ----
        private void AppendGazePressPipeline()
        {
            var moduleCount = Object.FindObjectsByType<XRUIInputModule>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            if (moduleCount > 1)
                _text.AppendLine($"!! {moduleCount} XRUIInputModules exist (incl. inactive) — duplicate EventSystems can hijack the pointer.");

            var gazeClick = Object.FindFirstObjectByType<GazeDesktopClick>(FindObjectsInactive.Include);
            if (gazeClick == null)
            {
                _text.AppendLine("GazeDesktopClick: MISSING — locked-mode ray has no click/drag/scroll path (M&K falls back to the frozen mouse pointer, gamepad has nothing).");
                return;
            }
            _text.AppendLine($"GazeDesktopClick: on '{gazeClick.gameObject.name}'  enabled:{gazeClick.enabled}  activeInHierarchy:{gazeClick.gameObject.activeInHierarchy}");

            // THE transport: the module's screen pointer, driven by GazeDesktopClick's
            // scheme-immune actions (same method for M&K and gamepad).
            var point = gazeClick.DebugPointAction;
            var click = gazeClick.DebugClickAction;
            _text.AppendLine(point != null
                ? $"  screen pointer POINT: enabled:{point.enabled}  value:{point.ReadValue<Vector2>():F0}"
                : "  screen pointer POINT: <not created>");
            _text.AppendLine(click != null
                ? $"  screen pointer CLICK: bindings:{click.bindings.Count}  enabled:{click.enabled}  PRESSED NOW: {click.IsPressed()}   (hold your click/A while reading this!)"
                : "  screen pointer CLICK: <not created>");
            var lastPress = gazeClick.DebugLastPressEventTime < 0f
                ? "never"
                : $"{Time.unscaledTime - gazeClick.DebugLastPressEventTime:F1}s ago";
            _text.AppendLine($"  click events fired: {gazeClick.DebugPressEventCount}x (last {lastPress})");

            // The gaze ray is hover/tint feedback only (demoted from clicking).
            var ray = gazeClick.DebugRay;
            if (ray != null)
            {
                var xrModule = EventSystem.current != null ? EventSystem.current.currentInputModule as XRUIInputModule : null;
                var registered = xrModule != null && xrModule.GetTrackedDeviceModel(ray, out _);
                var uiHover = ray.TryGetCurrentUIRaycastResult(out var uiHit) && uiHit.gameObject != null
                    ? uiHit.gameObject.name
                    : "<none>";
                _text.AppendLine($"  gaze ray (hover feedback only): registered:{registered}  UI hover: {uiHover}");
            }
        }

        // ---- 1c. the world-canvas click path (TrackedDeviceGraphicRaycaster canvases) ----
        private void AppendWorldUiInteractor()
        {
            var interactor = Object.FindFirstObjectByType<DesktopWorldUiInteractor>(FindObjectsInactive.Include);
            if (interactor == null)
            {
                _text.AppendLine("DesktopWorldUiInteractor: MISSING — world-space canvases (TrackedDevice-only) have NO desktop click/drag path.");
                return;
            }
            _text.AppendLine($"DesktopWorldUiInteractor: on '{interactor.gameObject.name}'  enabled:{interactor.enabled}  "
                + $"activeInHierarchy:{interactor.gameObject.activeInHierarchy}  scheme-active:{interactor.DebugSchemeActive}");
            var press = interactor.DebugPressAction;
            _text.AppendLine(press != null
                ? $"  press: bindings:{press.bindings.Count}  enabled:{press.enabled}  PRESSED NOW: {press.IsPressed()}   (hold your click/A while reading this!)"
                : "  press: <not created>");
            _text.AppendLine($"  world-UI hover: {interactor.DebugHoverName}   presses: {interactor.DebugPressCount}x   clicks: {interactor.DebugClickCount}x   dragging: {interactor.DebugDragging}");
        }

        // ---- 1d. the PICKUP chain (why "I click the tablet and nothing happens") ----
        // Pickup dies silently in four places: no action, UI swallowed the press, the
        // raycast missed (wrong layer / out of range / no collider), or the collider had
        // no PickableObject on it or its parents. Walk the whole chain, live.
        private void AppendPickup()
        {
            var take = Object.FindFirstObjectByType<TakeObject>(FindObjectsInactive.Include);
            if (take == null)
            {
                _text.AppendLine("PICKUP: no TakeObject in the scene — nothing can be picked up.");
                return;
            }

            var action = take.DebugTakeAction;
            _text.AppendLine($"PICKUP (TakeObject on '{take.gameObject.name}')  holding: "
                + $"{(take.DebugObjectInHand != null ? $"'{take.DebugObjectInHand.name}'" : "<nothing>")}");
            _text.AppendLine(action != null
                ? $"  take action: '{action.name}'  enabled:{action.enabled}  PRESSED NOW: {action.IsPressed()}   (hold your click while reading this!)"
                : "  take action: <NOT ASSIGNED — no press can ever arrive, pickup is dead>");

            var mask = take.DebugLayerMask;
            _text.AppendLine($"  layer mask: {MaskToNames(mask)}   max distance: {take.DebugMaxDistance:0.##}m");
            if (take.DebugUiOwnsPress)
            {
                _text.AppendLine("  >> BLOCKED: world UI owns this press (and it is not the face of a pickable).");
                return;
            }

            var camera = take.DebugCamera;
            if (camera == null) { _text.AppendLine("  >> Main Camera not assigned — the grab ray has no origin."); return; }

            // The exact ray Take() casts.
            var mouse = Mouse.current;
            var pointer = mouse != null
                ? (Vector3)mouse.position.ReadValue()
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            var ray = camera.ScreenPointToRay(pointer);

            if (Physics.Raycast(ray, out var masked, take.DebugMaxDistance, mask))
            {
                var pickable = masked.collider.GetComponentInParent<PickableObject>();
                _text.AppendLine($"  masked ray HIT '{masked.collider.name}' at {masked.distance:0.##}m "
                    + $"(layer '{LayerMask.LayerToName(masked.collider.gameObject.layer)}')");
                _text.AppendLine(pickable != null
                    ? $"  >> PickableObject: '{pickable.name}' (slot {pickable.Slot}, anchor {pickable.Anchor}) — a click SHOULD take it."
                    : "  >> NO PickableObject on that collider or its parents — add one (or its collider belongs to something else).");
                return;
            }

            // Missed. Say what is actually in front, which turns "nothing" into a cause.
            if (!Physics.Raycast(ray, out var any, 25f, ~0, QueryTriggerInteraction.Collide))
            {
                _text.AppendLine("  >> the grab ray hits NOTHING within 25m. Does the target have a Collider? "
                    + "(A world-space Canvas is NOT hittable by a physics raycast.)");
                return;
            }

            var layer = any.collider.gameObject.layer;
            var inMask = (mask.value & (1 << layer)) != 0;
            var found = any.collider.GetComponentInParent<PickableObject>();
            _text.AppendLine($"  masked ray MISSED. Nearest collider ahead: '{any.collider.name}' at {any.distance:0.##}m, "
                + $"layer '{LayerMask.LayerToName(layer)}'");
            _text.AppendLine($"  >> PickableObject on it: {(found != null ? $"YES ('{found.name}')" : "NO")}"
                + $"   layer in mask: {(inMask ? "yes" : "NO  <-- THE PROBLEM: add this layer to TakeObject's Layer Mask")}"
                + $"   in range: {(any.distance <= take.DebugMaxDistance ? "yes" : $"NO ({any.distance:0.##}m > {take.DebugMaxDistance:0.##}m)")}");
        }

        private static string MaskToNames(LayerMask mask)
        {
            if (mask.value == 0) return "<Nothing — the grab raycast can NEVER hit anything>";
            var names = new List<string>();
            for (var i = 0; i < 32; i++)
            {
                if ((mask.value & (1 << i)) == 0) continue;
                var name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }
            return names.Count > 0 ? string.Join(", ", names) : mask.value.ToString();
        }

        // ---- 2. every canvas and whether the mouse can click it ----
        private void AppendCanvasInventory()
        {
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _text.AppendLine($"Canvases (active, root): probing raycasters...");
            var shown = 0;
            foreach (var canvas in canvases)
            {
                if (!canvas.isRootCanvas) continue;
                if (shown++ >= 12) { _text.AppendLine("   ...(more canvases omitted)"); break; }

                var graphic = canvas.GetComponent<GraphicRaycaster>();          // mouse / screen pointer
                var tracked = canvas.GetComponent<TrackedDeviceGraphicRaycaster>(); // XR ray / poke
                // TrackedDeviceGraphicRaycaster derives from BaseRaycaster, NOT from
                // GraphicRaycaster, so GetComponent<GraphicRaycaster>() can only ever
                // return a real (mouse-driving) one. That is exactly why a TrackedDevice-
                // only canvas is invisible to the screen pointer.
                var mouseOk = graphic != null && graphic.enabled;
                var verdict = mouseOk
                    ? "mouse: OK"
                    : (tracked != null ? "mouse: NO GraphicRaycaster -> desktop cursor CANNOT click (VR-only canvas)"
                                       : "mouse: NO raycaster at all -> nothing can click");
                _text.AppendLine($"   '{canvas.name}'  [{canvas.renderMode}]  GraphicRaycaster:{(graphic != null ? (graphic.enabled ? "on" : "disabled") : "-")}  "
                    + $"TrackedDevice:{(tracked != null ? "yes" : "-")}   => {verdict}");
            }
            if (shown == 0) _text.AppendLine("   (no active root canvases found)");
        }

        // ---- 3. what the raycasters actually return at a screen point ----
        private void AppendRaycast(EventSystem eventSystem, string label, Vector2 screenPosition)
        {
            var pointer = new PointerEventData(eventSystem) { position = screenPosition };
            _results.Clear();
            eventSystem.RaycastAll(pointer, _results);
            _text.AppendLine($"RaycastAll @ {label} ({screenPosition.x:F0},{screenPosition.y:F0}): {_results.Count} hit(s)");
            for (var i = 0; i < Mathf.Min(4, _results.Count); i++)
            {
                var result = _results[i];
                var raycaster = result.module != null ? result.module.GetType().Name : "?";
                _text.AppendLine($"   [{i}] {result.gameObject.name}   via {raycaster}   dist {result.distance:F2}");
            }
            if (_results.Count == 0)
                _text.AppendLine("   (nothing — no UGUI raycaster answered the mouse here. If a control visibly tints, that tint is the PHYSICS reticle, not UGUI.)");
        }
    }
}
