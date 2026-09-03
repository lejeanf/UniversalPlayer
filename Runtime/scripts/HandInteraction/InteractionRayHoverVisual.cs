using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Shows the interaction ray's line only while it is pointing at something worth
    /// pointing at — a hovered/selected interactable or world-space UI — so there is no
    /// permanent laser pointer cluttering the view. Ships on the Player root (zero
    /// wiring): it finds the far interactors under the rig and gates their line visual.
    ///
    /// Two interactor families are handled:
    ///  - <see cref="NearFarInteractor"/> (the packaged controllers' far ray, XRI 3): its
    ///    <see cref="CurveVisualController"/> is switched off with its LineRenderer, so the
    ///    XRI resting stub (a short line drawn whenever nothing is hit) never shows. UI
    ///    hover is tracked through the interactor's UI hover events.
    ///  - straight-line <see cref="XRRayInteractor"/>s (legacy rigs / project variants):
    ///    their <see cref="XRInteractorLineVisual"/> is gated the same way.
    /// The projectile teleport ray is ignored (<see cref="StickTeleport"/> owns it).
    ///
    /// Colours: the Near-Far line wears the cursor's <see cref="CursorPaletteSO"/> (resting /
    /// hover / click), read through <see cref="CursorStateController"/> so cursor and ray
    /// share ONE source and cannot diverge.
    /// </summary>
    public class InteractionRayHoverVisual : MonoBehaviour
    {
        [Tooltip("How often (seconds) to re-scan for ray interactors when none are cached yet (e.g. controllers spawn a frame late).")]
        [SerializeField] private float rescanInterval = 1f;

        private sealed class FarRay
        {
            public NearFarInteractor Interactor;
            public CurveVisualController Visual;
            public LineRenderer Line;
            public bool UiHovered;
            public UnityEngine.Events.UnityAction<UIHoverEventArgs> OnUiEnter;
            public UnityEngine.Events.UnityAction<UIHoverEventArgs> OnUiExit;
        }

        private readonly List<FarRay> _farRays = new List<FarRay>();
        private CursorStateController _cursor;
        private Color _appliedResting, _appliedHover, _appliedClick;
        private bool _paletteApplied;
        private readonly List<XRRayInteractor> _rays = new List<XRRayInteractor>();
        private readonly List<XRInteractorLineVisual> _visuals = new List<XRInteractorLineVisual>();
        private float _nextScan;

        private void OnDisable()
        {
            for (int i = 0; i < _visuals.Count; i++)
                if (_visuals[i] != null) _visuals[i].enabled = false;
            _rays.Clear();
            _visuals.Clear();
            ReleaseFarRays();
            _paletteApplied = false;
        }

        private void Update()
        {
            // Outside VR the rays aren't used; keep their visuals off.
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR)
            {
                for (int i = 0; i < _visuals.Count; i++)
                    if (_visuals[i] != null && _visuals[i].enabled) _visuals[i].enabled = false;
                for (int i = 0; i < _farRays.Count; i++) SetFarVisible(_farRays[i], false);
                return;
            }

            if (_rays.Count == 0 && _farRays.Count == 0 && Time.unscaledTime >= _nextScan) Rescan();

            for (int i = 0; i < _rays.Count; i++)
            {
                var ray = _rays[i];
                var visual = _visuals[i];
                if (ray == null || visual == null) continue;
                var show = ShouldShow(ray);
                if (visual.enabled != show) visual.enabled = show; // XRInteractorLineVisual.OnDisable hides the line + reticle
            }

            SyncPalette();
            for (int i = 0; i < _farRays.Count; i++)
            {
                var far = _farRays[i];
                if (far.Interactor == null || far.Visual == null) continue;
                SetFarVisible(far, ShouldShow(far));
            }
        }

        // The ray wears the cursor's colours: same CursorPaletteSO, read THROUGH the
        // CursorStateController so there is exactly one source. Re-applied whenever the
        // palette's values change (an asset edit in Play mode shows up live).
        private void SyncPalette()
        {
            if (_farRays.Count == 0) return;
            if (_cursor == null)
            {
                _cursor = GetComponentInChildren<CursorStateController>(true);
                if (_cursor == null) _cursor = FindAnyObjectByType<CursorStateController>(FindObjectsInactive.Include);
                if (_cursor == null) return;
            }
            var palette = _cursor.Palette;
            if (palette == null) return;
            if (_paletteApplied && palette.resting == _appliedResting && palette.hover == _appliedHover && palette.click == _appliedClick) return;
            _appliedResting = palette.resting;
            _appliedHover = palette.hover;
            _appliedClick = palette.click;
            _paletteApplied = true;
            for (int i = 0; i < _farRays.Count; i++)
                if (_farRays[i].Visual != null) ApplyPalette(_farRays[i].Visual, palette);
        }

        /// <summary>
        /// Colours a Near-Far curve visual from the palette: resting while pointing at
        /// nothing usable, hover over interactables and UI, click while selecting/pressing.
        /// Keeps each state's authored alpha fade (only the colour keys change).
        /// </summary>
        public static void ApplyPalette(CurveVisualController visual, CursorPaletteSO palette)
        {
            if (visual == null || palette == null) return;
            visual.customizeLinePropertiesForState = true; // per-state properties own the colour from here on
            visual.noValidHitProperties = Tint(visual.noValidHitProperties, palette.resting);
            visual.hoverHitProperties = Tint(visual.hoverHitProperties, palette.hover);
            visual.uiHitProperties = Tint(visual.uiHitProperties, palette.hover);
            visual.selectHitProperties = Tint(visual.selectHitProperties, palette.click);
            visual.uiPressHitProperties = Tint(visual.uiPressHitProperties, palette.click);
        }

        private static LineProperties Tint(LineProperties properties, Color color)
        {
            properties ??= new LineProperties();
            properties.adjustGradient = true;
            properties.gradient = Tinted(properties.gradient, color);
            return properties;
        }

        private static Gradient Tinted(Gradient source, Color color)
        {
            var alphaKeys = source != null && source.alphaKeys != null && source.alphaKeys.Length > 0
                ? source.alphaKeys
                : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
            var tinted = new Gradient();
            tinted.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) }, alphaKeys);
            return tinted;
        }

        private void Rescan()
        {
            _nextScan = Time.unscaledTime + rescanInterval;
            _rays.Clear();
            _visuals.Clear();
            ReleaseFarRays();
            _paletteApplied = false; // new visuals need the palette again

            foreach (var ray in GetComponentsInChildren<XRRayInteractor>(true))
            {
                // The projectile-curve ray is the teleport aim (StickTeleport owns it).
                if (ray.lineType != XRRayInteractor.LineType.StraightLine) continue;
                if (!ray.TryGetComponent<XRInteractorLineVisual>(out var visual)) continue;
                _rays.Add(ray);
                _visuals.Add(visual);
                visual.enabled = false; // start hidden; only appear on hover
            }

            foreach (var interactor in GetComponentsInChildren<NearFarInteractor>(true))
            {
                var visual = interactor.GetComponentInChildren<CurveVisualController>(true);
                if (visual == null) continue;
                var far = new FarRay
                {
                    Interactor = interactor,
                    Visual = visual,
                    Line = visual.GetComponentInChildren<LineRenderer>(true),
                };
                far.OnUiEnter = _ => far.UiHovered = true;
                far.OnUiExit = _ => far.UiHovered = false;
                interactor.uiHoverEntered.AddListener(far.OnUiEnter);
                interactor.uiHoverExited.AddListener(far.OnUiExit);
                _farRays.Add(far);
                SetFarVisible(far, false);
            }
        }

        private void ReleaseFarRays()
        {
            for (int i = 0; i < _farRays.Count; i++)
            {
                var far = _farRays[i];
                if (far.Interactor != null)
                {
                    far.Interactor.uiHoverEntered.RemoveListener(far.OnUiEnter);
                    far.Interactor.uiHoverExited.RemoveListener(far.OnUiExit);
                }
                SetFarVisible(far, false);
            }
            _farRays.Clear();
        }

        // CurveVisualController rewrites LineRenderer.enabled every frame from
        // Application.onBeforeRender while it is enabled, so hiding means switching the
        // controller off too; switching it back on hands the line straight back to XRI.
        private static void SetFarVisible(FarRay far, bool show)
        {
            if (far.Visual != null && far.Visual.enabled != show) far.Visual.enabled = show;
            if (!show && far.Line != null && far.Line.enabled) far.Line.enabled = false;
        }

        /// <summary>True when the ray is over — or holding — something worth pointing at: a hovered/selected interactable or a world-space UI hit.</summary>
        private static bool ShouldShow(XRRayInteractor ray)
        {
            if (ray.interactablesHovered.Count > 0) return true;
            if (ray.interactablesSelected.Count > 0) return true; // keep the ray while pulling a distant object
            return ray.TryGetCurrentUIRaycastResult(out _);
        }

        private static bool ShouldShow(FarRay far)
        {
            var interactor = far.Interactor;
            if (!interactor.isActiveAndEnabled) return false;
            if (interactor.hasHover) return true;
            if (interactor.hasSelection) return true; // keep the ray while pulling a distant object
            return far.UiHovered;
        }
    }
}
