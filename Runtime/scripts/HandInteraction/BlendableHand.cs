using UnityEngine;

namespace jeanf.vrplayer
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

