using Unity.Entities;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace jeanf.universalplayer
{
    /// <summary>
    /// A plain GameObject stand-in for a baked seat: an ordinary PhysX <see cref="BoxCollider"/>
    /// the desktop raycast can hit, an <see cref="XRSimpleInteractable"/> a VR controller ray can
    /// select, plus the seat's <see cref="SeatData"/> so <see cref="SitController"/> treats it
    /// exactly like a classic <see cref="Seat"/>. There is no ECS collider anywhere — the entity is
    /// pure data and this proxy is what physics/XRI actually see. Placed by <see cref="SeatDataBridge"/>.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SeatProxy : MonoBehaviour, ISeatSource
    {
        public Entity SeatEntity { get; private set; }
        private SeatData _data;
        private XRSimpleInteractable _interactable;

        /// <summary>Seats are static, so the pose is captured once at placement.</summary>
        public void Bind(Entity entity, in SeatData data)
        {
            SeatEntity = entity;
            _data = data;
        }

        public SeatData GetSeatData() => _data;

        // VR parity (Universal Player: every action works in MKB/gamepad/VR): a controller-ray
        // select sits, mirroring how the classic Seat auto-wires an XRSimpleInteractable. Desktop
        // and gamepad still sit through SitController's own raycast on the BoxCollider below.
        private void Awake()
        {
            _interactable = gameObject.AddComponent<XRSimpleInteractable>();
            _interactable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDestroy()
        {
            if (_interactable != null) _interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        private void OnSelectEntered(SelectEnterEventArgs _)
        {
            if (SitController.Instance != null) SitController.Instance.ToggleSit(this);
        }
    }
}
