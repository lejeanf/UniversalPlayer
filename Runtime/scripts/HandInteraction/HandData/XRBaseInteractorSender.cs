using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
{
    public class XRBaseInteractorSender : MonoBehaviour
    {
        private XRBaseInteractor baseInteractor;
    
        [Header("Broadcasting on:")]
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
