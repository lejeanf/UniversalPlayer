using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace jeanf.vrplayer
{
    public class OrientObjectAlongTowardPoint : MonoBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;

        [SerializeField] private GameObject targetPoint;
        [SerializeField] private LayerMask layerMask;
        [Range(0,1f)][SerializeField] private float transitionTime = .2f;


        private bool canRotate = false;
        private bool lastCanRotateState = false;
        private Collider _collider;
        private Quaternion originalRotation;

        private void Update()
        {
            if(canRotate) OrientObjectTowardPoint();
        }

        private void OnTriggerEnter(Collider collider)
        {
            if (((1 << collider.gameObject.layer) & layerMask) == 0) return;
            if(isDebug) Debug.Log($"collision enter with {collider.gameObject.name}");

            _collider = collider;
            originalRotation = _collider.gameObject.transform.localRotation;
            OrientObjectTowardPoint();
        }

        private void OnTriggerExit(Collider collider)
        {
            if (((1 << collider.gameObject.layer) & layerMask) == 0) return;
            if(isDebug) Debug.Log($"collision exit with {collider.gameObject.name}");
            
            canRotate = false;
            ResetOriginalObjectRotation();
        }


        private void OrientObjectTowardPoint()
        {
            if(!_collider) return;

            var direction = (targetPoint.transform.position - _collider.transform.position).normalized;
            var lookRotation = Quaternion.LookRotation(direction);

            if (!canRotate)
            {
                _collider.transform.DOLocalRotateQuaternion(lookRotation, transitionTime).OnComplete(delegate { canRotate = true; });
            }
            else
            {
                _collider.transform.rotation = lookRotation;
            }
        }

        private void ResetOriginalObjectRotation()
        {
            if(!_collider) return;
            _collider.transform.DOLocalRotateQuaternion(originalRotation, transitionTime);
        }
        
        
    }
}
