using System;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Pure, MonoBehaviour-free decision logic for the player's control mode
    /// (Keyboard&amp;Mouse / XR / Gamepad). Kept free of Unity device I/O so the whole
    /// truth table is exhaustively unit-testable in EditMode — the flag interactions
    /// here are exactly the kind that broke silently when this lived inside a
    /// MonoBehaviour polled on a timer.
    ///
    /// Model (Approach A — automatic, no prompt):
    ///  • <b>Enter VR</b> on a debounced presence rising edge (a real "headset just put
    ///    on" signal) or an explicit request (Ctrl+Alt+V). Never from a continuously
    ///    streaming signal — over Quest Link <c>isTracked</c> stays true off-head, so it
    ///    is NOT fed here as a runtime worn signal (only as a one-time launch hint,
    ///    which the caller resolves before <see cref="SeedLaunch"/>).
    ///  • <b>Leave VR</b> only on deliberate desktop input (the caller supplies a strict
    ///    predicate so a desk bump does not count). This is the robust exit path: it does
    ///    not depend on the presence sensor ever reading "not worn".
    ///  • <b>Desktop</b> follows the most recent desktop input; idle keeps the current mode.
    ///
    /// Freecam is owned by the caller (PlayerMovement) and suspends arbitration entirely.
    /// </summary>
    public sealed class ControlModeArbiter
    {
        public enum Scheme { KeyboardMouse, XR, Gamepad }
        public enum DesktopInput { None, KeyboardMouse, Gamepad }

        private readonly int _wornStablePolls;
        private int _wornTruePolls;
        private bool _prevWornStable;
        private bool _pendingWornEdge;
        private bool _inVr;

        /// <summary>True while the player is logically in VR (drives the XR scheme).</summary>
        public bool InVr => _inVr;

        /// <param name="wornStablePolls">
        /// How many consecutive "worn" presence polls are required before a rising edge
        /// fires, so a single proximity-sensor flicker cannot yank the player into VR.
        /// </param>
        public ControlModeArbiter(int wornStablePolls = 2)
        {
            _wornStablePolls = Math.Max(1, wornStablePolls);
        }

        /// <summary>
        /// Seeds the mode decided at launch (before any polling). Marks the worn signal
        /// as already-stable so a headset that is worn continuously from launch does not
        /// register a fresh rising edge on the first poll.
        /// </summary>
        public void SeedLaunch(bool inVr)
        {
            _inVr = inVr;
            _prevWornStable = inVr;
            _wornTruePolls = inVr ? _wornStablePolls : 0;
            _pendingWornEdge = false;
        }

        /// <summary>
        /// Feeds one presence poll. <paramref name="worn"/> is the RUNTIME worn signal —
        /// presence when the runtime supports it, otherwise false (tracked must never be
        /// passed here). A debounced false→true transition arms a one-shot VR-entry edge
        /// consumed by the next <see cref="Decide"/>.
        /// </summary>
        public void NotifyWornPoll(bool worn)
        {
            if (worn) _wornTruePolls++;
            else _wornTruePolls = 0;

            var stable = _wornTruePolls >= _wornStablePolls;
            if (stable && !_prevWornStable) _pendingWornEdge = true; // rising edge only
            _prevWornStable = stable;
        }

        public readonly struct Decision
        {
            public readonly bool ChangeScheme;
            public readonly Scheme Scheme;
            private Decision(bool change, Scheme scheme) { ChangeScheme = change; Scheme = scheme; }
            public static Decision Keep => new Decision(false, default);
            public static Decision To(Scheme scheme) => new Decision(true, scheme);
        }

        /// <summary>
        /// Decides the desired scheme for this frame.
        /// </summary>
        /// <param name="isFreecam">Caller is in the free-fly camera sub-mode; arbitration is suspended and a pending entry edge is dropped so it cannot fire stale on exit.</param>
        /// <param name="hmdValid">An XR HMD device is actually present — guards VR entry when no headset is connected.</param>
        /// <param name="vrEntryRequested">One-shot VR-entry request this frame (e.g. Ctrl+Alt+V).</param>
        /// <param name="deliberateExit">Strict "leave VR now" desktop input this frame (None when absent). Only consulted while in VR.</param>
        /// <param name="desktopInput">Most-recent desktop input for KBM/Gamepad arbitration while already on desktop.</param>
        public Decision Decide(bool isFreecam, bool hmdValid, bool vrEntryRequested,
                               DesktopInput deliberateExit, DesktopInput desktopInput)
        {
            if (isFreecam)
            {
                _pendingWornEdge = false; // freecam owns the mode; don't fire a stale edge on exit
                return Decision.Keep;
            }

            var wornEdge = _pendingWornEdge;
            _pendingWornEdge = false;

            // VR entry: a debounced presence edge or an explicit request, but only when a
            // real headset is present (otherwise a stray Ctrl+Alt+V would strand _inVr
            // true while the scheme could not actually switch to XR).
            if ((vrEntryRequested || wornEdge) && hmdValid)
            {
                _inVr = true;
                return Decision.To(Scheme.XR);
            }

            if (_inVr)
            {
                switch (deliberateExit)
                {
                    case DesktopInput.KeyboardMouse: _inVr = false; return Decision.To(Scheme.KeyboardMouse);
                    case DesktopInput.Gamepad:       _inVr = false; return Decision.To(Scheme.Gamepad);
                    default:                         return Decision.To(Scheme.XR); // sticky: nothing deliberate → stay
                }
            }

            switch (desktopInput)
            {
                case DesktopInput.KeyboardMouse: return Decision.To(Scheme.KeyboardMouse);
                case DesktopInput.Gamepad:       return Decision.To(Scheme.Gamepad);
                default:                         return Decision.Keep; // idle keeps the current scheme
            }
        }

        /// <summary>Failsafe (e.g. a dying headset battery): force out of VR now. The caller switches the scheme to desktop; a fresh presence edge or Ctrl+Alt+V is required to return.</summary>
        public void ForceExitVr() => _inVr = false;
    }
}
