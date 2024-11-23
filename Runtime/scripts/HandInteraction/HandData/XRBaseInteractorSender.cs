using System;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.vrplayer
{
    public class XRBaseInteractorSender : MonoBehaviour
    {
        private XRBaseInteractor baseInteractor;
    
        [Header("Broadcasting on:")]
        [SerializeField] private XRBaseInteractorEventChannelSO XRBaseInteractorMessageChannel;

        public void SendXRDirectInteractor()
        {
            XRBaseInteractorMessageChannel.RaiseEvent(baseInteractor);
        }

        private void Update()
        {
            if (baseInteractor) return;
            try
            {
                baseInteractor = this.transform.GetComponent<XRDirectInteractor>();
            }
            catch (Exception)
            {

                throw;
            }
            baseInteractor = this.transform.GetComponentInChildren<XRDirectInteractor>();
            SendXRDirectInteractor();
        }
    }
}
