using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Shows the straight interaction ray's line only while it is hovering an
    /// interactable or world-space UI — so there is no permanent laser pointer
    /// cluttering the view. Ships on the Player root (zero wiring): it finds the
    /// straight-line <see cref="XRRayInteractor"/>s under the rig and gates their
    /// <see cref="XRInteractorLineVisual"/> on hover. The projectile teleport ray is
    /// ignored (it is driven separately by <see cref="StickTeleport"/>).
    /// </summary>
    public class InteractionRayHoverVisual : MonoBehaviour
    {
        [Tooltip("How often (seconds) to re-scan for ray interactors when none are cached yet (e.g. controllers spawn a frame late).")]
        [SerializeField] private float rescanInterval = 1f;

        private readonly List<XRRayInteractor> _rays = new List<XRRayInteractor>();
        private readonly List<XRInteractorLineVisual> _visuals = new List<XRInteractorLineVisual>();
        private float _nextScan;

        private void OnDisable()
        {
            for (int i = 0; i < _visuals.Count; i++)
                if (_visuals[i] != null) _visuals[i].enabled = false;
            _rays.Clear();
            _visuals.Clear();
        }

        private void Update()
        {
            // Outside VR the rays aren't used; keep their visuals off.
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR)
            {
                for (int i = 0; i < _visuals.Count; i++)
                    if (_visuals[i] != null && _visuals[i].enabled) _visuals[i].enabled = false;
                return;
            }

            if (_rays.Count == 0 && Time.unscaledTime >= _nextScan) Rescan();

            for (int i = 0; i < _rays.Count; i++)
            {
                var ray = _rays[i];
                var visual = _visuals[i];
                if (ray == null || visual == null) continue;
                var show = ShouldShow(ray);
                if (visual.enabled != show) visual.enabled = show; // XRInteractorLineVisual.OnDisable hides the line + reticle
            }
        }

        private void Rescan()
        {
            _nextScan = Time.unscaledTime + rescanInterval;
            _rays.Clear();
            _visuals.Clear();
            foreach (var ray in GetComponentsInChildren<XRRayInteractor>(true))
            {
                // The projectile-curve ray is the teleport aim (StickTeleport owns it).
                if (ray.lineType != XRRayInteractor.LineType.StraightLine) continue;
                if (!ray.TryGetComponent<XRInteractorLineVisual>(out var visual)) continue;
                _rays.Add(ray);
                _visuals.Add(visual);
                visual.enabled = false; // start hidden; only appear on hover
            }
        }

        /// <summary>True when the ray is over — or holding — something worth pointing at: a hovered/selected interactable or a world-space UI hit.</summary>
        private static bool ShouldShow(XRRayInteractor ray)
        {
            if (ray.interactablesHovered.Count > 0) return true;
            if (ray.interactablesSelected.Count > 0) return true; // keep the ray while pulling a distant object
            return ray.TryGetCurrentUIRaycastResult(out _);
        }
    }
}
