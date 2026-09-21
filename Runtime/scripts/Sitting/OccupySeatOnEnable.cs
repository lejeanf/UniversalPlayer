using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Marks a <see cref="Seat"/> occupied for as long as this object is enabled —
    /// use on NPC discussion chairs so the player cannot sit in their place.
    /// Scenario sit (<see cref="SitPlayerOnEnable"/> / sit-request) can still force the seat.
    /// </summary>
    public class OccupySeatOnEnable : MonoBehaviour
    {
        [Tooltip("The seat to occupy. Leave empty to use a Seat on this object or its parents.")]
        [SerializeField] private Seat seat;

        private Seat Resolved => seat != null ? seat : GetComponentInParent<Seat>();

        private void OnEnable() => Resolved?.SetOccupied(true);

        private void OnDisable() => Resolved?.SetOccupied(false);
    }
}
