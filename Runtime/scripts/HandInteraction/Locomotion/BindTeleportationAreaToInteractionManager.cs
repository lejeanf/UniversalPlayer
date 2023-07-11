using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace jeanf.vrplayer
{
    [RequireComponent(typeof(TeleportationArea))]
    public class BindTeleportationAreaToInteractionManager : MonoBehaviour
    {
        
        private TeleportationArea _teleportationArea;
        //private InteractionManager _interactionManager;

        private void Awake()
        {
            _teleportationArea = this.GetComponent<TeleportationArea>();
        }

        private void OnEnable()
        {
            // _interactionManager.OnInteractionStart += _teleportationArea.EnableTeleportation;
            // _interactionManager.OnInteractionEnd += _teleportationArea.DisableTeleportation;
        }

        private void OnDisable()
        {
            // _interactionManager.OnInteractionStart -= _teleportationArea.EnableTeleportation;
            // _interactionManager.OnInteractionEnd -= _teleportationArea.DisableTeleportation;
        }
    }
}

