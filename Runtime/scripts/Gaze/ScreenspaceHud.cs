using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The player's screen-space HUD, as ONE UI Toolkit document (ScreenspaceUI.uxml)
    /// instead of a pile of screen-space canvases: the cursor/reticle, the loading status
    /// line and the loading bar.
    ///
    /// It is deliberately NOT interactable (pickingMode = Ignore on everything): it is
    /// pure HUD, so it can never eat a click meant for the world or a world-space canvas.
    ///
    /// Loading arrives on SO channels, so this package needs no reference to the scene
    /// loading package — SceneManagement's LoadingInformation re-broadcasts its status and
    /// its (real, Addressables-measured) progress onto them.
    ///
    /// The cursor element is exposed rather than driven here: CursorStateController stays
    /// the single authority for cursor state/colour and pushes into it.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class ScreenspaceHud : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        // Element names in ScreenspaceUI.uxml.
        private const string CursorName = "Cursor";
        private const string LoadingName = "Loading";
        private const string InformationName = "Information";
        private const string ProgressName = "Progress";

        [Header("Listening on:")]
        [Tooltip("Loading status text (SceneManagement's LoadingInformation broadcasts it). Empty string = not loading.")]
        [SerializeField] private StringEventChannelSO loadingStatusChannel;
        [Tooltip("Loading progress 0..1 (real progress, measured by the loaders).")]
        [SerializeField] private FloatEventChannelSO loadingProgressChannel;

        [Header("Loading bar")]
        [Tooltip("Bar background while loading. White by design — the progress fill uses the cursor's colour on top of it.")]
        [SerializeField] private Color loadingBackgroundColor = Color.white;
        [Tooltip("Loading status text colour.")]
        [SerializeField] private Color informationColor = Color.white;
        [Tooltip("Fallback progress-fill colour when no CursorStateController is present (it normally supplies the cursor's resting colour).")]
        [SerializeField] private Color progressFallbackColor = new Color(1f, 0.72f, 0f);

        private UIDocument _document;
        private VisualElement _cursor;
        private VisualElement _loading;
        private Label _information;
        private VisualElement _progress;
        private CursorStateController _cursorColors;
        private float _warnAt;
        private bool _warned;
        private string _lastStatus = string.Empty;
        private float _lastProgress;

        /// <summary>The cursor/reticle element — CursorStateController drives it.</summary>
        public VisualElement CursorElement => _cursor;
        public bool IsReady => _cursor != null;

        /// <summary>The live HUD (there is one per player). Null before it comes up.</summary>
        public static ScreenspaceHud Active { get; private set; }

        private void OnEnable()
        {
            Active = this;
            _document = GetComponent<UIDocument>();
            _warnAt = Time.unscaledTime + 2f;
            _warned = false;

            if (loadingStatusChannel != null) loadingStatusChannel.OnEventRaised += OnLoadingStatus;
            if (loadingProgressChannel != null) loadingProgressChannel.OnEventRaised += OnLoadingProgress;

            // May be too early: UIDocument builds its visual tree in ITS OnEnable, and the
            // order between components is not guaranteed. Update() retries until it is up.
            TryQueryElements();
        }

        // Self-heals: the document's tree can appear a frame (or a scene load) after us.
        private void Update()
        {
            if (_cursor == null) TryQueryElements();
        }

        private void OnDisable()
        {
            if (loadingStatusChannel != null) loadingStatusChannel.OnEventRaised -= OnLoadingStatus;
            if (loadingProgressChannel != null) loadingProgressChannel.OnEventRaised -= OnLoadingProgress;
            if (Active == this) Active = null;
        }

        private bool TryQueryElements()
        {
            var root = _document != null ? _document.rootVisualElement : null;
            if (root == null)
            {
                WarnIfLate("the UIDocument has no root — assign a Source Asset (ScreenspaceUI.uxml) and a PanelSettings.");
                return false;
            }

            // HUD only: never intercept pointer events meant for the world/world-space UI.
            root.pickingMode = PickingMode.Ignore;
            foreach (var child in root.Query<VisualElement>().Build()) child.pickingMode = PickingMode.Ignore;

            _cursor = root.Q<VisualElement>(CursorName);
            _loading = root.Q<VisualElement>(LoadingName);
            _information = root.Q<Label>(InformationName);
            _progress = root.Q<VisualElement>(ProgressName);

            if (_cursor == null)
            {
                WarnIfLate($"ScreenspaceUI.uxml has no element named '{CursorName}' " +
                    $"(Loading={_loading != null}, Information={_information != null}, Progress={_progress != null}). " +
                    "The element names must match.");
                return false;
            }

            _cursorColors = GetComponentInParent<CursorStateController>() ?? FindFirstObjectByType<CursorStateController>();

            // Loading events can arrive before the tree exists — replay the latest so the
            // bar is never stuck on a stale/blank frame just because we came up late.
            OnLoadingStatus(_lastStatus);
            SetProgress(_lastProgress);
            return true;
        }

        // Missing elements for a frame or two is normal (document order, scene loads);
        // only complain if it never resolves, so a healthy startup stays silent.
        private void WarnIfLate(string message)
        {
            if (_warned || Time.unscaledTime < _warnAt) return;
            _warned = true;
            Debug.LogWarning($"{LogPrefix} ScreenspaceHud on '{name}': {message}", this);
        }

        // ---------------------------------------------------------------- cursor

        /// <summary>
        /// Renders the cursor. PURE STYLING — no sprite, so it stays sharp at any size:
        ///  - the ring is the element's BORDER, tinted to <paramref name="color"/>;
        ///  - <paramref name="fill"/> (0..1, tablet mode) fills the BACKGROUND with the
        ///    same colour — a float, not a flag, so the caller can EASE between the
        ///    default ring and the filled tablet dot instead of snapping;
        ///  - <paramref name="screenPosition"/> places it (screen pixels, y-up as Unity
        ///    reports them); null centres it, which is the locked/reticle case.
        /// Hidden = alpha 0 rather than display:none, so a fade never pops.
        /// CursorStateController owns WHAT to show; this only draws it.
        /// </summary>
        public void ApplyCursor(bool visible, Color color, float fill, Vector2? screenPosition, float scale)
        {
            if (_cursor == null) return;

            // Visibility is ONE lever: the element's opacity. The UXML authors the cursor
            // hidden via colour alphas; driving those too would mean two things fighting
            // over "is it visible", so the colours below are always written OPAQUE and
            // opacity alone decides.
            _cursor.style.opacity = visible ? 1f : 0f;

            var ring = color;
            ring.a = 1f;
            _cursor.style.borderTopColor = ring;
            _cursor.style.borderRightColor = ring;
            _cursor.style.borderBottomColor = ring;
            _cursor.style.borderLeftColor = ring;

            // Tablet mode fills the ring with its own colour; a continuous alpha so the
            // caller can ease the ring -> filled-dot transition.
            var background = color;
            background.a = Mathf.Clamp01(fill);
            _cursor.style.backgroundColor = background;

            _cursor.style.scale = new StyleScale(new Scale(Vector2.one * Mathf.Max(0.01f, scale)));

            // Absolute + a -50% translate: left/top then means "where the cursor POINTS",
            // independent of the element's size, so scaling never shifts the aim point.
            _cursor.style.position = Position.Absolute;
            _cursor.style.translate = new StyleTranslate(new Translate(Length.Percent(-50f), Length.Percent(-50f)));

            if (screenPosition.HasValue && _cursor.panel != null)
            {
                // Screen space is y-up, the panel is y-down.
                var screen = new Vector2(screenPosition.Value.x, Screen.height - screenPosition.Value.y);
                var panelPoint = RuntimePanelUtils.ScreenToPanel(_cursor.panel, screen);
                _cursor.style.left = panelPoint.x;
                _cursor.style.top = panelPoint.y;
            }
            else
            {
                _cursor.style.left = Length.Percent(50f);
                _cursor.style.top = Length.Percent(50f);
            }
        }

        // ---------------------------------------------------------------- loading

        // Empty status = not loading. That is the loaders' existing convention (they
        // raise "" when a session ends), so the bar follows it rather than inventing state.
        private void OnLoadingStatus(string status)
        {
            _lastStatus = status ?? string.Empty;
            var loading = !string.IsNullOrEmpty(_lastStatus);
            if (_information != null)
            {
                _information.text = _lastStatus;
                // Opaque text; the GROUP's opacity (below) decides whether it shows.
                var textColor = informationColor;
                textColor.a = 1f;
                _information.style.color = textColor;
            }
            SetLoadingVisible(loading);
            if (!loading) SetProgress(0f); // next session starts from empty, not from a stale full bar
        }

        private void OnLoadingProgress(float progress01) => SetProgress(progress01);

        // #Information and #Progress are CHILDREN of #Loading, so one opacity on the group
        // shows/hides the whole bar (background, label and fill) together — no per-element
        // alpha bookkeeping, and nothing can be left half-visible.
        private void SetLoadingVisible(bool visible)
        {
            if (_loading == null) return;
            _loading.style.opacity = visible ? 1f : 0f;
            var background = loadingBackgroundColor;
            background.a = 1f;
            _loading.style.backgroundColor = background;
        }

        private void SetProgress(float progress01)
        {
            _lastProgress = Mathf.Clamp01(progress01);
            if (_progress == null) return;
            _progress.style.width = Length.Percent(Mathf.Clamp01(progress01) * 100f);
            // The fill wears the cursor's resting colour so the HUD reads as one system.
            var fill = _cursorColors != null ? _cursorColors.RestingColor : progressFallbackColor;
            fill.a = 1f;
            _progress.style.backgroundColor = fill;
        }
    }
}
