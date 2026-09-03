using jeanf.EventSystem;
using jeanf.validationTools;
using UnityEngine;

namespace jeanf.universalplayer
{
    public class PoseContainer : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        // The pose applied when this object is grabbed. Read by the grab flow
        // (HandPoseManager.ResolveGrabPose / PoseGrabInteractable). The legacy manual
        // attach-transform fields and SetAttachTransform() were removed — the held object's
        // offset is now the pose's wrist-relative anchor, applied by PoseGrabInteractable.
        [Validation("Pose is required — a PoseContainer with no pose does nothing (the grab keeps XRI's default attach and the fingers never close).")]
        public Pose pose = null;
    }
}
