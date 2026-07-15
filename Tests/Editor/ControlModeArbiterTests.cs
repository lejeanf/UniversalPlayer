using NUnit.Framework;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Exhaustive, fast EditMode coverage of the pure control-mode decision logic
    /// (<see cref="ControlModeArbiter"/>). This is where the flag interactions that used
    /// to break silently on a polled MonoBehaviour are pinned down — no headset, no
    /// PlayerInput, no frame timing.
    /// </summary>
    public class ControlModeArbiterTests
    {
        private const bool NotFreecam = false;
        private const bool HmdValid = true;
        private const bool NoVrRequest = false;
        private static ControlModeArbiter.DesktopInput None => ControlModeArbiter.DesktopInput.None;
        private static ControlModeArbiter.DesktopInput Kbm => ControlModeArbiter.DesktopInput.KeyboardMouse;
        private static ControlModeArbiter.DesktopInput Pad => ControlModeArbiter.DesktopInput.Gamepad;

        private static ControlModeArbiter New(int stablePolls = 2) => new ControlModeArbiter(stablePolls);

        // ---- launch ----------------------------------------------------------------

        [Test]
        public void SeedLaunch_Worn_StartsInVr()
        {
            var a = New();
            a.SeedLaunch(inVr: true);
            Assert.That(a.InVr, Is.True);
        }

        [Test]
        public void SeedLaunch_Absent_StartsDesktop()
        {
            var a = New();
            a.SeedLaunch(inVr: false);
            Assert.That(a.InVr, Is.False);
        }

        // ---- debounced presence rising edge ---------------------------------------

        [Test]
        public void WornEdge_EntersVr_OnlyAfterStablePolls()
        {
            var a = New(stablePolls: 2);
            a.SeedLaunch(false);

            a.NotifyWornPoll(true); // 1 of 2 — not yet stable
            var d1 = a.Decide(NotFreecam, HmdValid, NoVrRequest, None, None);
            Assert.That(a.InVr, Is.False, "One worn poll must not enter VR with a 2-poll debounce.");
            Assert.That(d1.ChangeScheme && d1.Scheme == ControlModeArbiter.Scheme.XR, Is.False);

            a.NotifyWornPoll(true); // 2 of 2 — stable → rising edge
            var d2 = a.Decide(NotFreecam, HmdValid, NoVrRequest, None, None);
            Assert.That(a.InVr, Is.True);
            Assert.That(d2.Scheme, Is.EqualTo(ControlModeArbiter.Scheme.XR));
        }

        [Test]
        public void SingleWornFlicker_DoesNotEnterVr()
        {
            var a = New(stablePolls: 2);
            a.SeedLaunch(false);

            a.NotifyWornPoll(true);  // 1
            a.NotifyWornPoll(false); // reset
            a.NotifyWornPoll(true);  // 1 again — never two in a row
            a.Decide(NotFreecam, HmdValid, NoVrRequest, None, None);

            Assert.That(a.InVr, Is.False, "A single-poll flicker must never arm the VR-entry edge.");
        }

        // ---- the S1 regression: a continuously-worn signal must not block exit -----

        [Test]
        public void Worn_ContinuousSignal_DoesNotReArmEdge_AndExitStillWorks()
        {
            var a = New(stablePolls: 2);
            a.SeedLaunch(inVr: true); // launched worn, already stable

            // Headset stays on: more worn polls must NOT manufacture a fresh edge that
            // re-enters every frame and blocks the exit path.
            a.NotifyWornPoll(true);
            a.NotifyWornPoll(true);

            var d = a.Decide(NotFreecam, HmdValid, NoVrRequest, deliberateExit: Kbm, desktopInput: None);
            Assert.That(a.InVr, Is.False, "Deliberate desktop input must exit VR even while the headset reads worn.");
            Assert.That(d.Scheme, Is.EqualTo(ControlModeArbiter.Scheme.KeyboardMouse));
        }

        // ---- leaving VR ------------------------------------------------------------

        [Test]
        public void InVr_DeliberateKeyboard_ExitsToKbm()
        {
            var a = New();
            a.SeedLaunch(true);
            var d = a.Decide(NotFreecam, HmdValid, NoVrRequest, deliberateExit: Kbm, desktopInput: None);
            Assert.That(a.InVr, Is.False);
            Assert.That(d.Scheme, Is.EqualTo(ControlModeArbiter.Scheme.KeyboardMouse));
        }

        [Test]
        public void InVr_DeliberateGamepad_ExitsToGamepad()
        {
            var a = New();
            a.SeedLaunch(true);
            var d = a.Decide(NotFreecam, HmdValid, NoVrRequest, deliberateExit: Pad, desktopInput: None);
            Assert.That(a.InVr, Is.False);
            Assert.That(d.Scheme, Is.EqualTo(ControlModeArbiter.Scheme.Gamepad));
        }

        [Test]
        public void InVr_NoDeliberateInput_StaysVr()
        {
            var a = New();
            a.SeedLaunch(true);
            var d = a.Decide(NotFreecam, HmdValid, NoVrRequest, deliberateExit: None, desktopInput: Kbm);
            Assert.That(a.InVr, Is.True, "Non-deliberate desktop input must not drop out of VR.");
            Assert.That(d.Scheme, Is.EqualTo(ControlModeArbiter.Scheme.XR));
        }

        // ---- the Link regime: presence never reported → worn always false ----------

        [Test]
        public void StuckTrackedRegime_NeverAutoEntersVr()
        {
            // Over Link with userPresence unsupported the caller passes worn=false forever
            // (tracked is not a runtime worn signal). Starting on desktop, VR must never
            // auto-engage no matter how long the headset streams.
            var a = New();
            a.SeedLaunch(false);
            for (var i = 0; i < 20; i++) a.NotifyWornPoll(false);
            var d = a.Decide(NotFreecam, HmdValid, NoVrRequest, None, None);
            Assert.That(a.InVr, Is.False);
            Assert.That(d.ChangeScheme, Is.False, "Idle desktop keeps the current scheme.");
        }

        [Test]
        public void AfterExit_WornStaysFalse_DoesNotReEnter_ThenCtrlAltV_ReEnters()
        {
            var a = New();
            a.SeedLaunch(true);
            a.Decide(NotFreecam, HmdValid, NoVrRequest, deliberateExit: Kbm, desktopInput: None); // exit
            Assert.That(a.InVr, Is.False);

            for (var i = 0; i < 10; i++) a.NotifyWornPoll(false); // Link: presence never true
            a.Decide(NotFreecam, HmdValid, NoVrRequest, None, None);
            Assert.That(a.InVr, Is.False, "Stuck-tracked (worn=false) must not yank the player back into VR.");

            var d = a.Decide(NotFreecam, HmdValid, vrEntryRequested: true, None, None);
            Assert.That(a.InVr, Is.True, "Ctrl+Alt+V must re-enter VR when a headset is present.");
            Assert.That(d.Scheme, Is.EqualTo(ControlModeArbiter.Scheme.XR));
        }

        [Test]
        public void CtrlAltV_Ignored_WhenNoHmdPresent()
        {
            var a = New();
            a.SeedLaunch(false);
            var d = a.Decide(NotFreecam, hmdValid: false, vrEntryRequested: true, None, None);
            Assert.That(a.InVr, Is.False, "A VR request with no headset connected must not strand _inVr true.");
            Assert.That(d.ChangeScheme, Is.False);
        }

        // ---- presence-supporting runtime: re-don re-enters (no sticky intent) -------

        [Test]
        public void PresenceEdge_ReEntersVr_AfterDesktopExit()
        {
            var a = New(stablePolls: 2);
            a.SeedLaunch(true);
            a.Decide(NotFreecam, HmdValid, NoVrRequest, deliberateExit: Kbm, desktopInput: None); // exit to desktop
            Assert.That(a.InVr, Is.False);

            a.NotifyWornPoll(false); // headset off the desk sensor
            a.NotifyWornPoll(true);  // put back on...
            a.NotifyWornPoll(true);  // ...stable
            var d = a.Decide(NotFreecam, HmdValid, NoVrRequest, None, None);
            Assert.That(a.InVr, Is.True, "Putting the headset back on (real presence edge) must return to VR.");
            Assert.That(d.Scheme, Is.EqualTo(ControlModeArbiter.Scheme.XR));
        }

        // ---- freecam ---------------------------------------------------------------

        [Test]
        public void Freecam_SuspendsArbitration_AndDropsPendingEdge()
        {
            var a = New(stablePolls: 2);
            a.SeedLaunch(false);
            a.NotifyWornPoll(true);
            a.NotifyWornPoll(true); // edge armed

            var dFree = a.Decide(isFreecam: true, HmdValid, NoVrRequest, None, None);
            Assert.That(dFree.ChangeScheme, Is.False, "Freecam suspends arbitration.");

            var dAfter = a.Decide(NotFreecam, HmdValid, NoVrRequest, None, None);
            Assert.That(a.InVr, Is.False, "An entry edge that arrived during freecam must not fire stale on exit.");
        }

        // ---- desktop arbitration ---------------------------------------------------

        [Test]
        public void Desktop_FollowsMostRecentInput()
        {
            var a = New();
            a.SeedLaunch(false);
            Assert.That(a.Decide(NotFreecam, HmdValid, NoVrRequest, None, Pad).Scheme,
                Is.EqualTo(ControlModeArbiter.Scheme.Gamepad));
            Assert.That(a.Decide(NotFreecam, HmdValid, NoVrRequest, None, Kbm).Scheme,
                Is.EqualTo(ControlModeArbiter.Scheme.KeyboardMouse));
            Assert.That(a.Decide(NotFreecam, HmdValid, NoVrRequest, None, None).ChangeScheme,
                Is.False, "Idle keeps the current scheme.");
        }

        // ---- failsafe --------------------------------------------------------------

        [Test]
        public void ForceExitVr_LeavesVr()
        {
            var a = New();
            a.SeedLaunch(true);
            a.ForceExitVr();
            Assert.That(a.InVr, Is.False);
            var d = a.Decide(NotFreecam, HmdValid, NoVrRequest, None, None);
            Assert.That(d.ChangeScheme, Is.False, "After a forced exit the caller has already switched; idle keeps desktop.");
        }
    }
}
