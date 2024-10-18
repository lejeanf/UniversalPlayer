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
        [SerializeField] Quaternion snapOffsetRotation;
        [SerializeField] VoidEventChannelSO snapBegun;

        //On assigne
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.GetComponent<SnapZone>() && other.gameObject.tag == this.tag)
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
            if (other.gameObject.GetComponent<SnapZone>() && other.gameObject.tag == this.tag)
            {
                Snap();
                snapBegun.RaiseEvent();
            }
        }

        //On désassigne
        private void OnTriggerExit(Collider other)
        {

            if (other.gameObject.GetComponent<SnapZone>() && other.gameObject.tag == this.tag)
            {
                attachedSnapZone = null;
                nearestSnapPoint = null;
                OnSnap.Invoke(false);
            }
        }

        private void Snap()
        {
            RaycastHit hit;
            if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit, Mathf.Infinity, layerMask: snapTargetLayer))
            {
                OnSnap.Invoke(true);
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
                if (snapOffsetRotation != Quaternion.identity)
                {
                    OnSnapRotate.Invoke(transform, nearestSnapPoint.transform.rotation * snapOffsetRotation);
                }
                else
                {
                    OnSnapRotate.Invoke(transform, nearestSnapPoint.transform.rotation);
                }
                OnSnapMove.Invoke(transform, nearestSnapPoint.transform.position);

            }
            else
            {
                OnSnap.Invoke(false);
            }
        }
    }
}
