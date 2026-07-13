using System.Collections.Generic;
using UnityEngine;

namespace jeanf.universalplayer
{
    [RequireComponent(typeof(Collider))]
    public class SetPoseOnTrigger : MonoBehaviour
    {
        [SerializeField] private Pose defaultPose;
        [SerializeField] private Pose poseToSet;

        [SerializeField] private bool isUsingGrabCheck = true;
        [SerializeField] private bool canRightHandPoint = false;
        [SerializeField] private bool canLeftHandPoint = false;

        // Hands now carry one collider PER FINGER BONE (HandColliderBuilder), so a
        // zone sees many enters/exits per hand: count contacts per hand and only
        // apply on the first collider in / release on the last collider out.
        private readonly Dictionary<HandPoseManager, int> handContacts = new Dictionary<HandPoseManager, int>();

        private void OnTriggerEnter(Collider other)
        {
            // Finger-bone colliders sit deep in the rig: identify the hand by the
            // HandPoseManager above them (was: BlendableHand on the collider itself,
            // which the per-bone boxes don't have).
            var handPoseManager = other.GetComponentInParent<HandPoseManager>();
            if (handPoseManager == null) return;

            if (isUsingGrabCheck
                && !(handPoseManager.HandType == HandType.Left && canLeftHandPoint
                     || handPoseManager.HandType == HandType.Right && canRightHandPoint)) return;

            handContacts.TryGetValue(handPoseManager, out var contacts);
            handContacts[handPoseManager] = contacts + 1;
            if (contacts > 0) return; // already posed by an earlier finger

            handPoseManager.AcquirePoseHold();
            handPoseManager.ApplyPose(poseToSet);
        }

        private void OnTriggerExit(Collider other)
        {
            var handPoseManager = other.GetComponentInParent<HandPoseManager>();
            if (handPoseManager == null || !handContacts.TryGetValue(handPoseManager, out var contacts)) return;

            if (contacts > 1)
            {
                handContacts[handPoseManager] = contacts - 1;
                return; // other fingers are still inside the zone
            }
            handContacts.Remove(handPoseManager);

            // Back to normal: release the hold first so ControllerHandPoseDriver
            // resumes immediately (the default pose covers projects without it).
            handPoseManager.ReleasePoseHold();
            handPoseManager.ApplyPose(defaultPose);
        }

        private void OnDisable()
        {
            // A zone destroyed/disabled mid-visit must not leak its holds.
            foreach (var handPoseManager in handContacts.Keys)
            {
                if (handPoseManager != null) handPoseManager.ReleasePoseHold();
            }
            handContacts.Clear();
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
