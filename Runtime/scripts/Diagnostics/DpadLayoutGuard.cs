using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Works around a Unity OpenXR plugin bug that breaks gamepad hot-plugging.
    ///
    /// The OpenXR plugin registers its D-Pad interaction profile as a DEVICE
    /// layout named "DPad" — even when the D-Pad binding feature is disabled.
    /// Input System layout names are case-insensitive, so that registration
    /// silently replaces the built-in "Dpad" CONTROL layout. Every gamepad
    /// layout has a "dpad" child control built from that layout, so from then
    /// on NO gamepad can be created: turning a controller on mid-game fails with
    ///   "Cannot instantiate device layout 'Dpad' as child of
    ///    '/XInputControllerWindows'; devices must be added at root".
    /// (Unity issue tracker: "Connecting a controller causes a Could not create
    /// a device error when XR is set up in the project".)
    ///
    /// This guard re-registers the built-in <see cref="DpadControl"/> layout
    /// whenever the override is detected — once at startup and again on any
    /// later layout change (OpenXR re-registers its layouts when XR
    /// initializes), so a forgotten gamepad can always be switched on mid-game.
    /// Note the repair drops OpenXR's D-Pad interaction profile layout: if a
    /// project enables that OpenXR feature, thumbstick-as-dpad XR bindings and
    /// working gamepads cannot coexist until Unity fixes the plugin — this
    /// package chooses working gamepads.
    /// </summary>
    public static class DpadLayoutGuard
    {
        private const string LogPrefix = "[UniversalPlayer.Input]";
        private static bool _repairing;
        private static bool _repairScheduled;
        private static bool _warnedOnce;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            RepairIfNeeded();
            // OpenXR may (re)register the bad layout later — e.g. when the XR
            // loader initializes after a headset shows up. Watch for it.
            InputSystem.onLayoutChange -= OnLayoutChange;
            InputSystem.onLayoutChange += OnLayoutChange;
        }

#if UNITY_EDITOR
        // Also repair in edit mode: the same error spams the console when a
        // controller connects while working in the editor.
        [UnityEditor.InitializeOnLoadMethod]
        private static void InstallInEditor() => Install();
#endif

        private static void OnLayoutChange(string layoutName, InputControlLayoutChange change)
        {
            // NEVER repair from inside this callback. It fires synchronously from
            // within InputManager's layout registration — OpenXR registers its bad
            // DPad layout in the middle of XR loader init — and calling
            // RegisterLayout re-entrantly from there hangs the editor. Schedule
            // the repair for after the current input update instead.
            if (_repairing || _repairScheduled) return;
            if (change == InputControlLayoutChange.Removed) return;
            if (!string.Equals(layoutName, "Dpad", System.StringComparison.OrdinalIgnoreCase)) return;
            _repairScheduled = true;
            InputSystem.onAfterUpdate += RepairAfterUpdate;
        }

        private static void RepairAfterUpdate()
        {
            InputSystem.onAfterUpdate -= RepairAfterUpdate;
            _repairScheduled = false;
            RepairIfNeeded();
        }

        /// <summary>
        /// Restores the built-in "Dpad" control layout if something replaced it
        /// with a device layout. Returns true when a repair was performed.
        /// </summary>
        public static bool RepairIfNeeded()
        {
            InputControlLayout layout;
            try { layout = InputSystem.LoadLayout("Dpad"); }
            catch { return false; }
            if (layout == null || !layout.isDeviceLayout) return false; // healthy

            _repairing = true;
            try
            {
                InputSystem.RegisterLayout<DpadControl>("Dpad");
            }
            finally
            {
                _repairing = false;
            }

            if (!_warnedOnce)
            {
                _warnedOnce = true;
                Debug.LogWarning($"{LogPrefix} The OpenXR plugin replaced the built-in 'Dpad' control layout with a device " +
                                 "layout, which prevents ANY gamepad from being created (known Unity bug: \"Cannot instantiate " +
                                 "device layout 'Dpad'...\"). The built-in layout was restored automatically — gamepads can " +
                                 "now be plugged in or turned on at any time.");
            }
            return true;
        }
    }
}
