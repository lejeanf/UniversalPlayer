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
    public Pose DefaultPose => defaultPose;

    // Serialized so it can be used in editor by the preview hand
    [SerializeField] protected List<Transform> fingerRoots = new List<Transform>();

    // What kind of hand is this?
    [SerializeField] protected HandType handType = HandType.None;
    public HandType HandType => handType;

    public List<Transform> Joints { get; protected set; } = new List<Transform>();

    /// <summary>The serialized finger chain roots (used by the pose editor's scene tools).</summary>
    public IReadOnlyList<Transform> FingerRoots => fingerRoots;

    private bool _warnedNoBoneNames;

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

    public List<string> GetJointNames()
    {
        List<string> names = new List<string>();

        foreach (Transform joint in Joints)
            names.Add(joint != null ? joint.name : null);

        return names;
    }

    /// <summary>
    /// The hand's anchor bone (the wrist): the deepest transform that is a common
    /// ancestor of every finger root. It is a SHARED skeleton point — the same bone
    /// exists identically in the pose editor's preview hand and the runtime hand — so a
    /// held-item offset stored relative to it transfers exactly, regardless of how each
    /// rig is otherwise structured (physics wrappers, mesh offsets, wiring).
    /// </summary>
    public Transform GetAnchorBone()
    {
        Transform anchor = null;
        foreach (var root in fingerRoots)
        {
            if (root == null) continue;
            anchor = anchor == null ? root : DeepestCommonAncestor(anchor, root);
        }
        return anchor != null ? anchor : transform;
    }

    private static Transform DeepestCommonAncestor(Transform a, Transform b)
    {
        var ancestors = new HashSet<Transform>();
        for (var t = a; t != null; t = t.parent) ancestors.Add(t);
        for (var t = b; t != null; t = t.parent) if (ancestors.Contains(t)) return t;
        return null;
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
        if (handInfo == null) return;
        ApplyHandInfo(handInfo, false);
    }
    public void ApplyPoseForSetup(Pose pose)
    {
        if (pose == null) return;
        // Get the proper info using hand's type
        HandInfo handInfo = pose.GetHandInfo(handType);
        if (handInfo == null) return;

        // Apply rotations — INSTANTLY: setup is the editor path, and tools that
        // read or save the joints right after (live save, gesture presets) must
        // not race a 0.2s tween still moving toward the pose.
        ApplyHandInfo(handInfo, true);

        // Position, and rotate, this differs on the type of hand
        ApplyOffset(handInfo.attachPosition, handInfo.attachRotation);
    }

    /// <summary>
    /// Applies a pose's finger rotations, mapping each rotation to the right joint by
    /// BONE NAME when the pose carries names — order-independent, so a hand whose
    /// fingerRoots list is ordered differently from the authoring hand still poses
    /// correctly. Falls back to the old index mapping for poses saved before names.
    /// </summary>
    public void ApplyHandInfo(HandInfo info, bool instant)
    {
        if (info == null) return;
        var rotations = info.fingerRotations;
        var names = info.jointNames;

        if (names != null && names.Count == rotations.Count && names.Count > 0)
        {
            foreach (Transform joint in Joints)
            {
                if (joint == null) continue;
                var index = names.IndexOf(joint.name);
                if (index < 0) continue; // a joint this pose doesn't carry — leave it be
                SetJointRotation(joint, rotations[index], instant);
            }
            return;
        }

        // No names => the pose predates name-mapping. It maps by list index, which lands
        // on the wrong fingers whenever this hand's fingerRoots order differs from the
        // hand it was authored on. Warn only on the RUNTIME path (!instant) — the editor
        // setup path applies on the preview hand it was authored on, where index is fine,
        // and the migration itself runs through here (warning there is just noise).
        if (!instant && !_warnedNoBoneNames)
        {
            _warnedNoBoneNames = true;
            Debug.LogWarning($"[UniversalPlayer] Hand '{name}' ({handType}) applied a pose with NO bone names — it maps by " +
                "index and can scramble the fingers. Run Tools > UniversalPlayer > Migrate Poses (add bone names) once in " +
                "Edit Mode, then re-test.", this);
        }
        ApplyFingerRotations(rotations, instant);
    }

    public void ApplyFingerRotations(List<Quaternion> rotations, bool instant = false)
    {
        // Make sure we have the rotations for all the joints
        if (!HasProperCount(rotations))
        {
            Debug.LogWarning($"[UniversalPlayer.XR] Pose not applied on '{name}' ({handType}): it stores {rotations?.Count ?? 0} " +
                $"joint rotations but this hand has {Joints.Count} joints. The pose was saved against a different hand rig — " +
                "open it in Tools/UniversalPlayer/Pose Editor and re-save it.", this);
            return;
        }
        // Set the local rotation of each joint (index-mapped: legacy path)
        for (var i = 0; i < Joints.Count; i++)
        {
            if (Joints[i] != null) SetJointRotation(Joints[i], rotations[i], instant);
        }
    }

    private void SetJointRotation(Transform joint, Quaternion to, bool instant)
    {
        if (instant || !isLerpingOverTime)
        {
            joint.localRotation = to;
            return;
        }
        var from = joint.localRotation;
        LMotion.Create(from, to, lerpTime).Bind(x => joint.localRotation = x);
    }

    private bool HasProperCount(List<Quaternion> rotations)
    {
        return Joints.Count == rotations.Count;
    }

    public abstract void ApplyOffset(Vector3 position, Quaternion rotation);
}
