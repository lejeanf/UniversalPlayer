using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HandInfo
{
    public Vector3 attachPosition = Vector3.zero;
    public Quaternion attachRotation = Quaternion.identity;
    public List<Quaternion> fingerRotations = new List<Quaternion>();
    // Bone name for each entry in fingerRotations, in the same order. Lets the pose be
    // applied by NAME instead of by list index, so a hand whose fingerRoots are ordered
    // differently from the authoring (preview) hand still gets each rotation on the right
    // joint. Empty on poses saved before this existed — those fall back to index mapping.
    public List<string> jointNames = new List<string>();

    // The held object's pose expressed in the WRIST BONE's local frame (the wrist is a
    // shared skeleton point — see BaseHand.GetAnchorBone). Seating the item relative to
    // the runtime wrist with this offset places it exactly as authored, independent of
    // rig structure/wiring. Only meaningful when hasAnchorOffset is true (held-object
    // poses saved by the pose editor); older poses fall back to the hand-root offset.
    public bool hasAnchorOffset = false;
    public Vector3 anchorLocalPosition = Vector3.zero;
    public Quaternion anchorLocalRotation = Quaternion.identity;

    public static HandInfo Empty => new HandInfo();

    public void Save(PreviewHand hand, bool computeAnchorOffset = true)
    {
        // Save position and rotation
        attachPosition = hand.transform.localPosition;
        attachRotation = hand.transform.localRotation;

        // Save rotations AND the bone name they belong to, in lockstep.
        fingerRotations = hand.GetJointRotations();
        jointNames = hand.GetJointNames();

        // Wrist-relative offset of the held object. The pose editor parents the hand to
        // an anchor placed ON the object, so hand.transform.parent IS the object frame.
        // Only computed here (with that object context); the batch migration passes false
        // and leaves any existing value untouched.
        if (computeAnchorOffset)
        {
            var wrist = hand.GetAnchorBone();
            var objectFrame = hand.transform.parent;
            if (wrist != null && wrist != hand.transform && objectFrame != null)
            {
                anchorLocalPosition = wrist.InverseTransformPoint(objectFrame.position);
                anchorLocalRotation = Quaternion.Inverse(wrist.rotation) * objectFrame.rotation;
                hasAnchorOffset = true;
            }
            else hasAnchorOffset = false;
        }
    }
}