using jeanf.EventSystem;
using jeanf.validationTools;
using UnityEngine;

namespace jeanf.universalplayer
{
    [RequireComponent(typeof(HandPoseManager))]
    public class PointingPoseManager : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;
    
        [Validation("Default pose is required — the hand has nothing to return to when pointing ends and stays pointing.")]
        public Pose defaultPose;
        [Validation("Pointing pose is required — applying it is this component's whole job; nothing happens without it.")]
        public Pose pointingPose;

        [Header("Listening on:")]
        [Validation("Hand-pose channel is required — this component only reacts to that channel; the pointing pose never triggers without it.")]
        public BoolEventChannelSO handPoseEventChannelSO;

        private HandPoseManager _handPoseManager;


        private void Awake()
        {
            _handPoseManager = this.GetComponent<HandPoseManager>();
        }

        private void OnEnable()
        {
            if (handPoseEventChannelSO != null)
                handPoseEventChannelSO.OnEventRaised += SetPose;
        
        }

        private void OnDisable()
        {
            if (handPoseEventChannelSO != null)
                handPoseEventChannelSO.OnEventRaised -= SetPose;
        }

        private void SetPose(bool value)
        {
            // While an object is held, the grip pose owns the fingers — don't reopen the hand
            // into the pointing/default pose over the top of it.
            if (_handPoseManager.IsPoseHeld) return;
            var poseToSet = value ? pointingPose : defaultPose ;
            if(_isDebug) Debug.Log($"setting pose: {poseToSet.name}");
            _handPoseManager.ApplyPose(poseToSet);
        }
    }

}