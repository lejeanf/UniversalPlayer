using Unity.Entities;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Bakes a classic <see cref="Seat"/> that lives in a SubScene into <see cref="SeatComponent"/>.
    /// The very same component drives the additive-scene flow (as a MonoBehaviour) and the entity
    /// world (baked) — drop a Seat and it works in whichever world it ends up in. This baker only
    /// runs at SubScene bake time; in a normal scene the Seat MonoBehaviour just runs as before.
    /// </summary>
    public class SeatBaker : Baker<Seat>
    {
        public override void Bake(Seat authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            var sit = authoring.SitAnchor;   // never null: falls back to the seat transform
            var exit = authoring.ExitAnchor; // may be null
            var hand = authoring.HandSupportAnchor; // may be null

            AddComponent(entity, new SeatComponent
            {
                EyeHeight = authoring.EyeHeightAboveSeat,
                SitAnchor = GetEntity(sit, TransformUsageFlags.Dynamic),
                ExitAnchor = exit != null ? GetEntity(exit, TransformUsageFlags.Dynamic) : Entity.Null,
                HandSupportAnchor = hand != null ? GetEntity(hand, TransformUsageFlags.Dynamic) : Entity.Null,
            });
        }
    }
}
