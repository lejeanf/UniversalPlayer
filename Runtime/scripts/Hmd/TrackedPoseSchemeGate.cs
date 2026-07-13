using UnityEngine;
#pragma warning disable 0618 // ActionBasedController: deprecated in XRI 3, still what the gaze rig runs on

namespace jeanf.universalplayer
{
    /// <summary>
    /// Disables XR pose-driving behaviours outside the XR scheme and snaps the local
    /// pose back to identity. A plugged-in headset keeps STREAMING tracking data even
    /// in desktop mode, so anything pose-driven by XR actions (the gaze interactor,
    /// controller rigs) would aim wherever the desk headset points — the camera's
    /// TrackedPoseDriver gets the same treatment in FPSCameraMovement.
    /// </summary>
    public class TrackedPoseSchemeGate : MonoBehaviour
    {
        [Tooltip("Enabled only while the XR scheme is active (e.g. the ActionBasedController driving this transform from HMD/controller tracking).")]
        [SerializeField] private Behaviour[] xrOnlyBehaviours;
        [Tooltip("Controllers that keep their INPUT (select/UI press) in every mode but must stop pose-tracking outside XR — the gaze rig's controller, so its ray keeps clicking on desktop.")]
        [SerializeField] private UnityEngine.XR.Interaction.Toolkit.ActionBasedController[] trackingOnlyControllers;
        [Tooltip("Snap this transform's local pose back to identity when leaving XR, so it follows its parent (the camera look) again.")]
        [SerializeField] private bool resetLocalPoseOutsideXr = true;

        private void OnEnable()
        {
            BroadcastControlsStatus.SendControlScheme += OnControlSchemeChanged;
            Apply(BroadcastControlsStatus.controlScheme);
        }

        private void OnDisable()
        {
            BroadcastControlsStatus.SendControlScheme -= OnControlSchemeChanged;
        }

        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme scheme) => Apply(scheme);

        private void Apply(BroadcastControlsStatus.ControlScheme scheme)
        {
            var xr = scheme == BroadcastControlsStatus.ControlScheme.XR;
            if (xrOnlyBehaviours != null)
            {
                foreach (var behaviour in xrOnlyBehaviours)
                {
                    if (behaviour != null) behaviour.enabled = xr;
                }
            }
            if (trackingOnlyControllers != null)
            {
                foreach (var controller in trackingOnlyControllers)
                {
                    if (controller != null) controller.enableInputTracking = xr;
                }
            }
            if (!xr && resetLocalPoseOutsideXr)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
            }
        }
    }
}
