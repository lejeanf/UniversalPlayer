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


        //On assigne
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.GetComponent<SnapZone>() != null)
            {
                attachedSnapZone = other.gameObject.GetComponent<SnapZone>();

                foreach (GameObject snapPoint in attachedSnapZone.SnapPoints)
                {
                    snapPoints.Add(snapPoint);
                }
            }
        }

        //On call le snap
        private void OnTriggerStay(Collider other)
        {
            Snap();
        }

        //On désassigne
        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.GetComponent<SnapZone>() != null)
            {
                attachedSnapZone = null;
                nearestSnapPoint = null;
            }
        }

        private void Snap()
        {
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, Mathf.Infinity, layerMask: snapTargetLayer))
            {
                float minDistance = Mathf.Infinity;

                foreach (GameObject snapPoint in SnapPoints)
                {
                    float distance = Vector3.Distance(hit.point, snapPoint.transform.position);

                    if (distance < minDistance)
                    {
                        minDistance = distance;

                        nearestSnapPoint = snapPoint;
                    }
                }
                this.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                /*SetRotation*/   //snapObject.transform.LookAt(snapObject.AttachedSnapZone.LookTarget.transform.position);
                /*SetPosition*/   //SetObjectPosition(snapObject.transform, snapObject.NearestSnapPoint.transform.position, true);

            }
            else
            {
                this.GetComponent<Rigidbody>().constraints = ~RigidbodyConstraints.FreezePosition;

            }
        }
    }
}
