using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

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
        [SerializeField] LayerMask ignoreTheseOnGrab;
        GameObject pokeInteractor;
        private enum HandSide
        {
            Left,
            Right
        }
        [SerializeField] HandSide handSide;
        void Start()
        {
            rb = GetComponent<Rigidbody>();
            handColliders = GetComponentsInChildren<Collider>();
            pokeInteractor = GetComponentInChildren<XRPokeInteractor>().gameObject;
        }

        private void OnEnable()
        {
            TakeObject.OnGrabDeactivateCollider += HandleColliders;
            GetPrimaryInHandItemWithVRController.OnIpadStateChanged += ctx => HandleCollidersForSpecificHand(ctx);
        }        
        private void OnDisable()
        {
            TakeObject.OnGrabDeactivateCollider -= HandleColliders;
            GetPrimaryInHandItemWithVRController.OnIpadStateChanged -= ctx => HandleCollidersForSpecificHand(ctx);

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

        void HandleCollidersForSpecificHand(IpadState value)
        {
            switch (value)
            {
                case IpadState.Disabled:
                    pokeInteractor.SetActive(true);
                    rb.isKinematic = true;
                    foreach (Collider collider in handColliders)
                    {
                        collider.excludeLayers = 0;
                    }
                    rb.isKinematic = false;
                    break;
                case IpadState.InLeftHand:
                    if (handSide == HandSide.Left)
                    {
                        foreach (Collider collider in handColliders)
                        {
                            collider.excludeLayers = ignoreTheseOnGrab;
                            pokeInteractor.SetActive(false);
                        }
                    }
                    else
                    {
                        pokeInteractor.SetActive(true);
                        rb.isKinematic = true;
                        foreach (Collider collider in handColliders)
                        {
                            collider.excludeLayers = 0;
                        }
                        rb.isKinematic = false;
                    }
                    break;
                case IpadState.InRightHand:
                    if (handSide == HandSide.Right)
                    {
                        foreach (Collider collider in handColliders)
                        {
                            collider.excludeLayers = ignoreTheseOnGrab;
                            pokeInteractor.SetActive(false);
                        }
                    }
                    else
                    {
                        pokeInteractor.SetActive(true);
                        rb.isKinematic = true;
                        foreach (Collider collider in handColliders)
                        {
                            collider.excludeLayers = 0;
                        }
                        rb.isKinematic = false;
                    }
                    break;
            }
        }
        void HandleColliders(bool value)
        {
            if (value)
            {
                foreach(Collider collider in handColliders)
                {
                    collider.excludeLayers = ignoreTheseOnGrab;
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
