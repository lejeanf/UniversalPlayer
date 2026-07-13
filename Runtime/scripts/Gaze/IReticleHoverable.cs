namespace jeanf.universalplayer
{
    /// <summary>
    /// Marker: anything carrying this (on the collider or a parent) makes the
    /// reticle/cursor react on hover, even without an XRI interactable —
    /// tooltip-bearing objects implement it so "has a tooltip" always reads as
    /// "the cursor notices it".
    /// </summary>
    public interface IReticleHoverable { }
}
