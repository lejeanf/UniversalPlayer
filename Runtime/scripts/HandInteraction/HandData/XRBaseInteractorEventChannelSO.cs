using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;


namespace jeanf.universalplayer
{
    [CreateAssetMenu(menuName = "Events/XRBaseInteractor Event Channel")]
    public class XRBaseInteractorEventChannelSO : DescriptionBaseSO
    {
        public UnityAction<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor> OnEventRaised;

        public void RaiseEvent(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor value)
        {
            OnEventRaised?.Invoke(value);
        }
    }
}
