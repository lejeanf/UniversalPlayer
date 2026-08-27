using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// XrModeManager must flip the player camera between stereo (VR) and flat
    /// (desktop) rendering as the control scheme changes — this is what stops the
    /// Game view being stuck on the one-eye VR mirror after leaving VR.
    /// </summary>
    public class XrModeManagerTests
    {
        private GameObject _managerGo;
        private GameObject _cameraGo;
        private Camera _camera;
        private BroadcastControlsStatus.ControlScheme _originalScheme;

        [SetUp]
        public void SetUp()
        {
            _originalScheme = BroadcastControlsStatus.controlScheme;

            _cameraGo = new GameObject("XrModeManagerTestCamera");
            _camera = _cameraGo.AddComponent<Camera>();
            _camera.enabled = false;

            // Created inactive so the camera override is assigned before OnEnable runs.
            _managerGo = new GameObject("XrModeManagerTest");
            _managerGo.SetActive(false);
            var manager = _managerGo.AddComponent<XrModeManager>();
            manager.playerCameraOverride = _camera;
        }

        [TearDown]
        public void TearDown()
        {
            Object.Destroy(_managerGo);
            Object.Destroy(_cameraGo);
            BroadcastControlsStatus.controlScheme = _originalScheme;
        }

        [Test]
        public void EnablingOnDesktop_RendersFlat()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;

            _managerGo.SetActive(true);

            Assert.That(CameraXrRendering.IsXrEnabled(_camera), Is.False,
                "On a desktop scheme the manager must disable XR rendering on the player camera at enable time.");
        }

        [Test]
        public void SchemeBroadcasts_FollowVrInAndOut()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            _managerGo.SetActive(true);

            BroadcastControlsStatus.SendControlScheme?.Invoke(BroadcastControlsStatus.ControlScheme.XR);
            Assert.That(CameraXrRendering.IsXrEnabled(_camera), Is.True,
                "Entering VR must restore stereo rendering on the player camera.");

            BroadcastControlsStatus.SendControlScheme?.Invoke(BroadcastControlsStatus.ControlScheme.Gamepad);
            Assert.That(CameraXrRendering.IsXrEnabled(_camera), Is.False,
                "Leaving VR for gamepad must return the camera to the flat desktop view — " +
                "this is the 'Game view stuck on the left-eye mirror' regression.");
        }

        [Test]
        public void DesktopFov_SurvivesAVrRoundTrip()
        {
            // Stereo rendering overwrites Camera.fieldOfView with the HMD's (~100°);
            // leaving VR must put the desktop value back or the flat view renders with
            // the headset FOV ("FOV wrong after VR").
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            _managerGo.SetActive(true);
            _camera.fieldOfView = 72f; // a gameplay-tuned desktop FOV, deliberately not the 60 default

            BroadcastControlsStatus.SendControlScheme?.Invoke(BroadcastControlsStatus.ControlScheme.XR);
            _camera.fieldOfView = 100.2f; // what the XR layer does to the camera while stereo

            BroadcastControlsStatus.SendControlScheme?.Invoke(BroadcastControlsStatus.ControlScheme.KeyboardMouse);
            Assert.That(_camera.fieldOfView, Is.EqualTo(72f).Within(1e-3f),
                "Leaving VR must restore the desktop FOV captured on VR entry, not keep the headset's.");
        }

        [UnityTest]
        public IEnumerator SessionKeeper_NotCreatedWithoutAnXrDisplay()
        {
            // The keeper exists to feed the OpenXR session frames; with no display
            // subsystem (desktop-only session, tests) it must never appear — a stray
            // extra camera would double-render every frame for nothing.
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            _managerGo.SetActive(true);

            yield return null; // let Update's reconcile run at least once
            yield return null;

            Assert.That(_managerGo.GetComponentsInChildren<Camera>(true), Is.Empty,
                "XrModeManager created its session-keeper camera although no XRDisplaySubsystem exists.");
        }
    }
}
