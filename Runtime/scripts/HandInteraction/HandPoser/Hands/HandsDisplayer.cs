using jeanf.validationTools;
using UnityEngine;

namespace jeanf.universalplayer
{
    public class HandsDisplayer : MonoBehaviour
    {
        [Validation("Right hand object is required — HandsDisplayer is the only thing that shows/hides the VR hands; the right hand never appears in VR without it.")]
        [SerializeField] GameObject rightHand;
        [Validation("Left hand object is required — HandsDisplayer is the only thing that shows/hides the VR hands; the left hand never appears in VR without it.")]
        [SerializeField] GameObject leftHand;

        private void Awake()
        {
            DisplayHands();
        }
        private void OnEnable()
        {
            BroadcastControlsStatus.SendControlScheme += OnControlSchemeChanged;
        }
        private void OnDisable()
        {
            BroadcastControlsStatus.SendControlScheme -= OnControlSchemeChanged;
        }

        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme _) => DisplayHands();

        /// <summary>
        /// Test hook (used by Tools/Jeanf/UniversalPlayer/Hands Test Bench): shows or hides
        /// the hands regardless of the current control scheme, so they can be
        /// inspected without a headset. The next control-scheme change takes over again.
        /// </summary>
        public void ForceDisplay(bool show)
        {
            rightHand?.SetActive(show);
            leftHand?.SetActive(show);
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

