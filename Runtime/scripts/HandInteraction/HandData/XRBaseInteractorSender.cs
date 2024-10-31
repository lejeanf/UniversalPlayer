using System;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;


namespace jeanf.vrplayer
{
    public class XRBaseInteractorSender : MonoBehaviour
    {
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor baseInteractor;
    
        [Header("Broadcasting on:")]
        [SerializeField] private XRBaseInteractorEventChannelSO XRBaseInteractorMessageChannel;

        public void SendXRDirectInteractor()
        {
            XRBaseInteractorMessageChannel.RaiseEvent(baseInteractor);
        }

        private void Update()
        {
            if (baseInteractor) return;
            baseInteractor = this.transform.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>();
            SendXRDirectInteractor();
        }
    }
}
