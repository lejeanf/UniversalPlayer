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
            if (!baseInteractor) Debug.Log("SendXRDirectInteractor, target is null");
            XRBaseInteractorMessageChannel.RaiseEvent(baseInteractor);
        }

        private void Update()
        {
            if (!baseInteractor) Debug.Log("update, target is null");

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
