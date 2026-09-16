using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Sits on a hand's interactor object and reports the XRBaseInteractor it finds
    /// over <see cref="PlayerEvents.HandInteractorReported"/> (tagged with the hand side)
    /// so the matching XRBaseInteractorListener can hand it to the HandPoseManager.
    /// </summary>
    public class XRBaseInteractorSender : MonoBehaviour
    {
        private XRBaseInteractor baseInteractor;

        [Tooltip("Which hand this interactor belongs to — the XRBaseInteractorListener on that hand picks the report up.")]
        [SerializeField] private HandType hand = HandType.None;

        private bool _warnedNullInteractor;

        public void SendXRDirectInteractor()
        {
            if (!baseInteractor && !_warnedNullInteractor)
            {
                _warnedNullInteractor = true;
                Debug.LogWarning($"XRBaseInteractorSender on '{name}': no XRBaseInteractor found — broadcasting null.", this);
            }
            PlayerEvents.RaiseHandInteractorReported(hand, baseInteractor);
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
