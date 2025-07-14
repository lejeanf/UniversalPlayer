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

        [SerializeField] private bool _isHeadInWall = false;
        private bool _isHeadInWallLastValue = false;
        
        private static bool _isSceneLoading = true;
        
        private void FixedUpdate()
        {
            _isHeadInWallLastValue = _isHeadInWall;
            
            if (_isSceneLoading)
            {
                _isHeadInWall = true;
            }
            else
            {
                if (isDebug) Debug.Log("NoPeeking - made it through the return");
                if (Physics.CheckSphere(transform.position, sphereCheckSize, collisionLayer, QueryTriggerInteraction.Ignore))
                {
                    _isHeadInWall = true;
                }
                else
                {
                    _isHeadInWall = false;
                }
            }

            if (_isHeadInWall == _isHeadInWallLastValue) return;
            
            // Determine fade type based on whether we're loading or detecting collision
            if (_isSceneLoading)
            {
                // Black fade for loading
                FadeMask.FadeValue(_isHeadInWall, FadeMask.FadeType.Loading);
                if (isDebug) Debug.Log($"Loading fade changed to: {_isHeadInWall}");
            }
            else
            {
                // Gray fade for head-in-wall collision
                FadeMask.FadeValue(_isHeadInWall, FadeMask.FadeType.HeadInWall);
                if (isDebug) Debug.Log($"HeadInWall collision fade changed to: {_isHeadInWall}");
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
        
        /// <summary>
        /// Gets the current loading state
        /// </summary>
        public static bool IsCurrentlyLoading()
        {
            return _isSceneLoading;
        }
        
        /// <summary>
        /// Gets the current head-in-wall state (useful for debugging)
        /// </summary>
        public bool IsHeadInWall()
        {
            return _isHeadInWall;
        }
    }
}