using UnityEngine;
using UnityEngine.Rendering;
#if UNIVERSALPLAYER_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif
#if UNIVERSALPLAYER_URP
using UnityEngine.Rendering.Universal;
#endif

namespace jeanf.universalplayer
{
    /// <summary>
    /// Pipeline-agnostic per-camera XR switch: with XR rendering OFF the camera draws a
    /// normal flat view to the monitor/Game view even while the XRDisplaySubsystem keeps
    /// running (so re-entering VR never goes through the fragile display stop/start —
    /// see <see cref="XrModeManager"/>). Each render pipeline gates this differently:
    /// HDRP via <c>HDAdditionalCameraData.xrRendering</c>, URP via
    /// <c>UniversalAdditionalCameraData.allowXRRendering</c>, built-in via
    /// <c>Camera.stereoTargetEye</c>. The pipeline defines come from versionDefines on
    /// the asmdef, so the package compiles whether either, both, or neither pipeline
    /// package is installed; the active pipeline is checked at runtime because a project
    /// can have both packages present (uvs-package-creator is HDRP, uvs is URP).
    /// </summary>
    public static class CameraXrRendering
    {
        public static void Set(Camera camera, bool xrEnabled)
        {
            if (camera == null) return;

            // stereoTargetEye is a built-in-renderer API: under an SRP the setter is a
            // no-op that logs "only with the built-in renderer" on EVERY call, so it
            // must only run when no render pipeline asset is active.
            if (GraphicsSettings.currentRenderPipeline == null)
                camera.stereoTargetEye = xrEnabled ? StereoTargetEyeMask.Both : StereoTargetEyeMask.None;

#if UNIVERSALPLAYER_HDRP
            if (GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset)
            {
                // A camera without the additional-data component already renders XR by
                // default, so only add one when we actually need to turn XR off.
                if (camera.TryGetComponent(out HDAdditionalCameraData hdData))
                    hdData.xrRendering = xrEnabled;
                else if (!xrEnabled)
                    camera.gameObject.AddComponent<HDAdditionalCameraData>().xrRendering = false;
            }
#endif
#if UNIVERSALPLAYER_URP
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
            {
                if (camera.TryGetComponent(out UniversalAdditionalCameraData urpData))
                    urpData.allowXRRendering = xrEnabled;
                else if (!xrEnabled)
                    camera.gameObject.AddComponent<UniversalAdditionalCameraData>().allowXRRendering = false;
            }
#endif

            if (!xrEnabled)
            {
                // Leaving stereo strands the camera on the LEFT EYE's asymmetric
                // projection (and matching view/culling matrices): the flat desktop
                // render then still LOOKS like the VR left-eye mirror even though XR
                // rendering is off and the display is stopped — the "stays on Left Eye
                // after VR" bug. Explicit resets hand projection control back to
                // fieldOfView/aspect. Entering VR needs no counterpart: the XR layer
                // pushes fresh per-eye matrices every frame.
                camera.ResetStereoProjectionMatrices();
                camera.ResetStereoViewMatrices();
                camera.ResetProjectionMatrix();
                camera.ResetCullingMatrix();
                camera.ResetAspect();
            }
        }

        /// <summary>
        /// Reads back what <see cref="Set"/> controls on the ACTIVE pipeline — the test
        /// seam that keeps assertions pipeline-agnostic (HDRP in uvs-package-creator,
        /// URP in consumers, built-in fallback).
        /// </summary>
        public static bool IsXrEnabled(Camera camera)
        {
            if (camera == null) return false;
#if UNIVERSALPLAYER_HDRP
            if (GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset)
                return !camera.TryGetComponent(out HDAdditionalCameraData hdData) || hdData.xrRendering;
#endif
#if UNIVERSALPLAYER_URP
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                return !camera.TryGetComponent(out UniversalAdditionalCameraData urpData) || urpData.allowXRRendering;
#endif
            return camera.stereoTargetEye == StereoTargetEyeMask.Both;
        }
    }
}
