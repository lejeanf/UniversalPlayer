using jeanf.EventSystem;
using UnityEngine;

namespace jeanf.universalplayer
{
    public class HandsDisplayer : MonoBehaviour
    {
        [SerializeField] GameObject rightHand;
        [SerializeField] GameObject leftHand;
        [SerializeField] VoidEventChannelSO changedControlSchemeChannel;

        private void Awake()
        {
            DisplayHands();
        }
        private void OnEnable()
        {
            changedControlSchemeChannel.OnEventRaised += DisplayHands; 
        }



        private void DisplayHands()
        {
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                rightHand?.SetActive(true);
                leftHand?.SetActive(true);
            }
            else
            {
                rightHand?.SetActive(false);
                leftHand?.SetActive(false);
            }
        }
    }
}

