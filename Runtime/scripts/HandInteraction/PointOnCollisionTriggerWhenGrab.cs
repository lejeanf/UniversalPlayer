using jeanf.EventSystem;
using UnityEngine;
namespace jeanf.universalplayer
{
    /// <summary>
    /// Counts grabbing VR hands (HandPoseManager reports them over
    /// <see cref="PlayerEvents.HandGrabStateChanged"/>) and, when a hand enters a
    /// detection zone (HandDetectionZoneReporter) while the OTHER hand is grabbing,
    /// puts the free hand in its pointing pose (<see cref="PlayerEvents.HandPointingChanged"/>,
    /// which PointingPoseManager applies and the bridge reports on the hub).
    /// </summary>
    public class PointOnCollisionTriggerWhenGrab : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }

        [SerializeField] private bool _isDebug = false;

        [Space(10)] [SerializeField] private int grabCount = 0;
        [SerializeField] private int handsInDetectionzone = 0;

        [SerializeField] private bool setPointingPoseOnOppositeHandGrab = false;

        [SerializeField] private bool leftHandGrabState = false;
        [SerializeField] private bool rightHandGrabState = false;

        private void OnEnable()
        {
            PlayerEvents.HandGrabStateChanged += RegisterHandGrabState;
            PlayerEvents.HandEnteredDetectionZone += HandDetectedInPointingZone;
            PlayerEvents.HandLeftDetectionZone += HandDisappearedInPointingZone;
            GetPrimaryInHandItemWithVRController.OnIpadStateChanged += SetGrabState;
        }

        private void OnDisable()
        {
            PlayerEvents.HandGrabStateChanged -= RegisterHandGrabState;
            PlayerEvents.HandEnteredDetectionZone -= HandDetectedInPointingZone;
            PlayerEvents.HandLeftDetectionZone -= HandDisappearedInPointingZone;
            GetPrimaryInHandItemWithVRController.OnIpadStateChanged -= SetGrabState;
        }

        private void RegisterHandGrabState(HandType hand, bool grabbing)
        {
            switch (hand)
            {
                case HandType.Left: RegisterLeftHandState(grabbing); break;
                case HandType.Right: RegisterRightHandState(grabbing); break;
            }
        }

        private void CountTotalGrabsInAction(bool value)
        {
            if (isDebug) Debug.Log($"Grab event received: {value}");

            grabCount = value ? grabCount += 1 : grabCount -= 1;
            SendGrabCount(grabCount);
        }

        private void RegisterLeftHandState(bool value)
        {
            leftHandGrabState = value;
            CountTotalGrabsInAction(value);
        }

        private void RegisterRightHandState(bool value)
        {
            rightHandGrabState = value;
            CountTotalGrabsInAction(value);

        }

        private void SendGrabCount(int value)
        {
            PlayerEvents.RaiseGrabCount(value);
        }

        private void HandDetectedInPointingZone()
        {
            handsInDetectionzone += 1;

            SetPointingPose(true);
        }

        private void SetGrabState(IpadState grabState)
        {
            switch (grabState)
            {
                case IpadState.InLeftHand:
                    leftHandGrabState = true;
                    rightHandGrabState = false;
                    break;
                case IpadState.InRightHand:
                    rightHandGrabState = true;
                    leftHandGrabState = false;
                    break;
                case IpadState.Disabled:
                    leftHandGrabState = false;
                    rightHandGrabState = false;
                    SetDefaultPose();
                    break;
            }
        }

        private void HandDisappearedInPointingZone()
        {
            handsInDetectionzone -= 1;
            if (handsInDetectionzone < 0) handsInDetectionzone = 0;

            SetPointingPose(false);
        }

        private void SetPointingPose(bool state)
        {
            if(!setPointingPoseOnOppositeHandGrab) return;
            if (state)
            {
                Point();
            }
            else
            {
                SetDefaultPose();
            }
        }

        private void Point()
        {
            if(_isDebug) Debug.Log("grab > 1 handInDetectZone > 1");
            if (leftHandGrabState)
            {
                if(_isDebug) Debug.Log("setting RIGHT hand pointing to TRUE");
                PlayerEvents.RaiseHandPointing(HandType.Right, true);
            }

            else if (rightHandGrabState)
            {
                if(_isDebug) Debug.Log("setting LEFT hand pointing to TRUE");
                PlayerEvents.RaiseHandPointing(HandType.Left, true);
            }
        }


        private void SetDefaultPose()
        {
            if(_isDebug) Debug.Log("setting RIGHT hand pointing to FALSE");
            PlayerEvents.RaiseHandPointing(HandType.Right, false);
            if(_isDebug) Debug.Log("setting LEFT hand pointing to FALSE");
            PlayerEvents.RaiseHandPointing(HandType.Left, false);
        }
    }
}
