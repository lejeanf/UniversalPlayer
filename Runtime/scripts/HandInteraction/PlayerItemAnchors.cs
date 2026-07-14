using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The single place that answers "where does a held item go?" — the reason a
    /// <see cref="PickableObject"/> never needs a scene reference.
    ///
    /// Every anchor is resolved from the PLAYER RIG at runtime, so an item authored in
    /// one additively-loaded scene can dock to a player that lives in another. Nothing
    /// is serialized across a scene boundary.
    ///
    /// Resolution:
    ///  - XR: always the real tracked hand — the one that actually grabbed. (An item
    ///    authored as Camera-anchored still lands in the grabbing hand: docking to the
    ///    face is meaningless when you have hands.)
    ///  - M&amp;K / gamepad, Camera anchor: docked in front of the view (tablet-style).
    ///  - M&amp;K / gamepad, hand anchor + SteadyDock: a camera-relative anchor to that
    ///    side of the view. It rides the camera, so it is rock-steady on screen — use
    ///    it for anything whose UI must stay clickable.
    ///  - M&amp;K / gamepad, hand anchor + AnimatedBone: the first-person body's real hand
    ///    bone, so the item moves with the walk/idle animation. Requires FirstPersonBody
    ///    enabled with a humanoid rig; falls back to SteadyDock (with a warning) if not,
    ///    and PickableObject's editor flags it before you ever hit play.
    ///
    /// Lives on the Player rig. Missing dock transforms are created automatically, so a
    /// Player (variant) that predates this component still works with zero wiring.
    /// </summary>
    public class PlayerItemAnchors : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("The player camera. Left empty = Camera.main.")]
        [SerializeField] private Camera playerCamera;

        // A Camera-anchored item parents straight to the camera: the item's own
        // heldLocalPosition/Euler IS the dock pose (its z is the hold distance, which
        // the scroll wheel adjusts). Same "dock by numbers" idea PrimaryItemBehaviour
        // already uses for the tablet — no extra GameObject needed.
        //
        // The HAND docks are real transforms, because they answer a rig-level question
        // ("where is the right hand in view?") that every item then offsets FROM. The
        // rig owns where the hand is; the item owns how it sits in that hand.
        [Header("Desktop hand docks (auto-created under the camera when left empty)")]
        [SerializeField] private Transform rightHandDock;
        [SerializeField] private Transform leftHandDock;
        [SerializeField] private Vector3 rightHandDockLocalPosition = new Vector3(0.24f, -0.22f, 0.45f);
        [SerializeField] private Vector3 leftHandDockLocalPosition = new Vector3(-0.24f, -0.22f, 0.45f);

        [Header("VR hands (the real tracked hands)")]
        [SerializeField] private Transform vrRightHand;
        [SerializeField] private Transform vrLeftHand;

        [Header("First-person body — for the Animated Bone attach mode")]
        [Tooltip("Left empty = found in the scene. Supplies the real hand bones on M&K/gamepad.")]
        [SerializeField] private FirstPersonBody body;

        private HandPoseManager rightPose;
        private HandPoseManager leftPose;
        private bool poseManagersSearched;
        private bool boneFallbackWarned;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            if (body == null) body = FindFirstObjectByType<FirstPersonBody>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// The transform a held item should parent to, plus which hand it effectively
        /// ended up in (<see cref="HandType.None"/> for the camera dock — used to pick
        /// the right half of a hand pose).
        /// </summary>
        public Transform Resolve(PickableObject item, HandType grabbedWith, out HandType effectiveHand)
        {
            effectiveHand = HandType.None;
            if (item == null) return null;
            if (playerCamera == null) playerCamera = Camera.main;

            // VR: the hand that actually grabbed wins over whatever was authored.
            if (BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                var hand = grabbedWith != HandType.None ? grabbedWith : AnchorToHand(item.Anchor);
                if (hand == HandType.None) hand = HandType.Right; // a Camera-anchored item still lands in a hand
                effectiveHand = hand;
                var vrHand = hand == HandType.Left ? vrLeftHand : vrRightHand;
                if (vrHand != null) return vrHand;
                // No VR hand wired on this rig — better to dock to the view than to drop the item.
                return CameraTransform;
            }

            // Desktop.
            var anchor = item.Anchor;
            if (anchor == HeldAnchor.Camera) return CameraTransform;

            effectiveHand = AnchorToHand(anchor);

            if (item.AttachMode == HandAttachMode.AnimatedBone)
            {
                var bone = body != null ? body.GetHandBone(effectiveHand) : null;
                if (bone != null) return bone;
                if (!boneFallbackWarned)
                {
                    boneFallbackWarned = true;
                    Debug.LogWarning($"{LogPrefix} PlayerItemAnchors: '{item.name}' wants the Animated Bone attach mode, but the " +
                        "first-person body is disabled or is not a humanoid rig — docking it steadily to the view instead. " +
                        "Enable FirstPersonBody (Body Enabled) with a humanoid character to hold items in the real hand.", item);
                }
            }

            return EnsureHandDock(effectiveHand);
        }

        private static HandType AnchorToHand(HeldAnchor anchor) => anchor switch
        {
            HeldAnchor.RightHand => HandType.Right,
            HeldAnchor.LeftHand => HandType.Left,
            _ => HandType.None,
        };

        /// <summary>The camera an item docks to (also the frame the hand docks hang off).</summary>
        public Transform CameraTransform
        {
            get
            {
                if (playerCamera == null) playerCamera = Camera.main;
                return playerCamera != null ? playerCamera.transform : null;
            }
        }

        private Transform EnsureHandDock(HandType hand)
        {
            if (hand == HandType.Left)
            {
                if (leftHandDock == null) leftHandDock = CreateDock("LeftHandDock", leftHandDockLocalPosition);
                return leftHandDock;
            }
            if (rightHandDock == null) rightHandDock = CreateDock("RightHandDock", rightHandDockLocalPosition);
            return rightHandDock;
        }

        // The docks are children of the CAMERA on purpose: they ride the view, so a
        // docked item is steady in screen space (that is what keeps its UI clickable).
        private Transform CreateDock(string dockName, Vector3 localPosition)
        {
            var camera = CameraTransform;
            if (camera == null) return null;
            var dock = new GameObject(dockName).transform;
            dock.SetParent(camera, false);
            dock.localPosition = localPosition;
            dock.localRotation = Quaternion.identity;
            return dock;
        }

        /// <summary>
        /// The exact world pose an item WOULD have if it were taken right now — used by
        /// PickableObject's inspector to preview the held pose in edit mode, so the
        /// offsets can be tuned without entering play.
        ///
        /// Deliberately allocation-free: it never creates the dock GameObjects Resolve()
        /// would, because dirtying the scene just to draw a preview is unacceptable.
        /// </summary>
        public bool TryGetHeldPose(PickableObject item, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (item == null) return false;

            var camera = CameraTransform;
            if (camera == null) return false;

            var hand = AnchorToHand(item.Anchor);

            // Where the anchor sits, without instantiating it.
            Vector3 anchorPosition;
            Quaternion anchorRotation;
            var bone = hand != HandType.None && item.AttachMode == HandAttachMode.AnimatedBone && body != null
                ? body.GetHandBone(hand)
                : null;

            if (bone != null)
            {
                anchorPosition = bone.position;
                anchorRotation = bone.rotation;
            }
            else if (hand == HandType.None)
            {
                anchorPosition = camera.position;
                anchorRotation = camera.rotation;
            }
            else
            {
                var dock = hand == HandType.Left ? leftHandDock : rightHandDock;
                if (dock != null)
                {
                    anchorPosition = dock.position;
                    anchorRotation = dock.rotation;
                }
                else
                {
                    // The dock does not exist yet (it is created at runtime) — compute
                    // where it WOULD be, from the same serialized offset.
                    var local = hand == HandType.Left ? leftHandDockLocalPosition : rightHandDockLocalPosition;
                    anchorPosition = camera.TransformPoint(local);
                    anchorRotation = camera.rotation;
                }
            }

            item.GetHeldOffset(hand, out var localPosition, out var localRotation);
            position = anchorPosition + anchorRotation * localPosition;
            rotation = anchorRotation * localRotation;
            return true;
        }

        /// <summary>Wraps the hand around a held item (finger pose + attach offset come from the same Pose asset the primary item uses).</summary>
        public void ApplyHandPose(HandType hand, Pose pose)
        {
            if (pose == null || hand == HandType.None) return;
            var manager = GetPoseManager(hand);
            if (manager == null) return;
            manager.ApplyPose(pose);
            manager.AcquirePoseHold(); // hold it, or the controller pose driver opens the fingers again
        }

        /// <summary>Releases a pose acquired by <see cref="ApplyHandPose"/> and returns the hand to its default.</summary>
        public void ClearHandPose(HandType hand, Pose pose)
        {
            if (pose == null || hand == HandType.None) return;
            var manager = GetPoseManager(hand);
            if (manager == null) return;
            manager.ReleasePoseHold();
            manager.ApplyDefaultPose();
        }

        private HandPoseManager GetPoseManager(HandType hand)
        {
            if (!poseManagersSearched)
            {
                poseManagersSearched = true;
                foreach (var manager in FindObjectsByType<HandPoseManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (manager.HandType == HandType.Right) rightPose = manager;
                    else if (manager.HandType == HandType.Left) leftPose = manager;
                }
            }
            return hand == HandType.Left ? leftPose : rightPose;
        }
    }
}
