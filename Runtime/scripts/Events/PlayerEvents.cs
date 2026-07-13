using System;
using jeanf.EventSystem;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The player's internal delegate surface. Package components raise and subscribe
    /// HERE — plain C# events: zero inspector wiring, compile-checked names, no
    /// per-frame ScriptableObject indirection. SO event channels exist only at the
    /// project boundary, piped in both directions by <see cref="PlayerEventBridge"/>
    /// (the single place channel assets are referenced).
    ///
    /// House rule applies: subscribe and unsubscribe with NAMED methods only.
    /// (Control-scheme changes stay on <see cref="BroadcastControlsStatus.SendControlScheme"/>,
    /// which already followed this pattern before the bridge existed.)
    /// </summary>
    public static class PlayerEvents
    {
        // ---- outbound: the package raises, the bridge forwards to project channels ----
        public static event Action<bool> HmdStateChanged;
        public static event Action<bool> HmdConnectionChanged;
        public static event Action<string> XrIssueReported;
        public static event Action<bool> PlayerMovingChanged;
        public static event Action<bool> SeatedChanged;
        public static event Action<string> FallRecovered;
        public static event Action MapTogglePressed;
        public static event Action InventoryTogglePressed;

        // ---- bidirectional: raised internally AND by project channels ----
        public static event Action CameraResetRequested;

        // ---- inbound: the bridge forwards project channel raises, internals react ----
        public static event Action<bool> MouselookStateChanged;
        public static event Action<bool> MenuStateChanged;
        public static event Action<bool> SceneLoadingChanged;
        public static event Action<bool> PauseRequested;
        public static event Action<TeleportInformation> PlayerTeleported;
        public static event Action<TeleportInformation> ObjectTeleported;
        public static event Action<GameObject> SitRequested;

        // ---- internal: FadeMask reports when the world is black from loading /
        // teleporting (a menu-caused black is NOT included — menus need a cursor) ----
        public static event Action<bool> ScreenFadeChanged;

        public static void RaiseHmdState(bool mounted) => HmdStateChanged?.Invoke(mounted);
        public static void RaiseHmdConnection(bool connected) => HmdConnectionChanged?.Invoke(connected);
        public static void RaiseXrIssue(string message) => XrIssueReported?.Invoke(message);
        public static void RaisePlayerMoving(bool moving) => PlayerMovingChanged?.Invoke(moving);
        public static void RaiseSeated(bool seated) => SeatedChanged?.Invoke(seated);
        public static void RaiseFallRecovered(string message) => FallRecovered?.Invoke(message);
        public static void RaiseMapToggle() => MapTogglePressed?.Invoke();
        public static void RaiseInventoryToggle() => InventoryTogglePressed?.Invoke();
        public static void RaiseCameraReset() => CameraResetRequested?.Invoke();
        public static void RaiseMouselookState(bool canLook) => MouselookStateChanged?.Invoke(canLook);
        public static void RaiseMenuState(bool menuOpen) => MenuStateChanged?.Invoke(menuOpen);
        public static void RaiseSceneLoading(bool loading) => SceneLoadingChanged?.Invoke(loading);
        public static void RaisePause(bool paused) => PauseRequested?.Invoke(paused);
        public static void RaisePlayerTeleported(TeleportInformation info) => PlayerTeleported?.Invoke(info);
        public static void RaiseObjectTeleported(TeleportInformation info) => ObjectTeleported?.Invoke(info);
        public static void RaiseScreenFade(bool faded) => ScreenFadeChanged?.Invoke(faded);
        public static void RaiseSitRequest(GameObject seatObject) => SitRequested?.Invoke(seatObject);
    }
}
