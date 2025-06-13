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
            FadeMask.FadeValue(_isHeadInWall);
            if(isDebug) Debug.Log($"isHeadInWall changed to: {_isHeadInWall}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0f, .75f);
            Gizmos.DrawSphere(transform.position, sphereCheckSize);
        }
#endif

        public static void SetIsLoadingState(bool value)
        {
            value = !value;
            _isSceneLoading = value;
            Debug.Log($"isSceneLoading: {_isSceneLoading}");
        }
    }
}