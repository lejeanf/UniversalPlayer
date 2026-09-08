using NUnit.Framework;

namespace jeanf.universalplayer.tests.editor
{
    public class XrHealthMonitorTests
    {
        private const float Late = 6f;
        private const float Grace = 2f;

        private static XrHealthMonitor.SessionStartVerdict Assess(bool initOnStart, bool loader, bool display, bool focused, float seconds) =>
            XrHealthMonitor.AssessSessionStart(initOnStart, loader, display, focused, seconds, Late, Grace);

        [Test]
        public void SessionStart_FocusedWithinThreshold_IsHealthy()
        {
            Assert.That(Assess(true, true, true, true, 3f), Is.EqualTo(XrHealthMonitor.SessionStartVerdict.Healthy),
                "A session focused a few seconds after start is the normal Link handshake.");
        }

        [Test]
        public void SessionStart_FocusedAfterThreshold_IsLate()
        {
            Assert.That(Assess(true, true, true, true, 11.7f), Is.EqualTo(XrHealthMonitor.SessionStartVerdict.LateStart),
                "A session that waited for the headset to wake up must be flagged — Quest Link then often keeps the headset black.");
        }

        [Test]
        public void SessionStart_StillWaitingForFocus_IsPending()
        {
            Assert.That(Assess(true, true, false, false, 11.7f), Is.EqualTo(XrHealthMonitor.SessionStartVerdict.Pending),
                "No verdict while the display is not running yet — the wait is only judged once the session is focused.");
            Assert.That(Assess(true, true, true, false, 3f), Is.EqualTo(XrHealthMonitor.SessionStartVerdict.Pending));
        }

        [Test]
        public void SessionStart_NoLoaderAfterGrace_IsInitFailed_OnlyWhenXrInitializesOnStartup()
        {
            Assert.That(Assess(true, false, false, false, 1f), Is.EqualTo(XrHealthMonitor.SessionStartVerdict.Pending),
                "Within the grace period the loader may still be initializing.");
            Assert.That(Assess(true, false, false, false, 3f), Is.EqualTo(XrHealthMonitor.SessionStartVerdict.InitFailed),
                "XR initializes on startup, so no loader after the grace period means the runtime refused (form factor unavailable).");
            Assert.That(Assess(false, false, false, false, 3f), Is.EqualTo(XrHealthMonitor.SessionStartVerdict.Pending),
                "Without init-on-startup a missing loader is not a failure — the project may start XR later.");
        }

        [Test]
        public void SessionStart_NeverResolving_GivesUpSilently()
        {
            var afterGiveUp = XrHealthMonitor.SessionStartGiveUpSeconds + 1f;
            Assert.That(Assess(false, false, false, false, afterGiveUp), Is.EqualTo(XrHealthMonitor.SessionStartVerdict.Unwatched));
            Assert.That(Assess(true, true, false, false, afterGiveUp), Is.EqualTo(XrHealthMonitor.SessionStartVerdict.Unwatched),
                "A desktop-only play session that never wears the headset must not warn.");
        }

        [Test]
        public void SessionStart_Messages_TellHowToRecover()
        {
            Assert.That(XrHealthMonitor.LateStartMessage(11.7f), Does.Contain("11.7 s").And.Contain("Quest Link"));
            Assert.That(XrHealthMonitor.InitFailedMessage(), Does.Contain("form factor").And.Contain("Quest Link"));
        }
    }
}
