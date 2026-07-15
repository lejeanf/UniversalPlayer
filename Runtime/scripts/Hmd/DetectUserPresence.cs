using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
// This file talks to both XR device APIs; default the bare names to the legacy ones it already used.
using InputDevice = UnityEngine.XR.InputDevice;
using CommonUsages = UnityEngine.XR.CommonUsages;

/// <summary>
/// Reads the head-mounted display's presence/tracking state.
///
/// <see cref="IsHMDMounted"/> answers the plain "is someone wearing it" question
/// via the <c>userPresence</c> proximity feature. <see cref="ReadState"/> exposes
/// the raw pieces (device validity, whether the runtime even supports the presence
/// feature, and the tracking flag) so callers can fall back to "HMD valid and
/// tracking" when a runtime does not report <c>userPresence</c> — many OpenXR
/// runtimes over Link/Air Link do not.
///
/// <see cref="ReadDiagnostics"/> additionally reads the Input System XR layer
/// (<c>XRHMD.current</c>), which sometimes reports <c>userPresence</c> when the
/// legacy <see cref="InputDevices"/> path does not. It exists so the on-screen HUD
/// can show BOTH sources side by side and we can learn which one to trust on a given
/// runtime before wiring it into arbitration — it is intentionally NOT used to decide
/// the mode yet.
/// </summary>
public class DetectUserPresence : MonoBehaviour
{
    private static InputDevice headDevice;

    /// <summary>Snapshot of the head device state used to decide whether we are in VR.</summary>
    public readonly struct HmdState
    {
        public readonly bool HmdValid;
        /// <summary>True when the runtime reports the <c>userPresence</c> feature at all.</summary>
        public readonly bool PresenceSupported;
        /// <summary>Meaningful only when <see cref="PresenceSupported"/> is true.</summary>
        public readonly bool Present;
        public readonly bool Tracked;

        public HmdState(bool hmdValid, bool presenceSupported, bool present, bool tracked)
        {
            HmdValid = hmdValid;
            PresenceSupported = presenceSupported;
            Present = present;
            Tracked = tracked;
        }
    }

    public DetectUserPresence()
    {
        if (headDevice == null)
        {
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        }
    }

    private static InputDevice ResolveHead()
    {
        if (headDevice == null || headDevice.isValid == false)
        {
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        }
        return headDevice;
    }

    /// <summary>Reads the full head-device state in one pass.</summary>
    public static HmdState ReadState()
    {
        var head = ResolveHead();
        if (!head.isValid)
        {
            return new HmdState(false, false, false, false);
        }

        var presenceSupported = head.TryGetFeatureValue(CommonUsages.userPresence, out var present);
        head.TryGetFeatureValue(CommonUsages.isTracked, out var tracked);
        return new HmdState(true, presenceSupported, presenceSupported && present, tracked);
    }

    public static bool IsHMDMounted()
    {
        var state = ReadState();
        return state.PresenceSupported && state.Present;
    }

    /// <summary>Both presence sources at once, for the diagnostic HUD.</summary>
    public readonly struct HmdDiagnostics
    {
        // Legacy UnityEngine.XR.InputDevices path (what arbitration currently uses).
        public readonly bool LegacyValid;
        public readonly bool LegacyPresenceSupported;
        public readonly bool LegacyPresent;
        public readonly bool LegacyTracked;
        // Input System XR layer (XRHMD.current) path.
        public readonly bool InputSystemConnected;
        public readonly bool InputSystemPresenceSupported;
        public readonly bool InputSystemPresent;
        public readonly bool InputSystemTracked;

        public HmdDiagnostics(bool legacyValid, bool legacyPresenceSupported, bool legacyPresent, bool legacyTracked,
            bool inputSystemConnected, bool inputSystemPresenceSupported, bool inputSystemPresent, bool inputSystemTracked)
        {
            LegacyValid = legacyValid;
            LegacyPresenceSupported = legacyPresenceSupported;
            LegacyPresent = legacyPresent;
            LegacyTracked = legacyTracked;
            InputSystemConnected = inputSystemConnected;
            InputSystemPresenceSupported = inputSystemPresenceSupported;
            InputSystemPresent = inputSystemPresent;
            InputSystemTracked = inputSystemTracked;
        }
    }

    /// <summary>Reads both the legacy and the Input System presence paths for side-by-side diagnosis. Never used to decide the mode.</summary>
    public static HmdDiagnostics ReadDiagnostics()
    {
        var legacy = ReadState();

        UnityEngine.InputSystem.XR.XRHMD hmd = null;
        foreach (var device in InputSystem.devices)
        {
            if (device is UnityEngine.InputSystem.XR.XRHMD candidate) { hmd = candidate; break; }
        }
        var connected = hmd != null;
        // TryGetChildControl returns null when the control is absent — so a runtime that
        // does not expose userPresence reports "unsupported" rather than throwing.
        var presence = hmd != null ? hmd.TryGetChildControl<ButtonControl>("userPresence") : null;
        var tracked = hmd != null ? hmd.TryGetChildControl<ButtonControl>("isTracked") : null;

        return new HmdDiagnostics(
            legacy.HmdValid, legacy.PresenceSupported, legacy.Present, legacy.Tracked,
            connected,
            presence != null,
            presence != null && presence.isPressed,
            tracked != null && tracked.isPressed);
    }
}
