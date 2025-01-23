using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Serialization;

namespace jeanf.vrplayer
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

        private bool isHeadInWall = false;
        private bool isHeadInWallLastValue = false;
        
        [SerializeField] private bool isSceneLoading = false;
        private void FixedUpdate()
        {
            if (isSceneLoading)
            {
                isHeadInWall = true;
            }
            else
            {
                if (isDebug) Debug.Log("NoPeeking - made it through the return");
                if (Physics.CheckSphere(transform.position, sphereCheckSize, collisionLayer, QueryTriggerInteraction.Ignore))
                {
                    isHeadInWall = true;
                }
                else
                {
                    isHeadInWall = false;
                }
            }
            Debug.Log("FADE - FixedUpdate No Peeking");
            
            FadeMask.FadeValue(isHeadInWall);
            if(isDebug) Debug.Log($"isHeadInWall: {isHeadInWall}");
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0f, .75f);
            Gizmos.DrawSphere(transform.position, sphereCheckSize);
        }
        #endif

        public void SetCanFadeOutValue(bool value)
        {
            isSceneLoading = value;
            if (isDebug) Debug.Log($"SetCanFadeOutValue - isSceneLoading: {isSceneLoading}");
        }
    }
}