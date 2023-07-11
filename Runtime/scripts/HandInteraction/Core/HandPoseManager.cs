using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace  jeanf.vrplayer
{
    public class HandPoseManager : BaseHand, IDebugBehaviour
    {
        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;
        
        // The interactor we react to
        public XRBaseInteractor targetInteractor = null;

        private bool wasInitialized = false;

        public UnityEvent grabAction;
        public UnityEvent ungrabAction;

        private void Init()
        {
            // Subscribe to selected events
            //getDirectInteractor?.Invoke(handType,ref targetInteractor);
            if(_isDebug) Debug.Log($"targetInteractor : {targetInteractor.name}");
            if (!targetInteractor) return;
            targetInteractor.onSelectEntered.AddListener(TryApplyObjectPose);
            targetInteractor.onSelectExited.AddListener(TryApplyDefaultPose);

            wasInitialized = true;
        }

        private void OnDisable()
        {
            if (!wasInitialized) return;
            if (!targetInteractor) return;
            targetInteractor.onSelectEntered.RemoveListener(TryApplyObjectPose);
            targetInteractor.onSelectExited.RemoveListener(TryApplyDefaultPose);
        }

        private void TryApplyObjectPose(XRBaseInteractable interactable)
        {
            if(_isDebug) Debug.Log($"interactable : {interactable}");
            // Try and get pose container, and apply
            if (!interactable.TryGetComponent(out PoseContainer poseContainer)) return;
            grabAction.Invoke();
            if(_isDebug) Debug.Log($"Pose name : {poseContainer.pose.name}");
            //move AttachTransform
            //AplyPose
            ApplyPose(poseContainer.pose);
        }

        private void TryApplyDefaultPose(XRBaseInteractable interactable)
        {
            if(_isDebug) Debug.Log($"Default pose, interactable : {interactable}");
            // Try and get pose container, and apply
            if (interactable.TryGetComponent(out PoseContainer poseContainer))
            {
                ungrabAction.Invoke();  
                ApplyDefaultPose();
            }
        }

        public override void ApplyOffset(Vector3 position, Quaternion rotation)
        {
            if(_isDebug) Debug.Log($"ApplyOffset to: [{position}], [{rotation}]");
            /*
            // Invert since the we're moving the attach point instead of the hand
            Vector3 finalPosition = position * -1.0f;
            Quaternion finalRotation = Quaternion.Inverse(rotation);

            // Since it's a local position, we can just rotate around zero
            finalPosition = finalPosition.RotatePointAroundPivot(Vector3.zero, finalRotation.eulerAngles);

            // Set the position and rotach of attach
            targetInteractor.attachTransform.localPosition = finalPosition;
            targetInteractor.attachTransform.localRotation = finalRotation;
            */
        }

        private void OnValidate()
        {
            // Let's have this done automatically, but not hide the requirement
            if (!targetInteractor)
            {
                targetInteractor = GetComponentInParent<XRBaseInteractor>();
            }
        }

        public void SetXRDirectInteractor(XRBaseInteractor xrBaseInteractor)
        {
            targetInteractor = xrBaseInteractor;
            Init();
        }
    }
}