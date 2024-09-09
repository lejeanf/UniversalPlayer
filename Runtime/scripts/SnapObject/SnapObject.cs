using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using jeanf.EventSystem;
namespace jeanf.vrplayer
{
    [DefaultExecutionOrder(1)]
   public class SnapObject : PickableObject
    {
        private SnapPoint nearestSnapPoint;
        public SnapPoint NearestSnapPoint { get { return nearestSnapPoint; } set { nearestSnapPoint = value; } }
        [SerializeField] private GameObjectEventChannelSO snapEventChannelSO;
        [SerializeField] LayerMask snapTargetLayer;
        public LayerMask SnapTargetLayer { get { return snapTargetLayer; }}
        private List<SnapPoint> snapPoints = new List<SnapPoint>();
        public List<SnapPoint> SnapPoints { get {  return snapPoints; } }
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.GetComponent<SnapZone>() != null)
            {
                SnapZone zone = other.gameObject.GetComponent<SnapZone>();
                foreach (SnapPoint snapPoint in zone.SnapPoints)
                {
                    snapPoints.Add(snapPoint);
                }
                snapEventChannelSO.RaiseEvent(this.gameObject);
            }
        }
    }
}
