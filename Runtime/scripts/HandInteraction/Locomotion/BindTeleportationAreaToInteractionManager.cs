using UnityEngine;


namespace jeanf.universalplayer
{
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea))]
    public class BindTeleportationAreaToInteractionManager : MonoBehaviour
    {
        
        private UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea _teleportationArea;
        //private InteractionManager _interactionManager;

        private void Awake()
        {
            _teleportationArea = this.GetComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
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

