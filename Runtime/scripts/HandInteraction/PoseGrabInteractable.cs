using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
{
    /// <summary>
    /// An <see cref="XRGrabInteractable"/> that seats the held object exactly where it was
    /// authored in the Pose Editor. The pose stores the object's placement in the grabbing
    /// hand's WRIST frame (<see cref="HandInfo.anchorLocalPosition"/> / anchorLocalRotation) —
    /// a shared skeleton point, so it reproduces the authored offset regardless of how the
    /// runtime hand is wired.
    ///
    /// Why a subclass and not a grab listener: in XRI the attach pose is captured BEFORE any
    /// public select event fires (XRGrabInteractable.OnSelectEntering initialises the dynamic
    /// attach, then calls base which raises the event). A listener therefore always runs too
    /// late. <see cref="InitializeDynamicAttachPose"/> is the supported pre-capture hook.
    /// </summary>
    public class PoseGrabInteractable : XRGrabInteractable
    {
        [Tooltip("Log the hand resolution + offset for each grab (diagnostics). Off by default.")]
        [SerializeField] private bool _logPoseGrab = false;

        [Tooltip("Only allow grabbing at contact (near). Rejects distance/ray (far) grabs from a Near-Far Interactor, regardless of the rig's far-casting setting.")]
        [SerializeField] private bool _contactGrabOnly = true;

        protected override void Awake()
        {
            base.Awake();
            // Required so InitializeDynamicAttachPose is called, and so XRI keeps the pose we
            // set instead of snapping the object to wherever the hand happened to touch it.
            useDynamicAttach = true;
            matchAttachPosition = false;
            matchAttachRotation = false;
        }

        // Contact-only grabbing: refuse a Near-Far Interactor that is reaching from a DISTANCE
        // (its attach controller carries a pull offset in the far region). Near/contact grabs
        // have no such offset and stay allowed. Guarantees the behaviour on the object itself,
        // independent of whether the player rig has far-casting enabled.
        public override bool IsSelectableBy(IXRSelectInteractor interactor)
        {
            if (!base.IsSelectableBy(interactor)) return false;
            if (_contactGrabOnly
                && interactor is NearFarInteractor nearFar
                && nearFar.interactionAttachController != null
                && nearFar.interactionAttachController.hasOffset)
            {
                // Only log at the moment of an actual select attempt (isSelected transitions),
                // not every hover frame, to avoid spam — approximate by logging when nothing
                // holds it yet.
                if (_logPoseGrab && !isSelected)
                    Debug.Log($"[PoseGrabInteractable] {name}: REJECTED far grab from '{interactor.transform.name}' (contact-only).", this);
                return false;
            }
            return true;
        }

        protected override void InitializeDynamicAttachPose(IXRSelectInteractor interactor, Transform dynamicAttachTransform)
        {
            base.InitializeDynamicAttachPose(interactor, dynamicAttachTransform);

            var pose = ResolveGrabPose();
            if (pose == null) { Log("no pose resolved on this object — grab uses default attach."); return; }

            var manager = ResolveGrabbingHand(interactor);
            if (manager == null) { Log($"could not resolve the grabbing HandPoseManager from interactor '{interactor.transform.name}' — grab uses default attach."); return; }

            var info = pose.GetHandInfo(manager.HandType);
            // No authored wrist offset (gesture pose, or a pose saved before the offset
            // existed) — leave XRI's default attach, exactly as before this component.
            if (info == null || !info.hasAnchorOffset) { Log($"pose '{pose.name}' has no anchor offset for {manager.HandType} hand — grab uses default attach."); return; }

            var wrist = manager.GetAnchorBone();
            if (wrist == null) { Log("grabbing hand has no wrist/anchor bone — grab uses default attach."); return; }

            // The object's world pose we want after the grab: the authored offset applied to
            // the runtime wrist.
            var desiredRotation = wrist.rotation * info.anchorLocalRotation;
            var desiredPosition = wrist.TransformPoint(info.anchorLocalPosition);

            // XRI moves the object so this attach transform aligns onto the interactor's
            // attach. We need the attach to be the FIXED grab point on the object that, when
            // the object sits at (desiredPosition, desiredRotation), coincides with the
            // interactor's attach. Rather than compute that local offset by hand — which a
            // heavily non-uniform object scale (here ~3.7 x 16.3 x 3.7) shears out of true —
            // momentarily place the object AT the desired pose, snap the attach onto the
            // interactor's attach (Unity derives the correct scale/shear-aware local pose),
            // then restore the object. The attach keeps that local pose and rides the object.
            var interactorAttach = interactor.GetAttachTransform(this);
            var savedPosition = transform.position;
            var savedRotation = transform.rotation;
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            dynamicAttachTransform.SetPositionAndRotation(interactorAttach.position, interactorAttach.rotation);
            transform.SetPositionAndRotation(savedPosition, savedRotation);

            Log($"applied offset for {manager.HandType} hand (pose '{pose.name}'): wrist='{wrist.name}', " +
                $"desiredPos={desiredPosition}, desiredRot={desiredRotation.eulerAngles}, " +
                $"interactorAttach.pos={interactorAttach.position}.");
        }

        // Close the grabbing hand around the object AND claim a pose-hold, so the grip pose
        // isn't reopened by ControllerHandPoseDriver / PointingPoseManager while held. Done
        // here (not only in HandPoseManager, which reacts to its own targetInteractor) so the
        // fingers close no matter which interactor grabbed — including a Near-Far/ray grab.
        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            var pose = ResolveGrabPose();
            if (pose == null) return;
            var manager = ResolveGrabbingHand(args.interactorObject);
            if (manager == null) return;
            manager.AcquirePoseHold();
            manager.ApplyPose(pose);
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            // Mirror OnSelectEntered exactly so the pose-hold refcount stays balanced: only
            // release/reopen when we would have acquired (a resolvable pose + hand).
            if (ResolveGrabPose() == null) return;
            var manager = ResolveGrabbingHand(args.interactorObject);
            if (manager == null) return;
            manager.ReleasePoseHold();
            manager.ApplyDefaultPose();
        }

        private void Log(string message)
        {
            if (_logPoseGrab) Debug.Log($"[PoseGrabInteractable] {name}: {message}", this);
        }

        // Same resolution order as HandPoseManager.ResolveGrabPose: the legacy PoseContainer
        // wins if present, otherwise the PickableObject's Hand Pose (what the Pose Editor
        // auto-links).
        private Pose ResolveGrabPose()
        {
            if (TryGetComponent(out PoseContainer container) && container.pose != null)
                return container.pose;
            if (TryGetComponent(out PickableObject pickable) && pickable.HandPose != null)
                return pickable.HandPose;
            return null;
        }

        // Find the hand doing the grabbing — deterministically, so the SAME hand is always
        // resolved for the same controller (an ambiguous resolve is what made one hand or the
        // other fail at random between grabs).
        private HandPoseManager ResolveGrabbingHand(IXRSelectInteractor interactor)
        {
            var found = FindObjectsByType<HandPoseManager>(FindObjectsInactive.Exclude);

            // This rig has TWO HandPoseManagers per side: an invisible CC_*Hand_Controller
            // (tracking skeleton — what the Pose Editor authors against) and a visible physics
            // hand that chases it (the mesh the player actually sees). Both share the grabbing
            // interactor, so pick the one that is actually VISIBLE — posing the invisible hand
            // does nothing on screen. Fall back to the full set if no hand carries a mesh.
            var visibleHands = System.Array.FindAll(found, HasVisibleMesh);
            var all = visibleHands.Length > 0 ? visibleHands : found;

            if (_logPoseGrab)
            {
                var sb = new System.Text.StringBuilder();
                sb.Append($"resolve: interactor='{interactor.transform.name}' handedness={interactor.handedness}; ALL {found.Length} hand(s): ");
                foreach (var c in found)
                {
                    var rend = c.GetComponentInChildren<SkinnedMeshRenderer>(true);
                    var visible = rend != null && rend.enabled && rend.gameObject.activeInHierarchy;
                    var w = c.GetAnchorBone();
                    sb.Append($"[{c.name} {c.HandType} physics={(c.GetComponentInParent<HandsPhysics>() != null)} visibleMesh={visible} wrist={(w != null ? w.name : "null")}@{(w != null ? w.position.ToString("F2") : "-")}] ");
                }
                Log(sb.ToString());
            }

            // 1) Exact: the hand that listens to this very interactor.
            foreach (var candidate in all)
            {
                if (ReferenceEquals(candidate.targetInteractor, interactor))
                { Log($"resolved '{candidate.name}' ({candidate.HandType}) via EXACT targetInteractor."); return candidate; }
            }

            // 2) Same controller: the grab often comes from a different interactor than the
            // hand's own (a Near-Far Interactor vs the hand's Direct interactor), but both sit
            // under the same controller. Pick the hand whose own interactor shares the DEEPEST
            // hierarchy ancestor with the grabbing interactor — that's this controller's hand.
            HandPoseManager best = null;
            var bestDepth = -1;
            foreach (var candidate in all)
            {
                if (candidate.targetInteractor == null) continue;
                var depth = SharedAncestorDepth(interactor.transform, candidate.targetInteractor.transform);
                if (depth > bestDepth) { bestDepth = depth; best = candidate; }
            }
            if (best != null)
            { Log($"resolved '{best.name}' ({best.HandType}) via DEEPEST-ANCESTOR (depth={bestDepth})."); return best; }

            // 3) Last resort: match the interactor's configured handedness to the hand's side.
            var handedness = interactor.handedness;
            if (handedness != InteractorHandedness.None)
            {
                var wantType = handedness == InteractorHandedness.Left ? HandType.Left : HandType.Right;
                foreach (var candidate in all)
                {
                    if (candidate.HandType == wantType)
                    { Log($"resolved '{candidate.name}' ({candidate.HandType}) via HANDEDNESS."); return candidate; }
                }
            }
            Log("resolved NOTHING — no matching hand.");
            return null;
        }

        // True when this hand actually shows a mesh on screen (an enabled, active skinned
        // renderer somewhere in its hierarchy) — i.e. the hand the player sees, the one worth
        // posing. The invisible tracking hand returns false.
        private static bool HasVisibleMesh(HandPoseManager hand)
        {
            foreach (var renderer in hand.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    return true;
            }
            return false;
        }

        // Depth (distance from the scene root) of the deepest transform that is an ancestor of
        // BOTH a and b. Higher = the two live closer together in the hierarchy (same controller).
        private static int SharedAncestorDepth(Transform a, Transform b)
        {
            var ancestorsOfA = new System.Collections.Generic.HashSet<Transform>();
            for (var t = a; t != null; t = t.parent) ancestorsOfA.Add(t);
            for (var t = b; t != null; t = t.parent)
            {
                if (!ancestorsOfA.Contains(t)) continue;
                var depth = 0;
                for (var p = t; p != null; p = p.parent) depth++;
                return depth;
            }
            return -1;
        }
    }
}
