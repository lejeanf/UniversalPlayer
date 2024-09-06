using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using jeanf.EventSystem;
namespace jeanf.vrplayer
{
    [DefaultExecutionOrder(1)]
   public class SnapObject : PickableObject
    {
        private GameObject nearestSnapPoint;
        public GameObject NearestSnapPoint { get { return nearestSnapPoint; } set { nearestSnapPoint = value; } }
        [SerializeField] private GameObjectEventChannelSO snapEventChannelSO;
        [SerializeField] LayerMask snapTargetLayer;
        private List<GameObject> snapPoints = new List<GameObject>();
        public List<GameObject> SnapPoints { get {  return snapPoints; } }
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.GetComponent<SnapZone>() != null)
            {
                SnapZone zone = other.gameObject.GetComponent<SnapZone>();
                foreach (GameObject snapPoint in zone.SnapPoints)
                {
                    snapPoints.Add(snapPoint);
                }
                snapEventChannelSO.RaiseEvent(this.gameObject);
            }
        }

        public LayerMask GetLayerMask()
        {
            return snapTargetLayer;
        }
    }
}
