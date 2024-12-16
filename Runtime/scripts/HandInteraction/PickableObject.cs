using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace jeanf.vrplayer
{
    public class PickableObject : MonoBehaviour
    {
        //Bool
        [SerializeField] bool returnToInitialPositionOnRelease;
        public bool ReturnToInitialPositionOnRelease { get { return returnToInitialPositionOnRelease;} }


        //Save from initial position, rotation
        Vector3 initialPosition;
        Quaternion initialRotation;
        public Vector3 InitialPosition { get { return initialPosition; } }
        public Quaternion InitialRotation { get { return initialRotation; } }

        //Save rigidbody values
        Rigidbody rb;
        float initialDrag;
        float initialAngularDrag;
        bool initialUseGravity;
        [SerializeField] private Transform parent;
        public Rigidbody Rigidbody { get { return rb; }}
        public Transform Parent { get { return parent; }}

        public float InitialDrag { get { return initialDrag; } }
        public float InitialAngularDrag { get { return initialAngularDrag; } }
        public bool InitialUseGravity { get { return initialUseGravity; } }

        public bool canBeRejected;



        private void Awake()
        {
            initialPosition = this.gameObject.transform.position;
            initialRotation = this.gameObject.transform.rotation;
            rb = GetComponent<Rigidbody>();
            InitialSaveFromRigidbodySettings();
            parent = GetComponentInParent<Transform>();

        }
        private void InitialSaveFromRigidbodySettings()
        {
            initialPosition = this.gameObject.transform.position;
            initialDrag = rb.linearDamping;
            initialAngularDrag = rb.angularDamping;
            initialUseGravity = rb.useGravity;
        }
    }
}
