using Unity.Entities;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Data for a seat that was authored inside a SubScene and baked to an entity.
    /// The anchors are separate entities so their baked world pose (position AND facing)
    /// survives exactly; <see cref="SeatDataBridge"/> reads their LocalToWorld at runtime.
    /// </summary>
    public struct SeatComponent : IComponentData
    {
        public float EyeHeight;

        /// <summary>Hips position + facing. Never null (the baker falls back to the seat root).</summary>
        public Entity SitAnchor;

        /// <summary>Where the player stands on exit; <see cref="Entity.Null"/> when unset.</summary>
        public Entity ExitAnchor;

        /// <summary>Hand rest reached with IK during the glide; <see cref="Entity.Null"/> when unset.</summary>
        public Entity HandSupportAnchor;
    }
}
