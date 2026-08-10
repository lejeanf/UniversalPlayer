using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Bakes a classic <see cref="Seat"/> that lives in a SubScene into <see cref="SeatComponent"/>.
    /// The very same component drives the additive-scene flow (as a MonoBehaviour) and the entity
    /// world (baked) — drop a Seat and it works in whichever world it ends up in. This baker only
    /// runs at SubScene bake time; in a normal scene the Seat MonoBehaviour just runs as before.
    ///
    /// For the entity path the seat's BoxCollider should sit on the Seat root, so the runtime proxy
    /// reproduces it at the seat's LocalToWorld. A child collider is still baked but may be offset.
    /// </summary>
    public class SeatBaker : Baker<Seat>
    {
        public override void Bake(Seat authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            var sit = authoring.SitAnchor;   // never null: falls back to the seat transform
            var exit = authoring.ExitAnchor; // may be null
            var hand = authoring.HandSupportAnchor; // may be null

            var box = authoring.GetComponent<BoxCollider>();
            if (box == null) box = authoring.GetComponentInChildren<BoxCollider>();
            var hasCollider = box != null;
            if (hasCollider) DependsOn(box);

            AddComponent(entity, new SeatComponent
            {
                EyeHeight = authoring.EyeHeightAboveSeat,
                SitAnchor = GetEntity(sit, TransformUsageFlags.Dynamic),
                ExitAnchor = exit != null ? GetEntity(exit, TransformUsageFlags.Dynamic) : Entity.Null,
                HandSupportAnchor = hand != null ? GetEntity(hand, TransformUsageFlags.Dynamic) : Entity.Null,
                ColliderSize = hasCollider ? (float3)(Vector3)box.size : new float3(0.6f, 1f, 0.6f),
                ColliderCenter = hasCollider ? (float3)(Vector3)box.center : new float3(0f, 0.5f, 0f),
                HasCollider = (byte)(hasCollider ? 1 : 0),
                Layer = authoring.gameObject.layer,
            });
        }
    }
}
