using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.EventSystem;
namespace jeanf.universalplayer
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
        [SerializeField] bool shouldUseOffsetOnSnap;
        public bool ShouldOrientOnSnap { get { return shouldOrientOnSnap; } }
        public static event Action<Transform, Vector3> OnSnapMove;
        public static event Action<Transform, Quaternion> OnSnapRotate;
        public static event Action<bool> OnSnap;
        [SerializeField] Quaternion snapOffsetRotation;
        [SerializeField] VoidEventChannelSO snapBegun;
        [SerializeField] VoidEventChannelSO snapEnded;

        //On assigne
        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.GetComponent<SnapZone>())
            {
                attachedSnapZone = other.gameObject.GetComponent<SnapZone>();
                try
                {
                    snapBegun.RaiseEvent();
                }
                catch { }
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
                OnSnap.Invoke(false);
                snapEnded.RaiseEvent(); 
            }
        }

        private void Snap()
        {
            RaycastHit hit;
            // New Input System only — Input.mousePosition throws when the legacy
            // Input Manager is disabled. No mouse (gamepad/VR) = screen centre,
            // which is where the locked cursor sits anyway.
            var pointer = Mouse.current != null
                ? (Vector3)Mouse.current.position.ReadValue()
                : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            if (Physics.Raycast(Camera.main.ScreenPointToRay(pointer), out hit, Mathf.Infinity, layerMask: snapTargetLayer))
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
                if (shouldUseOffsetOnSnap)
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
