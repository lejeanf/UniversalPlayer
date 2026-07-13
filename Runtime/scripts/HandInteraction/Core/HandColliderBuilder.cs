using System.Collections.Generic;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Replaces a hand's MeshCollider with one BoxCollider per FINGER BONE.
    /// A mesh collider on a skinned hand is frozen in bind pose — it never follows
    /// the fingers — while a box on each phalanx bone moves with the pose for free.
    /// Following human hand anatomy, boxes go on the phalanges only (Proximal,
    /// Intermediate, Distal — thumb: Proximal + Distal); metacarpals and carpals
    /// live inside the palm and get no collider.
    /// Runs automatically from BlendableHand.Awake (idempotent: bones that already
    /// carry a BoxCollider — e.g. baked into the prefab by the Pose Editor — are
    /// skipped). The Pose Editor uses PlanFingerBoxes to visualize and bake.
    /// </summary>
    public static class HandColliderBuilder
    {
        private const string LogPrefix = "[UniversalPlayer]";

        // Phalanx markers in the CC rig's bone names (L_IndexProximal, R_ThumbDistal, ...).
        private static readonly string[] PhalanxMarkers = { "Proximal", "Intermediate", "Distal" };

        /// <summary>A finger-bone box: local center/size on its bone transform.</summary>
        public readonly struct PlannedBox
        {
            public readonly Transform Bone;
            public readonly Vector3 Center;
            public readonly Vector3 Size;

            public PlannedBox(Transform bone, Vector3 center, Vector3 size)
            {
                Bone = bone;
                Center = center;
                Size = size;
            }
        }

        /// <summary>
        /// The boxes that WOULD be built for the rig under <paramref name="bonesRoot"/>:
        /// one per phalanx bone, spanning from the bone to its first child.
        /// </summary>
        public static List<PlannedBox> PlanFingerBoxes(Transform bonesRoot, float thickness = 0.016f)
        {
            var boxes = new List<PlannedBox>();
            if (bonesRoot == null) return boxes;

            foreach (var bone in bonesRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!IsPhalanx(bone.name) || bone.childCount == 0) continue;

                var toChild = bone.GetChild(0).localPosition;
                if (toChild.sqrMagnitude < 1e-10f) continue;

                // Thickness is meant in world units — bones of skinned rigs can carry
                // odd scales, so convert per axis.
                var lossy = bone.lossyScale;
                var size = new Vector3(
                    Mathf.Max(Mathf.Abs(toChild.x), SafeDivide(thickness, lossy.x)),
                    Mathf.Max(Mathf.Abs(toChild.y), SafeDivide(thickness, lossy.y)),
                    Mathf.Max(Mathf.Abs(toChild.z), SafeDivide(thickness, lossy.z)));
                boxes.Add(new PlannedBox(bone, toChild * 0.5f, size));
            }
            return boxes;
        }

        /// <summary>
        /// Removes every MeshCollider under <paramref name="handRoot"/> and builds a
        /// BoxCollider on each phalanx bone under <paramref name="bonesRoot"/>.
        /// Bones that already carry a BoxCollider are left untouched (idempotent).
        /// Returns the number of NEW boxes created.
        /// </summary>
        public static int ReplaceWithFingerBoxes(Transform handRoot, Transform bonesRoot, float thickness = 0.016f)
        {
            if (handRoot == null || bonesRoot == null) return 0;

            // Carry the old collider's interaction settings over to the boxes.
            var isTrigger = false;
            PhysicsMaterial material = null;
            foreach (var meshCollider in handRoot.GetComponentsInChildren<MeshCollider>(true))
            {
                isTrigger |= meshCollider.isTrigger;
                if (material == null) material = meshCollider.sharedMaterial;
                // Immediate on purpose: HandsPhysics caches GetComponentsInChildren<Collider>()
                // in Start — a deferred Destroy would leave a dead collider in that cache.
                Object.DestroyImmediate(meshCollider);
            }

            var planned = PlanFingerBoxes(bonesRoot, thickness);
            var built = 0;
            foreach (var plan in planned)
            {
                if (plan.Bone.GetComponent<BoxCollider>() != null) continue; // already baked

                var box = plan.Bone.gameObject.AddComponent<BoxCollider>();
                box.center = plan.Center;
                box.size = plan.Size;
                box.isTrigger = isTrigger;
                box.sharedMaterial = material;
                built++;
            }

            if (planned.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} HandColliderBuilder on '{handRoot.name}': no phalanx bones found under " +
                    $"'{bonesRoot.name}' (expected names containing Proximal/Intermediate/Distal, like the CC hand rig) — " +
                    "the hand now has NO collider at all. Rename the finger bones or add colliders manually.", handRoot);
            }
            return built;
        }

        private static bool IsPhalanx(string boneName)
        {
            foreach (var marker in PhalanxMarkers)
            {
                if (boneName.IndexOf(marker, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static float SafeDivide(float a, float b) => Mathf.Approximately(b, 0f) ? a : a / b;
    }
}
