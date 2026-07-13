using UnityEngine;

[SelectionBase]
[ExecuteInEditMode]
public class PreviewHand : BaseHand
{
    /// <summary>
    /// Makes THIS hand the mirror image of the source across the given plane —
    /// root position, orientation and every finger joint get the same reflection.
    /// The caller picks the plane (object center, between the hands, ...).
    /// </summary>
    public void MirrorAndApplyPose(PreviewHand sourceHand, Vector3 planePoint, Vector3 planeNormal)
    {
        if (planeNormal.sqrMagnitude < 1e-10f) return;
        var normal = planeNormal.normalized;
        var sourcePosition = sourceHand.transform.position;

        var mirroredPosition = Reflect(sourcePosition - planePoint, normal) + planePoint;
        transform.SetPositionAndRotation(mirroredPosition, MirrorRotation(sourceHand.transform.rotation, normal));

        // Fingers: the SAME reflection applied to every joint's world frame. One
        // geometric operation for root and fingers is self-consistent by
        // construction — the old per-component quaternion flips (y-only on joints,
        // y+z on the root) mixed two conventions and twisted thumbs/fingers.
        // Parents precede children in the Joints list, so each absolute assignment
        // overwrites what the parent's change propagated.
        var sourceJoints = sourceHand.Joints;
        if (sourceJoints == null || Joints == null || sourceJoints.Count != Joints.Count)
        {
            Debug.LogWarning($"[UniversalPlayer] Mirror on '{name}': joint count differs from '{sourceHand.name}' " +
                $"({Joints?.Count ?? 0} vs {sourceJoints?.Count ?? 0}) — fingers not mirrored. Are both preview hands the same rig?", this);
            return;
        }
        for (var i = 0; i < Joints.Count; i++)
        {
            if (Joints[i] == null || sourceJoints[i] == null) continue;
            Joints[i].rotation = MirrorRotation(sourceJoints[i].rotation, normal);
        }
    }

    // The mirror image of a rotation: reflect its forward and up axes across the
    // plane, then rebuild a proper rotation — the handedness flip this bakes in is
    // exactly what the opposite hand's mirrored rig expects.
    private static Quaternion MirrorRotation(Quaternion rotation, Vector3 normal)
    {
        var forward = Reflect(rotation * Vector3.forward, normal);
        var up = Reflect(rotation * Vector3.up, normal);
        return Quaternion.LookRotation(forward, up);
    }

    private static Vector3 Reflect(Vector3 vector, Vector3 normal)
    {
        return vector - 2f * Vector3.Dot(vector, normal) * normal;
    }

    public override void ApplyOffset(Vector3 position, Quaternion rotation)
    {
        transform.localPosition = position;
        transform.localRotation = rotation;
    }
}
