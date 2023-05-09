using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HandInfo
{
    public Transform attatchTransform;
    //public Vector3 attachPosition = Vector3.zero;
    //public Quaternion attachRotation = Quaternion.identity;
    public List<Quaternion> fingerRotations = new List<Quaternion>();

    public static HandInfo Empty => new HandInfo();

    public void Save(PreviewHand hand)
    {
        // Save position and rotation
        attachPosition = hand.transform.localPosition;
        attachRotation = hand.transform.localRotation;
        attatchTransform = hand.

        // Save rotations from the hand's current joints
        fingerRotations = hand.GetJointRotations();
    }
}