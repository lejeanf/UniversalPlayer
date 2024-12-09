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
        [SerializeField] HandType handType;
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

                    foreach (Collider collider in handColliders)
                    {
                        collider.excludeLayers = 0;
                    }
                    break;
                case IpadState.InLeftHand:
                    if (handType == HandType.Left)
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
                        foreach (Collider collider in handColliders)
                        {
                            collider.excludeLayers = 0;
                        }
                    }
                    break;
                case IpadState.InRightHand:
                    if (handType == HandType.Right)
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
                        foreach (Collider collider in handColliders)
                        {
                            collider.excludeLayers = 0;
                        }
                    }
                    break;
            }
        }
        void HandleColliders(bool value, HandType side)
        {
            if (side == handType)
            {
                if (value)
                {
                    foreach (Collider collider in handColliders)
                    {
                        collider.excludeLayers = ignoreTheseOnGrab;
                    }
                }
                else
                {
                    foreach (Collider collider in handColliders)
                    {
                        collider.excludeLayers = 0;
                    }
                }
            }
        }
    }

}
