using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace jeanf.universalplayer
{
    public class HandPoseManager : BaseHand
    {
        // The interactor we react to
        public UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor targetInteractor = null;

        private bool wasInitialized = false;

        public UnityEvent grabAction;
        public UnityEvent ungrabAction;

        // Pose ownership: while anyone holds (a SetPoseOnTrigger zone, the primary
        // item, ...) or an object is selected, ControllerHandPoseDriver stays out of
        // the way. Refcounted so overlapping zones compose.
        private int poseHoldCount;
        public bool IsPoseHeld => poseHoldCount > 0;
        public bool IsSelecting => targetInteractor != null && targetInteractor.hasSelection;
        public void AcquirePoseHold() => poseHoldCount++;
        public void ReleasePoseHold() => poseHoldCount = Mathf.Max(0, poseHoldCount - 1);

        private void OnEnable()
        {
           //Debug.Log(this.gameObject.name + " start " + targetInteractor.name);
            Init();

        }

        

        private void Init()
        {
            // Subscribe to selected events
            //getDirectInteractor?.Invoke(handType,ref targetInteractor);
            if(isDebug) Debug.Log($"targetInteractor : {targetInteractor.name}");
            if (!targetInteractor)
                return;

            //targetInteractor.onSelectEntered.AddListener(TryApplyObjectPose);
            //targetInteractor.onSelectExited.AddListener(TryApplyDefaultPose);
            targetInteractor.selectEntered.AddListener(TryApplyObjectPose);
            targetInteractor.selectExited.AddListener(TryApplyDefaultPose);

            wasInitialized = true;
        }

        private void OnDisable()
        {
            if (!wasInitialized) return;
            if (!targetInteractor) return;
            targetInteractor.selectEntered.RemoveListener(TryApplyObjectPose);
            targetInteractor.selectExited.RemoveListener(TryApplyDefaultPose);
        }

        private void TryApplyObjectPose(SelectEnterEventArgs args)
        {
            var interactable = args.interactableObject as UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
            if (interactable == null) return;

            // The grab pose can live on either component: a PoseContainer (legacy) or a
            // PickableObject/SnapObject's Hand Pose (what the Pose Editor auto-links). Read
            // whichever is present so grabbing wraps the fingers around the object.
            var pose = ResolveGrabPose(interactable);
            if (pose == null) return;

            grabAction.Invoke();
            if (isDebug) Debug.Log($"Pose name : {pose.name}");
            ApplyPose(pose);
        }

        private void TryApplyDefaultPose(SelectExitEventArgs args)
        {
            var interactable = args.interactableObject as UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
            if (interactable == null) return;

            // Only reopen the hand for objects that actually posed it.
            if (ResolveGrabPose(interactable) == null) return;
            ungrabAction.Invoke();
            ApplyDefaultPose();
        }

        private static Pose ResolveGrabPose(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable)
        {
            if (interactable.TryGetComponent(out PoseContainer poseContainer) && poseContainer.pose != null)
                return poseContainer.pose;
            if (interactable.TryGetComponent(out PickableObject pickable) && pickable.HandPose != null)
                return pickable.HandPose;
            return null;
        }

        public override void ApplyOffset(Vector3 position, Quaternion rotation)
        {
            if(isDebug) Debug.Log($"ApplyOffset to: [{position}], [{rotation}]");
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
                targetInteractor = GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>();
            }
        }

        public void SetXRDirectInteractor(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor xrBaseInteractor)
        {
            targetInteractor = xrBaseInteractor;
            Init();
        }
    }
}