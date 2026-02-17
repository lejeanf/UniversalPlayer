using jeanf.EventSystem;
using UnityEngine;

namespace jeanf.universalplayer
{
    public class NoPeeking : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;
        
        [SerializeField] private LayerMask collisionLayer;
        [SerializeField] private float sphereCheckSize = .15f;

        [SerializeField] private bool _fadeState = false;
        private bool _fadeStateLastValue = false;
        private bool _wasSceneLoadingLastFrame;

        private static bool _isSceneLoading = true;
        
        private void FixedUpdate()
        {
            _fadeStateLastValue = _fadeState;
            bool wasLoadingLastFrame = _wasSceneLoadingLastFrame;
            _wasSceneLoadingLastFrame = _isSceneLoading;
    
            if (_isSceneLoading)
            {
                _fadeState = true;
            }
            else
            {
                if (isDebug) Debug.Log("NoPeeking - made it through the return");
                _fadeState = Physics.CheckSphere(transform.position, sphereCheckSize, collisionLayer, QueryTriggerInteraction.Ignore);
            }

            if (_fadeState == _fadeStateLastValue) return;

            var fadeType = _isSceneLoading ? FadeMask.FadeType.Loading : FadeMask.FadeType.HeadInWall;
            FadeMask.FadeValue(_fadeState, fadeType);

            if (isDebug) Debug.Log($"fadeType:{fadeType} fade changed to: {_fadeState}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0f, .75f);
            Gizmos.DrawSphere(transform.position, sphereCheckSize);
        }
#endif

        public static void SetIsLoadingState(bool isLoading)
        {
            _isSceneLoading = isLoading;
        }
        public static bool IsCurrentlyLoading()
        {
            return _isSceneLoading;
        }
        public bool GetFadeState()
        {
            return _fadeState;
        }
    }
}