using System;
using System.Collections.Generic;
using System.Linq;
using jeanf.EventSystem;
using UnityEngine;
using LitMotion;

using jeanf.propertyDrawer;
using UnityEngine.Serialization;

public abstract class BaseHand : MonoBehaviour, IDebugBehaviour
{
    public bool isDebug
    { 
        get => _isDebug;
        set => _isDebug = value; 
    }
    [SerializeField] private bool _isDebug = false;
    
    // Neutral pose for the hand
    [SerializeField] protected Pose defaultPose = null;

    // Serialized so it can be used in editor by the preview hand
    [SerializeField] protected List<Transform> fingerRoots = new List<Transform>();

    // What kind of hand is this?
    [SerializeField] protected HandType handType = HandType.None;
    public HandType HandType => handType;

    public List<Transform> Joints { get; protected set; } = new List<Transform>();

    [FormerlySerializedAs("isLerpingOverTipe")] public bool isLerpingOverTime = true;
    [DrawIf("isLerpingOverTipe", true, ComparisonType.Equals)]
    [SerializeField] private float lerpTime = .2f;

    protected virtual void Awake()
    {
        Joints = CollectJoints();
    }

    protected List<Transform> CollectJoints()
    {
        List<Transform> joints = new List<Transform>();

        foreach (Transform root in fingerRoots)
            joints.AddRange(root.GetComponentsInChildren<Transform>());

        return joints;
    }

    public List<Quaternion> GetJointRotations()
    {
        List<Quaternion> rotations = new List<Quaternion>();

        foreach (Transform joint in Joints)
            rotations.Add(joint.localRotation);

        return rotations;
    }

    public void ApplyDefaultPose()
    {
        ApplyPose(defaultPose);
    }
    public void ApplyDefaultPoseForeSetup()
    {
        ApplyPoseForSetup(defaultPose);
    }

    public void ApplyPose(Pose pose)
    {
        if (pose == null) return;
        //Pose name
        if(isDebug) Debug.Log($"pose.name : {pose.name}");
        
        // Get the proper info using hand's type
        HandInfo handInfo = pose.GetHandInfo(handType);

        // Apply rotations 
        if (handInfo == null) return;
        ApplyFingerRotations(handInfo.fingerRotations);

        // Position, and rotate, this differs on the type of hand
        //ApplyOffset(handInfo.attachPosition, handInfo.attachRotation);
    }
    public void ApplyPoseForSetup(Pose pose)
    {
        if (pose == null) return;
        // Get the proper info using hand's type
        HandInfo handInfo = pose.GetHandInfo(handType);

        // Apply rotations 
        if (handInfo == null) return;
        ApplyFingerRotations(handInfo.fingerRotations);

        // Position, and rotate, this differs on the type of hand
        ApplyOffset(handInfo.attachPosition, handInfo.attachRotation);
    }

    public void ApplyFingerRotations(List<Quaternion> rotations)
    {
        // Make sure we have the rotations for all the joints
        if (!HasProperCount(rotations)) return;
        // Set the local rotation of each joint

        foreach (var (value, i) in Joints.Select((value, i) => ( value, i )))
        {
            if (!isLerpingOverTime)
            {
                Joints[i].localRotation = rotations[i];
            }
            else
            { 
                var from = value.localRotation;
                var to = rotations[i];
                LMotion.Create(from, to, lerpTime).Bind(x => Joints[i].localRotation = x);
            }
        }
    }

    private bool HasProperCount(List<Quaternion> rotations)
    {
        return Joints.Count == rotations.Count;
    }

    public abstract void ApplyOffset(Vector3 position, Quaternion rotation);
}
