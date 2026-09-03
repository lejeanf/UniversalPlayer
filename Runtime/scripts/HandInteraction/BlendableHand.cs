using UnityEngine;

namespace jeanf.universalplayer
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public class BlendableHand : MonoBehaviour
    {
        public delegate void UpdateBlendableHandList(SkinnedMeshRenderer hand);
        public static UpdateBlendableHandList AddHand;
        public static UpdateBlendableHandList RemoveHand;
    
        private SkinnedMeshRenderer _hand;

        private void Awake()
        {
            _hand = GetComponent<SkinnedMeshRenderer>() == null ? GetComponentInChildren<SkinnedMeshRenderer>() : GetComponent<SkinnedMeshRenderer>();

            // The authored MeshCollider stays frozen in bind pose (skinned meshes never
            // move their collider) — swap it for per-phalanx boxes that follow the fingers.
            // Only for a PHYSICS hand (one riding a Rigidbody, e.g. Left/RightHandPhysics):
            // the non-physical "ghost" hand HandsPhysics shows when the physics hand is held
            // back by the world must stay collider-free, or it pushes objects through walls
            // and blocks the very hand it mirrors.
            if (Application.isPlaying && _hand != null && GetComponentInParent<Rigidbody>() != null)
                HandColliderBuilder.ReplaceWithFingerBoxes(transform, _hand.rootBone != null ? _hand.rootBone : transform);
        }

        private void OnEnable()
        {
            AddHand?.Invoke(_hand);
        }

        private void OnDisable()
        {
            RemoveHand?.Invoke(_hand);
        }
    }
}

