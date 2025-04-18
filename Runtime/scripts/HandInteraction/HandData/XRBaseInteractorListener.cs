using UnityEngine;
using UnityEngine.Events;


namespace jeanf.universalplayer
{
    [System.Serializable]
    public class XRBaseInteractorEvent : UnityEvent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>
    {

    }
	
    public class XRBaseInteractorListener : MonoBehaviour
    {
        public XRBaseInteractorEventChannelSO _channel = default;

        public XRBaseInteractorEvent OnEventRaised;

        private void OnEnable()
        {
            if (_channel != null)
                _channel.OnEventRaised += Respond;
        }

        private void OnDisable()
        {
            if (_channel != null)
                _channel.OnEventRaised -= Respond;
        }

        private void Respond(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor value)
        {
            OnEventRaised?.Invoke(value);
        }

        public XRBaseInteractorListener(XRBaseInteractorEventChannelSO _channel, XRBaseInteractorEvent onEventRaised)
        {
            this._channel = _channel;
            this.OnEventRaised = onEventRaised;
        }
    }
}