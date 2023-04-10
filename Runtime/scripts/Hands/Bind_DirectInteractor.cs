using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace  jeanf.vrplayer
{
    [RequireComponent(typeof(XRBaseInteractor))]
    public class Bind_DirectInteractor : MonoBehaviour
    {
        [SerializeField] HandType _handType = HandType.Left;
   
        private void OnEnable()
        {
            HandPoseManager.getDirectInteractor += SetDirectInteractor;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            HandPoseManager.getDirectInteractor -= null;
        }

        private void SetDirectInteractor(HandType handType, ref XRBaseInteractor directInteractor)
        {
            if (handType != _handType) return;
            directInteractor = GetComponent<XRBaseInteractor>();
        }
    }
}

