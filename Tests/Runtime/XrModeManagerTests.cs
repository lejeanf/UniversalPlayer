using System.Collections;
using System.Reflection;
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
        private XrModeManager _manager;
        private BroadcastControlsStatus.ControlScheme _originalScheme;

        [SetUp]
        public void SetUp()
        {
            _originalScheme = BroadcastControlsStatus.controlScheme;
            XrDisplayLifecycle.ResetRequestHistory();

            _cameraGo = new GameObject("XrModeManagerTestCamera");
            _camera = _cameraGo.AddComponent<Camera>();
            _camera.enabled = false;

            // Created inactive so the camera override is assigned before OnEnable runs.
            _managerGo = new GameObject("XrModeManagerTest");
            _managerGo.SetActive(false);
            _manager = _managerGo.AddComponent<XrModeManager>();
            _manager.playerCameraOverride = _camera;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} no longer exists — update this test with the rename.");
            field.SetValue(target, value);
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

        [UnityTest]
        public IEnumerator KeepRunningPolicy_NeverStartsOrStopsTheDisplayOnAModeEdge()
        {
            // The v1.16.6 contract the maintainer validated on Unity 6000.6: with the
            // keep-running policy a VR entry or exit is a plain camera flip. The only
            // display start the package ever issues is the keeper warming a present but
            // idle display on desktop — and there is no display in the test runner.
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            SetPrivateField(_manager, "reconcileIntervalSeconds", 0f);
            _managerGo.SetActive(true);

            BroadcastControlsStatus.SendControlScheme?.Invoke(BroadcastControlsStatus.ControlScheme.XR);
            yield return null;
            yield return null;
            BroadcastControlsStatus.SendControlScheme?.Invoke(BroadcastControlsStatus.ControlScheme.KeyboardMouse);
            yield return null;
            yield return null;

            Assert.That(XrDisplayLifecycle.StartRequests, Is.EqualTo(0),
                "With Stop Xr Display On Desktop = Never, entering VR must not start the display: the XR loader already runs it and a restart from the package destabilised the switch.");
            Assert.That(XrDisplayLifecycle.StopRequests, Is.EqualTo(0),
                "With Stop Xr Display On Desktop = Never the display must never be stopped.");
        }

        [Test]
        public void StopPolicy_StopsTheDisplayOnDesktop_StartsItOnVr()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            SetPrivateField(_manager, "stopXrDisplayOnDesktop", XrModeManager.DisplayStopMode.Always);
            _managerGo.SetActive(true);
            Assert.That(XrDisplayLifecycle.StartRequests, Is.EqualTo(0),
                "A desktop start must never start the display under the stop policy.");
            // No display subsystem exists in the test runner, so nothing is stopped here either.

            var startsBeforeEntry = XrDisplayLifecycle.StartRequests;
            BroadcastControlsStatus.SendControlScheme?.Invoke(BroadcastControlsStatus.ControlScheme.XR);
            Assert.That(XrDisplayLifecycle.StopRequests, Is.EqualTo(0),
                "VR entry must never stop the display.");

            BroadcastControlsStatus.SendControlScheme?.Invoke(BroadcastControlsStatus.ControlScheme.KeyboardMouse);
            Assert.That(XrDisplayLifecycle.StartRequests, Is.EqualTo(startsBeforeEntry),
                "Leaving VR must never start the display.");
        }

        [Test]
        public void DesktopEdge_ResetsTheCameraRect()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            _managerGo.SetActive(true);
            BroadcastControlsStatus.SendControlScheme?.Invoke(BroadcastControlsStatus.ControlScheme.XR);
            _camera.rect = new Rect(0f, 0f, 0.5f, 1f);

            BroadcastControlsStatus.SendControlScheme?.Invoke(BroadcastControlsStatus.ControlScheme.KeyboardMouse);
            Assert.That(_camera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
                "Leaving VR must hand the whole window back to the flat camera.");
        }

        [Test]
        public void PlayerCamera_IsResolvedInsideTheRig_NeverSceneWide()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;

            var decoyRoot = new GameObject("DecoyPlayer");
            decoyRoot.SetActive(false);
            var decoyCamera = new GameObject("DecoyCamera").AddComponent<Camera>();
            decoyCamera.enabled = false;
            decoyRoot.AddComponent<FPSCameraMovement>().playerCamera = decoyCamera;
            CameraXrRendering.Set(decoyCamera, true);

            var rig = new GameObject("Rig");
            rig.SetActive(false);
            var rigCamera = new GameObject("RigCamera").AddComponent<Camera>();
            rigCamera.enabled = false;
            rigCamera.transform.SetParent(rig.transform);
            var locomotion = new GameObject("Locomotion");
            locomotion.SetActive(false);
            locomotion.transform.SetParent(rig.transform);
            locomotion.AddComponent<FPSCameraMovement>().playerCamera = rigCamera;
            var xrSettings = new GameObject("XR");
            xrSettings.transform.SetParent(rig.transform);
            xrSettings.AddComponent<XrModeManager>();
            CameraXrRendering.Set(rigCamera, true);

            try
            {
                rig.SetActive(true);
                Assert.That(CameraXrRendering.IsXrEnabled(rigCamera), Is.False,
                    "XrModeManager under Settings/XR must still find the camera of ITS rig (FPSCameraMovement lives under Settings/Locomotion).");
                Assert.That(CameraXrRendering.IsXrEnabled(decoyCamera), Is.True,
                    "A second player's camera elsewhere in the scene must never be touched (the scene-wide FindAnyObjectByType regression).");
            }
            finally
            {
                Object.DestroyImmediate(rig);
                Object.DestroyImmediate(decoyRoot);
                Object.DestroyImmediate(decoyCamera.gameObject);
            }
        }
    }
}
