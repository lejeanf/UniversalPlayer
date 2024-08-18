using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace jeanf.vrplayer
{
    public class NormalDisplayer : MonoBehaviour
    {
        bool isInAuscultationZone;
        [SerializeField] LayerMask auscultationPoint;
        GameObject alignmentReference;
        private void Update()
        {
            if (isInAuscultationZone) SurfaceAlignment(alignmentReference);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<OrientObjectAlongTowardPoint>() != null)
            {
                isInAuscultationZone = true;
                alignmentReference = other.GetComponent<OrientObjectAlongTowardPoint>().targetPoint;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<OrientObjectAlongTowardPoint>() != null)
            {
                isInAuscultationZone = false;
                alignmentReference = null;

            }
        }
        private void SurfaceAlignment(GameObject alignmentReference)
        {
            try
            {
                Ray ray = new Ray(transform.position, alignmentReference.transform.position);
                RaycastHit info = new RaycastHit();
                if (Physics.Linecast(transform.position, alignmentReference.transform.position, out info, auscultationPoint))
                {
                    transform.rotation = Quaternion.FromToRotation(Vector3.forward, info.normal);
                }
            } catch { }

        }
    }
}
