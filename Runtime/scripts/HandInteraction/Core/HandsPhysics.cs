using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace jeanf.vrplayer
{
    public class HandsPhysics : MonoBehaviour
    {
        [SerializeField] private Transform target;
        private Rigidbody rb;
        [SerializeField] private Quaternion offset;
        [SerializeField] private GameObject nonPhysicalHand;
        [SerializeField] private float showNonPhysicalHandDistance = 0.5f;
        Collider[] handColliders;
        [SerializeField] LayerMask layermask;
        void Start()
        {
            rb = GetComponent<Rigidbody>();
            handColliders = GetComponentsInChildren<Collider>();
        }

        private void OnEnable()
        {
            TakeObject.OnGrabDeactivateCollider += HandleColliders;
        }        
        private void OnDisable()
        {
            TakeObject.OnGrabDeactivateCollider -= HandleColliders;
        }
        private void Update()
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if(distance > showNonPhysicalHandDistance)
            {
                nonPhysicalHand.SetActive(true);
            }
            else
            {
                nonPhysicalHand.SetActive(false);
            }
        }
        void FixedUpdate()
        {
            rb.linearVelocity = (target.position - transform.position)/Time.fixedDeltaTime;
        

            Quaternion rotationDifference = target.rotation * Quaternion.Inverse(transform.rotation*offset);
            rotationDifference.ToAngleAxis(out float angleInDegree, out Vector3 rotationAxis);

            Vector3 rotationDifferenceInDegree = angleInDegree * rotationAxis;

            rb.angularVelocity = (rotationDifferenceInDegree * Mathf.Deg2Rad/Time.deltaTime);
        }

        void HandleColliders(bool value)
        {
            if (value)
            {
                foreach(Collider collider in handColliders)
                {
                    collider.excludeLayers = layermask;
                }
            }
            else
            {
                foreach(Collider collider in handColliders)
                {
                    collider.excludeLayers = 0;
                }
            }
        }
    }

}
