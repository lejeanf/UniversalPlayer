using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
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

        [SerializeField] VoidEventChannelSO controlSchemeChangeEvent;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            handColliders = GetComponentsInChildren<Collider>();
            pokeInteractor = GetComponentInChildren<XRPokeInteractor>().gameObject;
            CheckXRStatus();
        }

        private void OnEnable()
        {
            TakeObject.OnGrabDeactivateCollider += HandleColliders;
            GetPrimaryInHandItemWithVRController.OnIpadStateChanged += ctx => HandleCollidersForSpecificHand(ctx);
            controlSchemeChangeEvent.OnEventRaised += CheckXRStatus;
            PrimaryItemController.TriggerLastUsedHand += HandleColliders;
        }
        private void OnDisable()
        {
            TakeObject.OnGrabDeactivateCollider -= HandleColliders;
            GetPrimaryInHandItemWithVRController.OnIpadStateChanged -= ctx => HandleCollidersForSpecificHand(ctx);
            controlSchemeChangeEvent.OnEventRaised -= CheckXRStatus;
            PrimaryItemController.TriggerLastUsedHand -= HandleColliders;


        }
        private void Update()
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > showNonPhysicalHandDistance)
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
            rb.linearVelocity = (target.position - transform.position) / Time.fixedDeltaTime;


            Quaternion rotationDifference = target.rotation * Quaternion.Inverse(transform.rotation * offset);
            rotationDifference.ToAngleAxis(out float angleInDegree, out Vector3 rotationAxis);

            Vector3 rotationDifferenceInDegree = angleInDegree * rotationAxis;

            rb.angularVelocity = (rotationDifferenceInDegree * Mathf.Deg2Rad / Time.deltaTime);
        }

        private void CheckXRStatus()
        {
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                transform.parent.gameObject.SetActive(true);
            }
            else
            {
                transform.parent.gameObject.SetActive(false);
            }
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

        void HandleColliders(XRHandsInteractionManager.LastUsedHand hand, bool state)
        {
            switch (hand)
            {
                case XRHandsInteractionManager.LastUsedHand.LeftHand:
                    if (handType != HandType.Right) return;
                    if (state)
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
                    break;
                case XRHandsInteractionManager.LastUsedHand.RightHand:
                    if (handType != HandType.Left) return;
                    if (state)
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
                    break;

            }
        }
    }

}
