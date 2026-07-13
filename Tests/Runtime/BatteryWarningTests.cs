using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Battery warning system: low XR batteries show the VR panel, a low gamepad
    /// battery shows the overlay, critical levels raise the pause event once, and the
    /// failsafe eventually hands control back to the desktop. Probes are faked — no
    /// hardware involved.
    /// </summary>
    public class BatteryWarningTests
    {
        private GameObject _root;
        private BatteryWarningSystem _system;
        private List<(string, float)> _fakeXr;
        private BatteryWarningSystem.GamepadBatteryState _fakeGamepad;
        private readonly List<bool> _pauseEvents = new List<bool>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;
            _fakeXr = new List<(string, float)>();
            _fakeGamepad = BatteryWarningSystem.GamepadBatteryState.Ok;
            _pauseEvents.Clear();
            PlayerEvents.PauseRequested += RecordPause;

            _root = new GameObject("BatteryRig");
            _root.SetActive(false);
            _system = _root.AddComponent<BatteryWarningSystem>();
            SetField(_system, "pollIntervalSeconds", 0.05f);
            SetField(_system, "failsafeSwitchSeconds", 0.4f);
            _root.SetActive(true);
            _system.XrBatteryProbe = () => _fakeXr;
            _system.GamepadBatteryProbe = () => _fakeGamepad;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerEvents.PauseRequested -= RecordPause;
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            Object.Destroy(_root);
            var stray = GameObject.Find("BatteryWarning_VR");
            if (stray != null) Object.Destroy(stray);
            yield return null;
        }

        private void RecordPause(bool paused) => _pauseEvents.Add(paused);

        private static void SetField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"Field '{field}' not found on {target.GetType().Name} — update BatteryWarningTests alongside the refactor.");
            info.SetValue(target, value);
        }

        private static Text FindWarningText(string panelName)
        {
            var panel = GameObject.Find(panelName);
            return panel == null ? null : panel.GetComponentInChildren<Text>(true);
        }

        [UnityTest]
        public IEnumerator LowHeadsetBattery_ShowsTheVrPanel_NoPause()
        {
            var camera = new GameObject("MainCamera") { tag = "MainCamera" };
            camera.AddComponent<Camera>();
            try
            {
                _fakeXr.Add(("Headset", 0.15f));
                yield return new WaitForSeconds(0.2f);

                var text = FindWarningText("BatteryWarning_VR");
                Assert.That(text, Is.Not.Null, "Low headset battery did not build/show the VR warning panel.");
                Assert.That(text.text, Does.Contain("Headset battery 15%"),
                    "The VR warning does not name the device and level.");
                Assert.That(_pauseEvents, Is.Empty, "A merely-low battery must not pause the game.");
            }
            finally
            {
                Object.Destroy(camera);
            }
        }

        [UnityTest]
        public IEnumerator CriticalBattery_PausesOnce_AndFailsafeCountsDown()
        {
            var camera = new GameObject("MainCamera") { tag = "MainCamera" };
            camera.AddComponent<Camera>();
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("CRITICAL battery"));
                _fakeXr.Add(("Left controller", 0.05f));
                yield return new WaitForSeconds(0.2f);

                Assert.That(_pauseEvents, Is.EqualTo(new[] { true }),
                    "Critical battery must raise PauseRequested(true) exactly once.");
                var text = FindWarningText("BatteryWarning_VR");
                Assert.That(text.text, Does.Contain("Switching to keyboard in"),
                    "The failsafe countdown is not shown while the battery stays critical.");

                // The failsafe fires after failsafeSwitchSeconds (0.4s here). No
                // BroadcastControlsStatus exists in this rig, so it must complain loudly
                // instead of silently doing nothing.
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("failsafe elapsed"));
                yield return new WaitForSeconds(0.5f);
            }
            finally
            {
                Object.Destroy(camera);
            }
        }

        [UnityTest]
        public IEnumerator GamepadLowBattery_ShowsOverlay_OnlyInGamepadScheme()
        {
            _fakeGamepad = BatteryWarningSystem.GamepadBatteryState.Low;

            // In XR scheme, the gamepad state must be ignored entirely.
            yield return new WaitForSeconds(0.15f);
            Assert.That(GameObject.Find("BatteryWarning_Overlay"), Is.Null,
                "The gamepad overlay appeared while the scheme was XR.");

            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.Gamepad;
            yield return new WaitForSeconds(0.15f);

            var text = FindWarningText("BatteryWarning_Overlay");
            Assert.That(text, Is.Not.Null, "Low gamepad battery did not show the screen-space overlay in Gamepad scheme.");
            Assert.That(text.text, Does.Contain("Controller battery low"));
            Assert.That(_pauseEvents, Is.Empty, "A merely-low gamepad battery must not pause the game.");
        }
    }
}
