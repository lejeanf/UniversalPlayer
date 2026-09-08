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

        private void SetPose(bool pointing)
        {
            if (pointing)
            {
                var accepted = _handPoseManager.TryClaimPose(this, HandPoseSource.Pointing, pointingPose);
                if (_isDebug) Debug.Log($"pointing pose {(accepted ? "applied" : "refused")} on {_handPoseManager.name}");
                return;
            }
            _handPoseManager.ReleasePoseClaim(this);
        }
    }

}