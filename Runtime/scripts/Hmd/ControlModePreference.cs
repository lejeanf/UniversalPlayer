using System;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Remembers the player's last desktop control mode so a session can start in
    /// the mode they last used (Keyboard&amp;Mouse vs Gamepad) instead of always
    /// defaulting to Keyboard&amp;Mouse. VR is never persisted here — it is decided by
    /// headset presence at launch.
    ///
    /// Storage is local (<see cref="PlayerPrefs"/>) by default. A project that keeps
    /// the preference in a database can set <see cref="ExternalProvider"/> (read) and
    /// <see cref="ExternalSink"/> (write); the external source wins over local memory,
    /// and local memory remains the offline fallback.
    /// </summary>
    public static class ControlModePreference
    {
        private const string PrefKey = "jeanf.universalplayer.lastDesktopMode";

        /// <summary>Optional read override (e.g. a database). When it returns a value, that value wins over local memory.</summary>
        public static Func<BroadcastControlsStatus.ControlScheme?> ExternalProvider;

        /// <summary>Optional write sink (e.g. a database) called alongside the local save.</summary>
        public static Action<BroadcastControlsStatus.ControlScheme> ExternalSink;

        /// <summary>The remembered desktop mode, or null if none is stored. Only ever KeyboardMouse or Gamepad.</summary>
        public static BroadcastControlsStatus.ControlScheme? Load()
        {
            var external = ExternalProvider?.Invoke();
            if (external.HasValue && IsDesktop(external.Value)) return external.Value;

            if (!PlayerPrefs.HasKey(PrefKey)) return null;
            var raw = PlayerPrefs.GetInt(PrefKey, -1);
            if (!Enum.IsDefined(typeof(BroadcastControlsStatus.ControlScheme), raw)) return null;
            var scheme = (BroadcastControlsStatus.ControlScheme)raw;
            return IsDesktop(scheme) ? scheme : (BroadcastControlsStatus.ControlScheme?)null;
        }

        /// <summary>Persists a desktop mode. Non-desktop schemes (XR, FreeCam) are ignored so the last desktop choice survives VR/FreeCam sessions.</summary>
        public static void Save(BroadcastControlsStatus.ControlScheme scheme)
        {
            if (!IsDesktop(scheme)) return;
            PlayerPrefs.SetInt(PrefKey, (int)scheme);
            PlayerPrefs.Save();
            ExternalSink?.Invoke(scheme);
        }

        /// <summary>Forgets the locally stored desktop mode (used by tests and "reset to defaults").</summary>
        public static void Clear() => PlayerPrefs.DeleteKey(PrefKey);

        private static bool IsDesktop(BroadcastControlsStatus.ControlScheme scheme) =>
            scheme == BroadcastControlsStatus.ControlScheme.KeyboardMouse ||
            scheme == BroadcastControlsStatus.ControlScheme.Gamepad;
    }
}
