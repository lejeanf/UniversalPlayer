using System;
using jeanf.validationTools;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
{
    public class XRBaseInteractorSender : MonoBehaviour
    {
        private XRBaseInteractor baseInteractor;
    
        [Header("Broadcasting on:")]
        [Validation("The XRBaseInteractor event channel is required — RaiseEvent is called on it unguarded (a null reference throws) and the hand's HandPoseManager never receives its interactor.")]
        [SerializeField] private XRBaseInteractorEventChannelSO XRBaseInteractorMessageChannel;

        private bool _warnedNullInteractor;

        public void SendXRDirectInteractor()
        {
            if (!baseInteractor && !_warnedNullInteractor)
            {
                _warnedNullInteractor = true;
                Debug.LogWarning($"XRBaseInteractorSender on '{name}': no XRBaseInteractor found — broadcasting null.", this);
            }
            XRBaseInteractorMessageChannel.RaiseEvent(baseInteractor);
        }

        private void Update()
        {
            if (baseInteractor) return;
            try
            {
                baseInteractor = this.transform.GetComponent<XRBaseInteractor>();
            }
            catch (Exception)
            {   
                baseInteractor = this.transform.GetComponentInChildren<XRBaseInteractor>();
            }
            SendXRDirectInteractor();
        }
    }
}
