using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Serialization;

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

        [FormerlySerializedAs("_isHeadInWall")] [SerializeField] private bool fadeState = false;
        private bool _isHeadInWallLastValue = false;
        
        private static bool _isSceneLoading = true;
        private static bool _wasSceneLoadingLastFrame = true;
        
        private void FixedUpdate()
        {
            _isHeadInWallLastValue = fadeState;
            _wasSceneLoadingLastFrame = _isSceneLoading;
            
            if (_isSceneLoading)
            {
                fadeState = true;
            }
            else
            {
                if (isDebug) Debug.Log("NoPeeking - made it through the return");
                if (Physics.CheckSphere(transform.position, sphereCheckSize, collisionLayer, QueryTriggerInteraction.Ignore))
                {
                    fadeState = true;
                }
                else
                {
                    fadeState = false;
                }
            }

            // Check if loading state changed from true to false
            if (_wasSceneLoadingLastFrame && !_isSceneLoading)
            {
                // Scene loading just finished - ensure we set up the correct fade type for head-in-wall detection
                if (isDebug) Debug.Log("Scene loading finished - setting up head-in-wall detection mode");
                
                // Force a fade update with the correct type, even if the value hasn't changed
                FadeMask.FadeValue(fadeState, FadeMask.FadeType.HeadInWall);
                if (isDebug) Debug.Log($"Forced HeadInWall setup with value: {fadeState}");
                return; // Exit early to avoid duplicate fade calls
            }

            // Normal state change detection
            if (fadeState == _isHeadInWallLastValue) return;
            
            // Determine fade type based on whether we're loading or detecting collision
            if (_isSceneLoading)
            {
                // Black fade for loading
                FadeMask.FadeValue(fadeState, FadeMask.FadeType.Loading);
                if (isDebug) Debug.Log($"Loading fade changed to: {fadeState}");
            }
            else
            {
                // Gray fade for head-in-wall collision
                FadeMask.FadeValue(fadeState, FadeMask.FadeType.HeadInWall);
                if (isDebug) Debug.Log($"HeadInWall collision fade changed to: {fadeState}");
            }
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
        
        public bool IsHeadInWall()
        {
            return fadeState;
        }
    }
}