using System;
using UnityEngine;

namespace jeanf.vrplayer
{
    [RequireComponent(typeof(Collider))]
    public class SetPoseOnTrigger : MonoBehaviour
    {
        private HandPoseManager _handPoseManager;
        
        [SerializeField] private Pose defaultPose;
        [SerializeField] private Pose poseToSet;

        [SerializeField] private bool isUsingGrabCheck = true;
        [SerializeField] private bool canRightHandPoint = false;
        [SerializeField] private bool canLeftHandPoint = false;


        private void OnTriggerEnter(Collider other)
        {
            if(!other.GetComponent(typeof(BlendableHand))) return;
            _handPoseManager = (HandPoseManager) other.GetComponentInParent(typeof(Transform)).GetComponentInParent(typeof(HandPoseManager));

            if (isUsingGrabCheck)
            {
                if(_handPoseManager.HandType == HandType.Left && canLeftHandPoint || _handPoseManager.HandType == HandType.Right && canRightHandPoint) _handPoseManager.ApplyPose(poseToSet);
            }
            else
            {
                _handPoseManager.ApplyPose(poseToSet);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.GetComponent(typeof(BlendableHand))) return;
            _handPoseManager = (HandPoseManager)other.GetComponentInParent(typeof(Transform)).GetComponentInParent(typeof(HandPoseManager));
            
            _handPoseManager.ApplyPose(defaultPose);
        }

        public void RightHandIsGrabbing()
        {
            canLeftHandPoint = true;
            canRightHandPoint = false;
        }
        public void LeftHandIsGrabbing()
        {
            canLeftHandPoint = false;
            canRightHandPoint = true;
        }

        public void NoHandIsGrabbing()
        {
            canLeftHandPoint = true;
            canRightHandPoint = true;
        }
    }
}
