using Unity.Entities;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// A plain GameObject stand-in for a baked seat: an ordinary PhysX <see cref="BoxCollider"/>
    /// the desktop raycast / VR interactor can hit, plus the seat's <see cref="SeatData"/> so
    /// <see cref="SitController"/> treats it exactly like a classic <see cref="Seat"/>. There is no
    /// ECS collider anywhere — the entity is pure data and this proxy is what physics actually sees.
    /// Placed and recycled by <see cref="SeatColliderPoolManager"/>.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SeatProxy : MonoBehaviour, ISeatSource
    {
        public Entity SeatEntity { get; private set; }
        private SeatData _data;

        /// <summary>Seats are static, so the pose is captured once at placement.</summary>
        public void Bind(Entity entity, in SeatData data)
        {
            SeatEntity = entity;
            _data = data;
        }

        public SeatData GetSeatData() => _data;
    }
}
