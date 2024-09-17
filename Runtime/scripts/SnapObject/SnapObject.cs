using System.Collections;
using System;
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
        public static event Action<Transform, Vector3> OnSnapMove;
        public static event Action<Transform, Quaternion> OnSnapRotate;
        public static event Action<bool> OnSnap;

        //On assigne
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log("ENTERING TRIGGER" + other.name);

            if (other.gameObject.GetComponent<SnapZone>())
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
            if (other.gameObject.GetComponent<SnapZone>())
            {
                Debug.Log("STAYING IN TRIGGER" + other.name);
                Snap();
            }
        }

        //On désassigne
        private void OnTriggerExit(Collider other)
        {

            if (other.gameObject.GetComponent<SnapZone>())
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
                Debug.Log("RAYCAST WORKED");
                OnSnap.Invoke(true);
                float minDistance = Mathf.Infinity;
                GameObject refSnapPoint = null;
                foreach (GameObject snapPoint in SnapPoints)
                {
                    float distance = Vector3.Distance(hit.point, snapPoint.transform.position);

                    if (distance < minDistance)
                    {
                        minDistance = distance;

                        refSnapPoint = snapPoint;
                    }

                }
                if (refSnapPoint != nearestSnapPoint)
                {
                    nearestSnapPoint = refSnapPoint;
                    this.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;

                    if (shouldOrientOnSnap)
                    {
                        try
                        {
                            SnapZone snapZone = attachedSnapZone.GetComponent<SnapZone>();

                            OnSnapRotate.Invoke(transform, nearestSnapPoint.transform.rotation);
                        }
                        catch
                        {
                            Debug.LogWarning("Cannot orient towards organ, not an auscultation zone");
                        }
                    }
                    OnSnapMove.Invoke(transform, nearestSnapPoint.transform.position);
                }
                else
                {
                    this.GetComponent<Rigidbody>().constraints = ~RigidbodyConstraints.FreezePosition;
                }
            }
            else
            {
                OnSnap.Invoke(false);
            }
        }
    }
}
