using jeanf.EventSystem;
using jeanf.vrplayer;
using UnityEngine;

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



    void DisplayHands()
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
