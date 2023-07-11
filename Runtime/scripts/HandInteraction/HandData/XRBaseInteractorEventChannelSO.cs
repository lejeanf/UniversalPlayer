using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace jeanf.vrplayer
{
    [CreateAssetMenu(menuName = "Events/XRBaseInteractor Event Channel")]
    public class XRBaseInteractorEventChannelSO : DescriptionBaseSO
    {
        public UnityAction<XRBaseInteractor> OnEventRaised;

        public void RaiseEvent(XRBaseInteractor value)
        {
            OnEventRaised?.Invoke(value);
        }
    }
}
