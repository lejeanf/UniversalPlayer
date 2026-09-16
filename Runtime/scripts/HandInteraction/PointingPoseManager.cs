using jeanf.EventSystem;
using jeanf.validationTools;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Applies the pointing pose to THIS hand when PointOnCollisionTriggerWhenGrab says
    /// so (<see cref="PlayerEvents.HandPointingChanged"/>). Which hand it is comes from
    /// the HandPoseManager on the same object — no per-hand channel to wire.
    /// </summary>
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

        private HandPoseManager _handPoseManager;


        private void Awake()
        {
            _handPoseManager = this.GetComponent<HandPoseManager>();
        }

        private void OnEnable()
        {
            PlayerEvents.HandPointingChanged += OnHandPointingChanged;
        }

        private void OnDisable()
        {
            PlayerEvents.HandPointingChanged -= OnHandPointingChanged;
        }

        private void OnHandPointingChanged(HandType hand, bool pointing)
        {
            if (_handPoseManager == null || hand != _handPoseManager.HandType) return;
            SetPose(pointing);
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
