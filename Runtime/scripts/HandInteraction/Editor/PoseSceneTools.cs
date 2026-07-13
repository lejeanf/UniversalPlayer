#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Scene-side pose math for the Pose Editor: finger chains, closest-surface
    /// targets and a damped CCD solver — powers auto-posing a hand around an object
    /// and the fingertip IK targets.
    /// </summary>
    public static class PoseSceneTools
    {
        public enum FingerKind { Unknown, Thumb, Index, Middle, Ring, Little }

        public sealed class FingerChain
        {
            public List<Transform> Joints; // root..tip inclusive
            public Transform Tip => Joints[Joints.Count - 1];
            public FingerKind Kind;
        }

        public static List<FingerChain> Chains(BaseHand hand)
        {
            var chains = new List<FingerChain>();
            if (hand == null || hand.FingerRoots == null) return chains;
            foreach (var root in hand.FingerRoots)
            {
                if (root == null) continue;
                var joints = new List<Transform>();
                for (var joint = root; joint != null; joint = joint.childCount > 0 ? joint.GetChild(0) : null)
                    joints.Add(joint);
                if (joints.Count > 1) chains.Add(new FingerChain { Joints = joints, Kind = Classify(root.name) });
            }
            return chains;
        }

        private static FingerKind Classify(string boneName)
        {
            var lower = boneName.ToLowerInvariant();
            if (lower.Contains("thumb")) return FingerKind.Thumb;
            if (lower.Contains("index")) return FingerKind.Index;
            if (lower.Contains("middle")) return FingerKind.Middle;
            if (lower.Contains("ring")) return FingerKind.Ring;
            if (lower.Contains("little") || lower.Contains("pinky") || lower.Contains("pinkie")) return FingerKind.Little;
            return FingerKind.Unknown;
        }

        /// <summary>
        /// The hand's anatomical frame for curling: the KNUCKLE LINE (index base →
        /// little base) is the shared hinge axis of the four fingers, and Interior
        /// (knuckle centroid pulled toward the wrist) is where fingertips head in a
        /// fist — the probe target that disambiguates the curl direction per rig.
        /// </summary>
        public struct PalmFrame
        {
            public bool Valid;
            public Vector3 KnuckleAxis; // sign-agnostic; the per-finger probe picks the direction
            public Vector3 Center;      // centroid of the non-thumb finger bases
            public Vector3 Interior;    // curl probe target, inside the palm
            public Vector3 Normal;      // palm-facing normal — the adduction (sideways-swing) axis
        }

        public static PalmFrame ComputePalmFrame(BaseHand hand)
        {
            var frame = new PalmFrame();
            Vector3 first = default, last = default;
            var center = Vector3.zero;
            var count = 0;
            foreach (var chain in Chains(hand))
            {
                if (chain.Kind == FingerKind.Thumb) continue;
                var basePosition = chain.Joints[0].position;
                if (count == 0) first = basePosition;
                last = basePosition;
                center += basePosition;
                count++;
            }
            if (count < 2 || hand == null) return frame; // no knuckle line to speak of

            frame.Center = center / count;
            frame.KnuckleAxis = (last - first).normalized;
            frame.Interior = Vector3.Lerp(frame.Center, hand.transform.position, 0.5f);
            // Palm normal: perpendicular to the knuckle line and the wrist-ward
            // direction. Rotating a finger base around it swings the tip sideways.
            frame.Normal = Vector3.Cross(frame.KnuckleAxis, frame.Interior - frame.Center).normalized;
            frame.Valid = frame.KnuckleAxis.sqrMagnitude > 0.5f;
            return frame;
        }

        // Natural per-joint flexion range (MCP, PIP, DIP degrees). A closed fist bends
        // the base knuckle (MCP) LESS than the middle one (PIP) — the PIP carries the
        // curl — so the base is deliberately the smallest of the three. The thumb
        // bends less at each knuckle. Distal joints follow proximal at ~2:3 (the
        // shared FDP/FDS tendon coupling).
        private static readonly float[] FingerRom = { 72f, 105f, 70f };
        private static readonly float[] ThumbRom = { 40f, 45f, 55f };

        /// <summary>
        /// Curls a finger from its current pose toward the palm by <paramref name="amount"/>
        /// (0 = leave straight, 1 = full fist), as a planar hinge with per-joint ROM.
        /// Fingers hinge around the palm's KNUCKLE LINE; the thumb (whose axis is
        /// oblique to it) derives its own from finger direction × toward-palm.
        /// <paramref name="flexibility"/> stretches the range (loose joints reach a
        /// tighter fist). Rotation only.
        /// </summary>
        public static void CurlFinger(FingerChain chain, float amount, float flexibility, PalmFrame palm)
        {
            var joints = chain.Joints;
            if (!palm.Valid || joints.Count < 2) return;
            var baseJoint = joints[0];
            var fingerDir = chain.Tip.position - baseJoint.position;
            if (fingerDir.sqrMagnitude < 1e-10f) return;

            Vector3 bendAxis;
            if (chain.Kind == FingerKind.Thumb)
            {
                // The thumb folds ACROSS the palm — for it, base→palm-center is a
                // genuine transverse direction (its base sits far off the centroid).
                bendAxis = Vector3.Cross(fingerDir, palm.Center - baseJoint.position);
            }
            else
            {
                bendAxis = palm.KnuckleAxis;
            }
            if (bendAxis.sqrMagnitude < 1e-10f) return;
            bendAxis.Normalize();

            // Sign that curls the tip toward the palm interior (rig handedness varies).
            var probe = baseJoint.position + Quaternion.AngleAxis(5f, bendAxis) * fingerDir;
            var sign = (probe - palm.Interior).sqrMagnitude < (chain.Tip.position - palm.Interior).sqrMagnitude ? 1f : -1f;

            var rom = chain.Kind == FingerKind.Thumb ? ThumbRom : FingerRom;
            var reach = Mathf.Lerp(0.85f, 1.1f, Mathf.Clamp01(flexibility)); // looser joints curl further
            var distalIndex = joints.Count - 2; // the DIP — last hinge before the tip
            for (var j = 0; j < joints.Count - 1; j++)
            {
                // The fingertip joint only hooks in the FINAL approach to a tight fist:
                // a tuck (~0.8) leaves the tip straight, a full fist (1.0) curls it.
                var jointAmount = amount;
                if (j == distalIndex && joints.Count >= 3)
                    jointAmount = amount * Mathf.InverseLerp(0.85f, 1f, amount);

                var romIndex = Mathf.Min(j, rom.Length - 1);
                var angle = jointAmount * rom[romIndex] * reach * sign;
                joints[j].rotation = Quaternion.AngleAxis(angle, bendAxis) * joints[j].rotation;
            }
        }

        /// <summary>
        /// Adducts a finger — swings it sideways in the palm plane — so its tip leans
        /// toward <paramref name="towardPoint"/> (a folded neighbour's base). This is
        /// the sideways lean the pure-flexion curl can't produce.
        /// </summary>
        public static void AdductFingerToward(FingerChain chain, Vector3 towardPoint, float degrees, PalmFrame palm)
        {
            if (!palm.Valid || chain.Joints.Count < 2) return;
            var baseJoint = chain.Joints[0];
            var tip = chain.Tip.position;
            var axis = palm.Normal;
            if (axis.sqrMagnitude < 1e-10f) return;

            var probe = baseJoint.position + Quaternion.AngleAxis(3f, axis) * (tip - baseJoint.position);
            var sign = (probe - towardPoint).sqrMagnitude < (tip - towardPoint).sqrMagnitude ? 1f : -1f;
            baseJoint.rotation = Quaternion.AngleAxis(degrees * sign, axis) * baseJoint.rotation;
        }

        public static bool ClosestPointOnObject(GameObject target, Vector3 from, out Vector3 closest)
        {
            closest = from;
            var bestSqr = float.MaxValue;
            var found = false;
            foreach (var collider in target.GetComponentsInChildren<Collider>())
            {
                if (collider == null || !collider.enabled) continue;
                Vector3 point;
                try { point = collider.ClosestPoint(from); }
                catch (System.Exception) { continue; } // non-convex mesh colliders refuse ClosestPoint
                var sqr = (point - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    closest = point;
                    found = true;
                }
            }
            return found;
        }

        // Anatomical hinge limits (interior bend angle per knuckle, degrees):
        // fingers flex up to ~110° per joint and hyperextend only a few degrees.
        private const float FlexionLimitDegrees = 110f;
        private const float HyperextensionLimitDegrees = 12f;

        /// <summary>
        /// Damped CCD constrained the way finger rigs are constrained in industry
        /// solvers: a finger is a PLANAR HINGE CHAIN. Every joint rotates only
        /// around the finger's shared bend-plane normal (twist and sideways splay
        /// are impossible by construction) and each knuckle's interior angle is
        /// clamped to the anatomical range — full flexion one way, a few degrees of
        /// hyperextension the other. Only rotates; bone lengths never change.
        /// </summary>
        public static void SolveChainToTarget(FingerChain chain, Vector3 target, int iterations = 8, float damping = 0.6f)
        {
            var joints = chain.Joints;
            var tip = chain.Tip;

            // The finger's bend plane: accumulated from the current curl; a straight
            // finger takes the plane through root, tip and target instead.
            var normal = Vector3.zero;
            for (var j = 0; j + 2 < joints.Count; j++)
            {
                normal += Vector3.Cross(joints[j + 1].position - joints[j].position,
                    joints[j + 2].position - joints[j + 1].position);
            }
            var planeDefinedByCurl = normal.sqrMagnitude > 1e-10f;
            if (!planeDefinedByCurl)
                normal = Vector3.Cross(tip.position - joints[0].position, target - joints[0].position);
            if (normal.sqrMagnitude < 1e-10f) return; // straight finger aiming along itself — nothing to do
            normal.Normalize();

            // Which way this finger curls (positive or negative around the normal):
            // read from its current bend. A perfectly straight finger hasn't decided
            // yet, so both directions stay open until the first bend.
            var curl = 0f;
            for (var j = 1; j + 1 < joints.Count; j++)
            {
                curl += Vector3.SignedAngle(joints[j].position - joints[j - 1].position,
                    joints[j + 1].position - joints[j].position, normal);
            }
            float minAngle, maxAngle;
            if (Mathf.Abs(curl) < 1f)
            {
                minAngle = -FlexionLimitDegrees;
                maxAngle = FlexionLimitDegrees;
            }
            else if (curl > 0f)
            {
                minAngle = -HyperextensionLimitDegrees;
                maxAngle = FlexionLimitDegrees;
            }
            else
            {
                minAngle = -FlexionLimitDegrees;
                maxAngle = HyperextensionLimitDegrees;
            }

            for (var i = 0; i < iterations; i++)
            {
                for (var j = joints.Count - 2; j >= 0; j--)
                {
                    var joint = joints[j];
                    var toTip = tip.position - joint.position;
                    var toTarget = target - joint.position;
                    if (toTip.sqrMagnitude < 1e-8f || toTarget.sqrMagnitude < 1e-8f) continue;

                    // Hinge step: only the rotation component around the bend-plane
                    // normal — the finger stays planar, so it can never corkscrew.
                    var step = Vector3.SignedAngle(toTip, toTarget, normal) * damping;

                    // Clamp the knuckle's resulting interior angle to anatomy. The
                    // chain root measures against its parent bone (the palm).
                    var parentReference = j > 0 ? joints[j - 1].position
                        : joint.parent != null ? joint.parent.position : joint.position;
                    var parentBone = joint.position - parentReference;
                    var bone = joints[j + 1].position - joint.position;
                    if (parentBone.sqrMagnitude > 1e-10f && bone.sqrMagnitude > 1e-10f)
                    {
                        var interior = Vector3.SignedAngle(parentBone, bone, normal);
                        step = Mathf.Clamp(interior + step, minAngle, maxAngle) - interior;
                    }

                    joint.rotation = Quaternion.AngleAxis(step, normal) * joint.rotation;
                }
                if ((tip.position - target).sqrMagnitude < 1e-6f) break;
            }
        }

        // Each phalanx is treated as a capsule of this radius for contact tests
        // (matches HandColliderBuilder's runtime box thickness).
        private const float FingerRadius = 0.008f;
        private const float CurlStepDegrees = 4f;

        /// <summary>
        /// Auto-pose the way procedural grasp generators do it: every finger CURLS
        /// in small hinge steps (planar, flexion-limited — same constraints as the
        /// IK) until any of its phalanges contacts the object surface or an
        /// already-posed finger. Whole-phalanx capsule tests mean middle segments
        /// can't sink into the object, and finger-vs-finger tests keep neighbours
        /// from overlapping. The object needs colliders.
        /// </summary>
        public static void AutoPose(BaseHand hand, GameObject target, float surfaceOffset = 0.006f, float flexibility = 0.5f)
        {
            if (target == null || target.GetComponentsInChildren<Collider>().Length == 0)
            {
                Debug.LogWarning($"Auto-pose: '{(target == null ? "<none>" : target.name)}' has no colliders — " +
                    "nothing to wrap the fingers onto. Add a collider to the preview object.");
                return;
            }

            var palmFrame = ComputePalmFrame(hand);
            var posedSegments = new List<(Vector3 a, Vector3 b)>();
            var previousCurl = 0f;
            foreach (var chain in Chains(hand))
            {
                // Adjacent-finger synergy: a curled neighbour drags this finger a
                // little before its own collision curl, scaled by flexibility (the
                // user's "drags the next finger"). The THUMB is excluded from the
                // chain of drags — it moves on its own musculature, independent of
                // the fingers — so it neither receives nor passes the drag along.
                if (chain.Kind != FingerKind.Thumb && previousCurl > 0f)
                    CurlFinger(chain, previousCurl * flexibility * 0.35f, flexibility, palmFrame);

                var curled = CurlFingerOntoObject(chain, target, surfaceOffset, posedSegments);
                previousCurl = chain.Kind == FingerKind.Thumb ? 0f : curled;
                for (var j = 0; j + 1 < chain.Joints.Count; j++)
                    posedSegments.Add((chain.Joints[j].position, chain.Joints[j + 1].position));
            }
        }

        // Returns the fraction of full flexion this finger ended up curling (0..1),
        // so the next finger can be dragged along by the adjacency synergy.
        private static float CurlFingerOntoObject(FingerChain chain, GameObject target, float surfaceOffset,
            List<(Vector3 a, Vector3 b)> otherFingers)
        {
            var joints = chain.Joints;
            var totalStepped = 0f;

            // Out of reach entirely? Leave the finger as authored instead of balling it up.
            var chainLength = 0f;
            for (var j = 0; j + 1 < joints.Count; j++)
                chainLength += (joints[j + 1].position - joints[j].position).magnitude;
            if (!ClosestPointOnObject(target, joints[0].position, out var closestToRoot)) return 0f;
            if ((closestToRoot - joints[0].position).magnitude > chainLength + surfaceOffset + FingerRadius) return 0f;

            var normal = BendPlaneNormal(chain, target);
            if (normal == Vector3.zero) return 0f;
            var sign = CurlSign(chain, normal, target);
            var contactDistance = surfaceOffset + FingerRadius;

            // Full flexion for this many joints, for normalizing the returned fraction.
            var maxPossible = (joints.Count - 1) * FlexionLimitDegrees;

            for (var j = 0; j < joints.Count - 1; j++)
            {
                for (var guard = 0; guard < 60; guard++)
                {
                    // Anatomical flexion limit at this knuckle (same as the IK solver).
                    var parentReference = j > 0 ? joints[j - 1].position
                        : joints[j].parent != null ? joints[j].parent.position : joints[j].position;
                    var parentBone = joints[j].position - parentReference;
                    var bone = joints[j + 1].position - joints[j].position;
                    if (parentBone.sqrMagnitude > 1e-10f
                        && Vector3.SignedAngle(parentBone, bone, normal) * sign >= FlexionLimitDegrees) break;

                    joints[j].rotation = Quaternion.AngleAxis(CurlStepDegrees * sign, normal) * joints[j].rotation;
                    totalStepped += CurlStepDegrees;

                    var clearance = FingerClearance(joints, j, target, otherFingers);
                    if (clearance < contactDistance)
                    {
                        // Overshot INTO the surface / another finger: back the step off.
                        if (clearance < FingerRadius)
                        {
                            joints[j].rotation = Quaternion.AngleAxis(-CurlStepDegrees * sign, normal) * joints[j].rotation;
                            totalStepped -= CurlStepDegrees;
                        }
                        break;
                    }
                }
            }
            return maxPossible > 0f ? Mathf.Clamp01(totalStepped / maxPossible) : 0f;
        }

        // Smallest distance from the phalanges at/below the moved joint to the object
        // surface and to the already-posed fingers (their radius pre-subtracted, so
        // one threshold serves both).
        private static float FingerClearance(List<Transform> joints, int fromJoint, GameObject target,
            List<(Vector3 a, Vector3 b)> otherFingers)
        {
            var clearance = float.MaxValue;
            for (var j = fromJoint; j + 1 < joints.Count; j++)
            {
                var start = joints[j].position;
                var end = joints[j + 1].position;
                for (var s = 0; s <= 2; s++)
                {
                    var sample = Vector3.Lerp(start, end, s / 2f);
                    // A point INSIDE a collider comes back as itself — distance 0 reads as penetration.
                    if (ClosestPointOnObject(target, sample, out var closest))
                        clearance = Mathf.Min(clearance, (sample - closest).magnitude);
                }
                foreach (var (a, b) in otherFingers)
                    clearance = Mathf.Min(clearance, SegmentSegmentDistance(start, end, a, b) - FingerRadius);
            }
            return clearance;
        }

        private static Vector3 BendPlaneNormal(FingerChain chain, GameObject target)
        {
            var joints = chain.Joints;
            var normal = Vector3.zero;
            for (var j = 0; j + 2 < joints.Count; j++)
            {
                normal += Vector3.Cross(joints[j + 1].position - joints[j].position,
                    joints[j + 2].position - joints[j + 1].position);
            }
            if (normal.sqrMagnitude < 1e-10f && ClosestPointOnObject(target, chain.Tip.position, out var closest))
                normal = Vector3.Cross(chain.Tip.position - joints[0].position, closest - joints[0].position);
            return normal.sqrMagnitude < 1e-10f ? Vector3.zero : normal.normalized;
        }

        // Which rotation direction around the plane normal curls TOWARD the object:
        // read from the finger's existing bend, or probed with a trial step when straight.
        private static float CurlSign(FingerChain chain, Vector3 normal, GameObject target)
        {
            var joints = chain.Joints;
            var curl = 0f;
            for (var j = 1; j + 1 < joints.Count; j++)
            {
                curl += Vector3.SignedAngle(joints[j].position - joints[j - 1].position,
                    joints[j + 1].position - joints[j].position, normal);
            }
            if (Mathf.Abs(curl) > 1f) return Mathf.Sign(curl);

            var root = joints[0];
            var restRotation = root.rotation;
            root.rotation = Quaternion.AngleAxis(CurlStepDegrees, normal) * restRotation;
            var positiveDistance = TipDistance(chain, target);
            root.rotation = Quaternion.AngleAxis(-CurlStepDegrees, normal) * restRotation;
            var negativeDistance = TipDistance(chain, target);
            root.rotation = restRotation;
            return positiveDistance <= negativeDistance ? 1f : -1f;
        }

        private static float TipDistance(FingerChain chain, GameObject target)
        {
            return ClosestPointOnObject(target, chain.Tip.position, out var closest)
                ? (chain.Tip.position - closest).magnitude
                : float.MaxValue;
        }

        // Closest distance between two segments (Ericson, Real-Time Collision Detection).
        private static float SegmentSegmentDistance(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            var a = Vector3.Dot(d1, d1);
            var e = Vector3.Dot(d2, d2);
            var f = Vector3.Dot(d2, r);
            float s, t;

            if (a <= 1e-10f && e <= 1e-10f) return r.magnitude;
            if (a <= 1e-10f)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                var c = Vector3.Dot(d1, r);
                if (e <= 1e-10f)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    var b = Vector3.Dot(d1, d2);
                    var denominator = a * e - b * b;
                    s = denominator > 1e-10f ? Mathf.Clamp01((b * f - c * e) / denominator) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }
            return (p1 + d1 * s - (p2 + d2 * t)).magnitude;
        }
    }
}
#endif
