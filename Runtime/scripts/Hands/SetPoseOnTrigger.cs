using UnityEngine;

namespace jeanf.vrplayer
{
    [RequireComponent(typeof(Collider))]
    public class SetPoseOnTrigger : MonoBehaviour
    {
        private HandPoseManager _handPoseManager;
        
        [SerializeField] private Pose defaultPose;
        [SerializeField] private Pose poseToSet;

        private bool rightHandGrab = false;
        private bool leftHandGrab = false;

        [SerializeField] private bool isUsingGrabCheck = true;

        private void OnTriggerEnter(Collider other)
        {
            if(!other.GetComponent(typeof(BlendableHand))) return;
            _handPoseManager = (HandPoseManager) other.GetComponentInParent(typeof(Transform)).GetComponentInParent(typeof(HandPoseManager));
            if (isUsingGrabCheck && _handPoseManager.HandType == HandType.Left && leftHandGrab || isUsingGrabCheck && _handPoseManager.HandType == HandType.Right && rightHandGrab) return;
            if(_handPoseManager) _handPoseManager.ApplyPose(poseToSet);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.GetComponent(typeof(BlendableHand))) return;
            _handPoseManager = (HandPoseManager)other.GetComponentInParent(typeof(Transform)).GetComponentInParent(typeof(HandPoseManager));
            
            if (isUsingGrabCheck && _handPoseManager.HandType == HandType.Left && leftHandGrab || isUsingGrabCheck && _handPoseManager.HandType == HandType.Right && rightHandGrab) return;
            
            if (_handPoseManager) _handPoseManager.ApplyPose(defaultPose);
        }

        public void SetRightHandState(bool state)
        {
            rightHandGrab = state;
        }
        public void SetLeftHandState(bool state)
        {
            leftHandGrab = state;
        }
    }
}
