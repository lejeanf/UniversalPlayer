using jeanf.EventSystem;
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
    
        public Pose defaultPose;
        public Pose pointingPose;

        [Header("Listening on:")] public BoolEventChannelSO handPoseEventChannelSO;

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
                handPoseEventChannelSO.OnEventRaised -= null;
        }

        private void SetPose(bool value)
        {
            var poseToSet = value ? pointingPose : defaultPose ;
            if(_isDebug) Debug.Log($"setting pose: {poseToSet.name}");
            _handPoseManager.ApplyPose(poseToSet);
        }
    }

}