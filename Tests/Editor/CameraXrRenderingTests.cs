using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
#if UNIVERSALPLAYER_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif
#if UNIVERSALPLAYER_URP
using UnityEngine.Rendering.Universal;
#endif

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// The per-camera XR switch behind XrModeManager's flat-desktop-view behaviour.
    /// The pipeline-specific assertions run against whichever pipeline the executing
    /// project has active (HDRP in uvs-package-creator, URP in consumers), so the same
    /// test file validates both integrations.
    /// </summary>
    public class CameraXrRenderingTests
    {
        private GameObject _cameraGo;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _cameraGo = new GameObject("CameraXrRenderingTestCamera");
            _camera = _cameraGo.AddComponent<Camera>();
            _camera.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cameraGo);
        }

        [Test]
        public void NullCamera_IsANoOp()
        {
            Assert.DoesNotThrow(() => CameraXrRendering.Set(null, false),
                "CameraXrRendering.Set must tolerate a missing camera (the manager may run before the rig is resolved).");
        }

        [Test]
        public void DisablingXr_ReadsBackDisabled_OnTheActivePipeline()
        {
            CameraXrRendering.Set(_camera, false);
            Assert.That(CameraXrRendering.IsXrEnabled(_camera), Is.False,
                "Desktop mode must disable XR rendering on the camera (flat view) on whichever pipeline is active.");
        }

        [Test]
        public void EnablingXr_ReadsBackEnabled_OnTheActivePipeline()
        {
            CameraXrRendering.Set(_camera, false);
            CameraXrRendering.Set(_camera, true);
            Assert.That(CameraXrRendering.IsXrEnabled(_camera), Is.True,
                "Re-entering VR must restore XR rendering on the camera.");
        }

        [Test]
        public void UnderAnSrp_StereoTargetEyeIsLeftAlone()
        {
            // Camera.stereoTargetEye is a built-in-renderer API; under an SRP the setter
            // is a no-op that logs "You can use Camera.stereoTargetEye only with the
            // built-in renderer" on EVERY call — CameraXrRendering must not touch it.
            if (GraphicsSettings.currentRenderPipeline == null)
                Assert.Ignore("Built-in pipeline active — stereoTargetEye IS the mechanism there.");

            CameraXrRendering.Set(_camera, false);
            Assert.That(_camera.stereoTargetEye, Is.EqualTo(StereoTargetEyeMask.Both),
                "CameraXrRendering wrote stereoTargetEye under an SRP — that setter only spams a console warning there.");
        }

        [Test]
        public void ActivePipeline_CameraDataFollowsTheSwitch()
        {
#if UNIVERSALPLAYER_HDRP
            if (GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset)
            {
                CameraXrRendering.Set(_camera, false);
                var hdData = _camera.GetComponent<HDAdditionalCameraData>();
                Assert.That(hdData, Is.Not.Null,
                    "Disabling XR under HDRP must ensure an HDAdditionalCameraData exists — without it the camera keeps rendering the VR mirror.");
                Assert.That(hdData.xrRendering, Is.False,
                    "HDRP gates per-camera XR on HDAdditionalCameraData.xrRendering; it must be off for the flat desktop view.");

                CameraXrRendering.Set(_camera, true);
                Assert.That(hdData.xrRendering, Is.True,
                    "Re-entering VR must re-enable HDAdditionalCameraData.xrRendering.");
                return;
            }
#endif
#if UNIVERSALPLAYER_URP
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
            {
                CameraXrRendering.Set(_camera, false);
                var urpData = _camera.GetComponent<UniversalAdditionalCameraData>();
                Assert.That(urpData, Is.Not.Null,
                    "Disabling XR under URP must ensure a UniversalAdditionalCameraData exists — without it the camera keeps rendering the VR mirror.");
                Assert.That(urpData.allowXRRendering, Is.False,
                    "URP gates per-camera XR on UniversalAdditionalCameraData.allowXRRendering; it must be off for the flat desktop view.");

                CameraXrRendering.Set(_camera, true);
                Assert.That(urpData.allowXRRendering, Is.True,
                    "Re-entering VR must re-enable UniversalAdditionalCameraData.allowXRRendering.");
                return;
            }
#endif
            Assert.Ignore("No SRP active in this project — the built-in path is covered by the stereoTargetEye tests.");
        }

        [Test]
        public void DisablingXr_ResetsExplicitProjectionOverrides()
        {
            // Stereo rendering leaves the camera holding the left eye's asymmetric
            // projection as an explicit override; unless it is reset, the flat desktop
            // view still LOOKS like the VR left-eye mirror ("stays on Left Eye after
            // VR"). Simulate the stranded state with an explicit override and verify
            // Set(false) returns projection control to fieldOfView/aspect.
            var stranded = Matrix4x4.Frustum(-0.9f, 0.7f, -0.8f, 0.8f, 0.05f, 100f);
            _camera.projectionMatrix = stranded;

            CameraXrRendering.Set(_camera, false);

            Assert.That(_camera.projectionMatrix, Is.Not.EqualTo(stranded),
                "Set(false) must ResetProjectionMatrix — an explicit (per-eye) projection override survived.");
            Assert.That(_camera.projectionMatrix.m02, Is.EqualTo(0f).Within(1e-5f),
                "The reset projection must be symmetric again (no off-center eye projection).");
        }

        [Test]
        public void EnablingXr_DoesNotAddPipelineCameraData()
        {
            // A camera without additional camera data already renders XR by default;
            // enabling must not spray components onto it.
            CameraXrRendering.Set(_camera, true);
#if UNIVERSALPLAYER_HDRP
            Assert.That(_camera.GetComponent<HDAdditionalCameraData>(), Is.Null,
                "Enabling XR on a bare camera must not add HDAdditionalCameraData — the implicit default already renders XR.");
#endif
#if UNIVERSALPLAYER_URP
            Assert.That(_camera.GetComponent<UniversalAdditionalCameraData>(), Is.Null,
                "Enabling XR on a bare camera must not add UniversalAdditionalCameraData — the implicit default already renders XR.");
#endif
        }
    }
}
