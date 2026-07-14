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
    /// F9 (or LB+Select) in play mode: on-screen dump of the whole UGUI event
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
            if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame) _visible = !_visible;
            if (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame
                && Gamepad.current.leftShoulder.isPressed) _visible = !_visible; // LB+Select on gamepad
        }

        private void OnGUI()
        {
            if (!_visible) return;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = false };

            _text.Length = 0;
            var eventSystem = EventSystem.current;
            _text.AppendLine("== UI EVENT DEBUG (F9 / LB+Select) ==");
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

            // XRI 3: what actually clicks is the RAY's own input readers (the
            // deprecated controller actions fire into the void) — inspect those.
            var ray = gazeClick.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(true);
            if (ray == null)
            {
                _text.AppendLine("  (no XRRayInteractor under GazeDesktopClick — nothing to press)");
                return;
            }
            _text.AppendLine($"  ray uiPressInput: mode:{ray.uiPressInput.inputSourceMode}  PRESSED NOW: {ray.uiPressInput.ReadIsPerformed()}   (hold your click/A while reading this!)");
            _text.AppendLine($"  ray uiScrollInput: mode:{ray.uiScrollInput.inputSourceMode}  value: {ray.uiScrollInput.ReadValue()}");

            // Latched telemetry — survives screenshot timing.
            var lastPress = gazeClick.DebugLastPressEventTime < 0f
                ? "never"
                : $"{Time.unscaledTime - gazeClick.DebugLastPressEventTime:F1}s ago";
            _text.AppendLine($"  press action fired: {gazeClick.DebugPressEventCount}x (last {lastPress})   rayIsPointer: {gazeClick.DebugRayIsPointer}");

            // Is this pointer even known to the EventSystem? Unregistered = hover
            // impossible and queued presses go nowhere.
            var xrModule = EventSystem.current != null ? EventSystem.current.currentInputModule as XRUIInputModule : null;
            var registered = xrModule != null && xrModule.GetTrackedDeviceModel(ray, out _);
            var uiHover = ray.TryGetCurrentUIRaycastResult(out var uiHit) && uiHit.gameObject != null
                ? uiHit.gameObject.name
                : "<none>";
            _text.AppendLine($"  ray registered with module: {registered}   ray UI hover: {uiHover}");
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
                // A TrackedDeviceGraphicRaycaster must not count as a mouse raycaster even if
                // it were ever assignable to GraphicRaycaster — only a plain one drives the mouse.
                var hasMouseRaycaster = graphic != null && !(graphic is TrackedDeviceGraphicRaycaster);
                var mouseOk = hasMouseRaycaster && graphic.enabled;
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
