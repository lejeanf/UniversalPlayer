using Unity.Entities;
using Unity.Mathematics;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Data for a seat that was authored inside a SubScene and baked to an entity.
    /// The anchors are separate entities so their baked world pose (position AND facing)
    /// survives exactly; <see cref="SeatDataBridge"/> reads their LocalToWorld at runtime.
    /// </summary>
    public struct SeatComponent : IComponentData
    {
        /// <summary>The Seat's authored scenario-targeting id (0 = not targetable), baked as-is
        /// so <see cref="SeatDataBridge"/> can resolve "sit at seat N" requests by id.</summary>
        public int SeatId;

        public float EyeHeight;

        /// <summary>Hips position + facing. Never null (the baker falls back to the seat root).</summary>
        public Entity SitAnchor;

        /// <summary>Where the player stands on exit; <see cref="Entity.Null"/> when unset.</summary>
        public Entity ExitAnchor;

        /// <summary>Hand rest reached with IK during the glide; <see cref="Entity.Null"/> when unset.</summary>
        public Entity HandSupportAnchor;

        // Chair collider, baked from the Seat's BoxCollider and reproduced on a runtime proxy so
        // the GameObject-side raycast / hover can hit a baked seat. Local to the seat root transform.
        public float3 ColliderSize;
        public float3 ColliderCenter;
        public byte HasCollider;

        /// <summary>The seat GameObject's layer, so the proxy sits on the same physics layer.</summary>
        public int Layer;
    }

    /// <summary>
    /// A <see cref="SitPlayerOnEnable"/> that was authored inside a SubScene. The MonoBehaviour is
    /// stripped at runtime (baked SubScenes keep entities only), so its intent is baked to data and
    /// <see cref="SeatDataBridge"/> executes it when the entity streams in — the entity-world
    /// equivalent of "OnEnable": force-sit the player the moment this object's section is loaded.
    /// </summary>
    public struct ForceSitOnLoad : IComponentData
    {
        /// <summary>The directly-linked seat (same SubScene); <see cref="Entity.Null"/> when targeting by id.</summary>
        public Entity Seat;
        /// <summary>Authored Seat Id fallback for seats the SubScene cannot reference; 0 = unset.</summary>
        public int SeatId;
        public byte FadeToBlack;
        public float HoldBlackSeconds;
    }
}
