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
        
        private static bool _isSceneLoading = true;
        private static bool _wasSceneLoadingLastFrame = true;
        
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
                if (Physics.CheckSphere(transform.position, sphereCheckSize, collisionLayer, QueryTriggerInteraction.Ignore))
                {
                    _fadeState = true;
                }
                else
                {
                    _fadeState = false;
                }
            }

            if (wasLoadingLastFrame && !_isSceneLoading)
            {
                if (isDebug) Debug.Log("Scene loading finished - preparing head-in-wall detection mode");
                FadeMask.PrepareVolumeProfile(FadeMask.FadeType.HeadInWall);
            }

            if (_fadeState == _fadeStateLastValue) return;
            
            FadeMask.FadeValue(_fadeState, FadeMask.FadeType.HeadInWall);
            if (isDebug) Debug.Log($"HeadInWall collision fade changed to: {_fadeState}");
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