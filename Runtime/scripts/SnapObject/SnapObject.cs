using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using jeanf.EventSystem;
namespace jeanf.vrplayer
{
   public class SnapObject : PickableObject
    {
        private GameObject nearestSnapPoint;
        public GameObject NearestSnapPoint { get { return nearestSnapPoint; } set { nearestSnapPoint = value; } }
        [SerializeField] private GameObjectEventChannelSO snapEventChannelSO;
        [SerializeField] LayerMask snapTargetLayer;
        public LayerMask SnapTargetLayer { get { return snapTargetLayer; }}
        private List<GameObject> snapPoints = new List<GameObject>();
        public List<GameObject> SnapPoints { get {  return snapPoints; } }
        private SnapZone attachedSnapZone;
        public SnapZone AttachedSnapZone {  get { return attachedSnapZone; }}
        [SerializeField] bool shouldOrientOnSnap;
        public bool ShouldOrientOnSnap { get { return shouldOrientOnSnap; } }
        private void OnTriggerStay(Collider other)
        {
            if (other.gameObject.GetComponent<SnapZone>() != null)
            {
                attachedSnapZone = other.gameObject.GetComponent<SnapZone>();
                foreach (GameObject snapPoint in attachedSnapZone.SnapPoints)
                {
                    snapPoints.Add(snapPoint);
                }
                snapEventChannelSO.RaiseEvent(this.gameObject);
            }
        }


        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.GetComponent<SnapZone>() != null)
            {
                attachedSnapZone = null;
            }
        }
    }
}
