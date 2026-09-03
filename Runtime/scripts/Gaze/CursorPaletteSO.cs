using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The ONE set of pointer colours for the player: the screen cursor/reticle
    /// (<see cref="CursorStateController"/>) and the VR interaction ray
    /// (<see cref="InteractionRayHoverVisual"/>) both read THIS asset, so the two can
    /// never drift apart. Projects duplicate the packaged default and assign their copy
    /// on the Player variant's CursorStateController — nothing else needs wiring.
    /// </summary>
    [CreateAssetMenu(fileName = "CursorPalette", menuName = "UniversalPlayer/Cursor Palette")]
    public class CursorPaletteSO : ScriptableObject
    {
        [Tooltip("Resting colour, in every mode (tablet included — tablet changes shape, not colour). Also the ray while it points at nothing usable.")]
        public Color resting = Color.white;
        [Tooltip("While aiming at anything usable — interactables, seats, pickables, tooltip objects, world-space UI.")]
        public Color hover = new Color(0.35f, 0.95f, 0.6f);
        [Tooltip("While the interact/take/UI-press input is held on something usable (the click flash).")]
        public Color click = new Color(1f, 0.85f, 0.25f);
        [Tooltip("Flashed when a click/interaction is REJECTED (PlayerEvents.RaiseInvalidAction).")]
        public Color invalid = new Color(1f, 0.28f, 0.38f);

        private static CursorPaletteSO _fallback;
        /// <summary>The packaged default colours, for a rig with no palette wired. Never edited, never saved.</summary>
        public static CursorPaletteSO Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<CursorPaletteSO>();
                    _fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return _fallback;
            }
        }
    }
}
