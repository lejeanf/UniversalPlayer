using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Regression tests for the OpenXR "DPad" layout bug: the OpenXR plugin
    /// registers a DEVICE layout named "DPad" which overrides the built-in
    /// "Dpad" CONTROL layout (names are case-insensitive), after which no
    /// gamepad can be created — turning a controller on mid-game fails.
    /// DpadLayoutGuard must detect the override and restore the built-in layout.
    /// </summary>
    public class DpadLayoutGuardTests : InputTestFixture
    {
        // Stand-in for OpenXR's D-Pad interaction profile device.
        [InputControlLayout(displayName = "Fake OpenXR DPad")]
        private class FakeOpenXrDpadDevice : InputDevice { }

        public override void Setup()
        {
            base.Setup();
            // The "repaired" warning is one-shot per domain; reset it so each
            // test can assert it independently.
            typeof(DpadLayoutGuard)
                .GetField("_warnedOnce", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, false);
            typeof(DpadLayoutGuard)
                .GetField("_repairScheduled", BindingFlags.NonPublic | BindingFlags.Static)
                ?.SetValue(null, false);
        }

        [Test]
        public void HealthyLayout_RepairIsANoOp()
        {
            Assert.That(DpadLayoutGuard.RepairIfNeeded(), Is.False,
                "RepairIfNeeded must not touch a healthy built-in Dpad layout.");
            Assert.DoesNotThrow(() => InputSystem.AddDevice<Gamepad>());
        }

        [Test]
        public void CorruptedDpadLayout_BreaksGamepads_AndRepairRestoresThem()
        {
            // Reproduce the OpenXR bug: a device layout takes over "Dpad".
            InputSystem.RegisterLayout<FakeOpenXrDpadDevice>("DPad");
            Assert.That(InputSystem.LoadLayout("Dpad").isDeviceLayout, Is.True,
                "Test setup failed: the override did not replace the built-in control layout.");
            Assert.Catch(() => InputSystem.AddDevice<Gamepad>(),
                "With the override in place, gamepad creation should fail — the bug this guards against.");

            LogAssert.Expect(LogType.Warning, new Regex("restored automatically"));
            Assert.That(DpadLayoutGuard.RepairIfNeeded(), Is.True, "The corrupted layout must be repaired.");

            Assert.That(InputSystem.LoadLayout("Dpad").isDeviceLayout, Is.False,
                "After repair, 'Dpad' must be a control layout again.");
            var gamepad = InputSystem.AddDevice<Gamepad>();
            Assert.That(gamepad.dpad, Is.Not.Null, "The repaired gamepad must have a working dpad control.");
        }

        [Test]
        public void Watcher_DefersTheRepair_ToAfterTheCurrentInputUpdate()
        {
            // Install() subscribes the onLayoutChange watcher (normally done by
            // RuntimeInitializeOnLoadMethod, which does not run inside the
            // isolated InputTestFixture input system).
            typeof(DpadLayoutGuard)
                .GetMethod("Install", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, null);

            InputSystem.RegisterLayout<FakeOpenXrDpadDevice>("DPad");

            // REGRESSION (editor freeze): the watcher must NOT call RegisterLayout
            // from inside onLayoutChange — that callback runs in the middle of
            // InputManager's registration (OpenXR registers the bad layout during
            // XR loader init) and re-entrant registration hangs the editor.
            Assert.That(InputSystem.LoadLayout("Dpad").isDeviceLayout, Is.True,
                "The watcher must not repair re-entrantly inside the layout-change callback.");

            LogAssert.Expect(LogType.Warning, new Regex("restored automatically"));
            InputSystem.Update(); // the deferred repair runs after the next input update

            Assert.That(InputSystem.LoadLayout("Dpad").isDeviceLayout, Is.False,
                "The scheduled repair must restore the control layout on the next input update.");
            Assert.DoesNotThrow(() => InputSystem.AddDevice<Gamepad>(),
                "A gamepad turned on after the (auto-repaired) override must be created normally.");
        }
    }
}
