using System.Collections.Generic;
using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
{
    public enum HandPoseSource
    {
        ControllerDriver = 0,
        Pointing = 1,
        TriggerZone = 2,
        PrimaryItem = 3,
        Grab = 4
    }

    public class HandPoseManager : BaseHand
    {
        public XRBaseInteractor targetInteractor = null;

        private bool wasInitialized = false;

        public UnityEvent grabAction;
        public UnityEvent ungrabAction;

        private struct PoseClaim
        {
            public object Owner;
            public HandPoseSource Source;
            public Pose Pose;
        }

        private readonly List<PoseClaim> poseClaims = new List<PoseClaim>();

        public bool IsPoseHeld => poseClaims.Count > 0;
        public bool IsSelecting => targetInteractor != null && targetInteractor.hasSelection;
        public HandPoseSource? ActivePoseSource => poseClaims.Count > 0 ? poseClaims[poseClaims.Count - 1].Source : (HandPoseSource?)null;
        public bool HasPoseClaim(object owner) => IndexOfClaim(owner) >= 0;

        public bool CanApplyPose(HandPoseSource source) =>
            poseClaims.Count == 0 || source >= poseClaims[poseClaims.Count - 1].Source;

        public bool TryClaimPose(object owner, HandPoseSource source, Pose pose)
        {
            if (!CanApplyPose(source))
            {
                if (isDebug) Debug.Log($"{name}: {source} pose '{PoseName(pose)}' refused — {ActivePoseSource} owns this hand.", this);
                return false;
            }
            var existing = IndexOfClaim(owner);
            if (existing >= 0) poseClaims.RemoveAt(existing);
            poseClaims.Add(new PoseClaim { Owner = owner, Source = source, Pose = pose });
            ApplyClaimedPose(pose);
            return true;
        }

        public void ReleasePoseClaim(object owner)
        {
            var index = IndexOfClaim(owner);
            if (index < 0) return;
            var wasTop = index == poseClaims.Count - 1;
            poseClaims.RemoveAt(index);
            if (!wasTop) return;
            if (poseClaims.Count == 0) ApplyDefaultPose();
            else ApplyClaimedPose(poseClaims[poseClaims.Count - 1].Pose);
        }

        public void ReleaseHeldObjects()
        {
            if (targetInteractor == null || !targetInteractor.hasSelection) return;
            var manager = targetInteractor.interactionManager;
            if (manager == null) return;
            var held = new List<IXRSelectInteractable>(targetInteractor.interactablesSelected);
            foreach (var interactable in held) manager.SelectExit(targetInteractor, interactable);
        }

        private void ApplyClaimedPose(Pose pose)
        {
            if (pose != null) ApplyPose(pose);
            else ApplyDefaultPose();
        }

        private int IndexOfClaim(object owner)
        {
            for (var i = 0; i < poseClaims.Count; i++)
            {
                if (ReferenceEquals(poseClaims[i].Owner, owner)) return i;
            }
            return -1;
        }

        private static string PoseName(Pose pose) => pose != null ? pose.name : "none";

        private void OnEnable()
        {
            Init();
        }

        private void Init()
        {
            if (!targetInteractor)
            {
                if (isDebug) Debug.LogWarning($"{name}: no targetInteractor — grabbing will never pose this hand.", this);
                return;
            }

            if (isDebug) Debug.Log($"targetInteractor : {targetInteractor.name}");

            targetInteractor.selectEntered.AddListener(ClaimGrabPose);
            targetInteractor.selectExited.AddListener(ReleaseGrabPose);

            wasInitialized = true;
        }

        private void OnDisable()
        {
            if (!wasInitialized) return;
            if (!targetInteractor) return;
            targetInteractor.selectEntered.RemoveListener(ClaimGrabPose);
            targetInteractor.selectExited.RemoveListener(ReleaseGrabPose);
        }

        private void ClaimGrabPose(SelectEnterEventArgs args)
        {
            var interactable = args.interactableObject as XRBaseInteractable;
            if (interactable == null) return;

            var pose = ResolveGrabPose(interactable);
            if (pose == null) return;

            grabAction.Invoke();
            if (isDebug) Debug.Log($"Pose name : {pose.name}");
            TryClaimPose(interactable, HandPoseSource.Grab, pose);
        }

        private void ReleaseGrabPose(SelectExitEventArgs args)
        {
            var interactable = args.interactableObject as XRBaseInteractable;
            if (interactable == null) return;

            if (ResolveGrabPose(interactable) == null) return;
            ungrabAction.Invoke();
            ReleasePoseClaim(interactable);
        }

        private static Pose ResolveGrabPose(XRBaseInteractable interactable)
        {
            if (interactable.TryGetComponent(out PoseContainer poseContainer) && poseContainer.pose != null)
                return poseContainer.pose;
            if (interactable.TryGetComponent(out PickableObject pickable) && pickable.HandPose != null)
                return pickable.HandPose;
            return null;
        }

        public override void ApplyOffset(Vector3 position, Quaternion rotation) { }

        private void OnValidate()
        {
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
