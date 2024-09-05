using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using jeanf.EventSystem;
namespace jeanf.vrplayer
{
    [DefaultExecutionOrder(1)]
   public class SnapObject : MonoBehaviour
    {
        private List<SnapZone> zones = new List<SnapZone>();
        private SnapZone nearestZone;
        private Quaternion startingRotation;
        [SerializeField] LayerMask auscultationPoint;
        [Header("Broadcasting On")]
        [SerializeField] private BoolEventChannelSO objectIsInSnapZone;
        private void OnEnable()
        {
            SnapZone.OnEnableSnapZone += AddZoneToList;
        }

        private void Start()
        {
            startingRotation = this.transform.rotation;
        }
        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            SnapZone.OnEnableSnapZone -= AddZoneToList;
        }
        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<SnapZone>() != null)
            {
                float minDistance = Mathf.Infinity;
                foreach (SnapZone snapZone in zones)
                {
                    float distance = Vector3.Distance(this.transform.position, snapZone.gameObject.transform.position);

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestZone = snapZone;
                    }
                }
                SnapObjectToZone();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            objectIsInSnapZone.RaiseEvent(false);
            this.transform.rotation = startingRotation;
        }

        private void AddZoneToList(SnapZone zone)
        {
            Debug.Log($"SNAP - Adding {zone.name} to zones");
            zones.Add(zone);
        }

        private void SnapObjectToZone()
        {
            //objectIsInSnapZone.RaiseEvent(true);
            ////Debug.Log("SNAP - Snapping to zone");
            ////Debug.Log($"SNAP - rotation is {this.transform.rotation} before");
            ////Debug.Log($"SNAP - Position is {this.transform.position} before");
            //this.transform.rotation = nearestZone.SnapObjectRotationValue;
            //this.transform.position = nearestZone.SnapObjectPositionValue;
            //this.GetComponent<Rigidbody>().isKinematic = true;
            ////Debug.Log($"SNAP - rotation is {this.transform.rotation} after");
            ////Debug.Log($"SNAP - Position is {this.transform.position} after");
            ///

            try
            {
                Ray ray = new Ray(transform.position, nearestZone.body.transform.position);
                RaycastHit info = new RaycastHit();
                if (Physics.Linecast(transform.position, nearestZone.body.transform.position, out info, auscultationPoint))
                {
                    transform.rotation = Quaternion.FromToRotation(Vector3.forward, info.normal);
                }
            }
            catch { }
        }
    }
}
