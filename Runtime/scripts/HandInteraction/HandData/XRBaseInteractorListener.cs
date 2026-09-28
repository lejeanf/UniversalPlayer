using UnityEngine;
using UnityEngine.Events;


namespace jeanf.universalplayer
{
    [System.Serializable]
    public class XRBaseInteractorEvent : UnityEvent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>
    {

    }

    /// <summary>
    /// Sits on a hand and relays the interactor an XRBaseInteractorSender reported for
    /// the SAME hand side (<see cref="PlayerEvents.HandInteractorReported"/>) to its
    /// UnityEvent — the prefab wires HandPoseManager.SetXRDirectInteractor there.
    /// </summary>
    public class XRBaseInteractorListener : MonoBehaviour
    {
        [Tooltip("Which hand this listener sits on — only reports tagged with this side are relayed.")]
        [SerializeField] private HandType hand = HandType.None;

        public XRBaseInteractorEvent OnEventRaised;

        private void OnEnable()
        {
            PlayerEvents.HandInteractorReported += Respond;
        }

        private void OnDisable()
        {
            PlayerEvents.HandInteractorReported -= Respond;
        }

        private void Respond(HandType side, UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor value)
        {
            if (side != hand) return;
            OnEventRaised?.Invoke(value);
        }
    }
}
