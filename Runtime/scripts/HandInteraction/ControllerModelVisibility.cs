using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Sits on the controller model prefabs (XR Controller Left/Right): hides the model's
    /// renderers while the hand meshes are the player's representation (XR scheme), so a
    /// white controller never pokes through a palm. Everything stays tracked and
    /// functional — only the visuals toggle.
    /// </summary>
    public class ControllerModelVisibility : MonoBehaviour
    {
        [Tooltip("Hide this controller model while the hands are displayed (XR scheme). Turn off to always show the model.")]
        [SerializeField] private bool hideWhileHandsVisible = true;

        private Renderer[] renderers;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void OnEnable()
        {
            BroadcastControlsStatus.SendControlScheme += OnControlSchemeChanged;
            Apply(BroadcastControlsStatus.controlScheme);
        }

        private void OnDisable()
        {
            BroadcastControlsStatus.SendControlScheme -= OnControlSchemeChanged;
        }

        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme scheme) => Apply(scheme);

        private void Apply(BroadcastControlsStatus.ControlScheme scheme)
        {
            var hide = hideWhileHandsVisible && scheme == BroadcastControlsStatus.ControlScheme.XR;
            foreach (var modelRenderer in renderers)
            {
                if (modelRenderer != null) modelRenderer.enabled = !hide;
            }
        }
    }
}
