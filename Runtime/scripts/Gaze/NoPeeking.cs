using jeanf.EventSystem;
using UnityEngine;

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
        private void Update()
        {
            
            if (Physics.CheckSphere(transform.position, sphereCheckSize, collisionLayer, QueryTriggerInteraction.Ignore))
            {
                isHeadInWall = true;
            }
            else
            {
                isHeadInWall = false;
            }
            
            FadeMask.FadeValue(isHeadInWall);
            if(isDebug) Debug.Log($"isHeadInWall: {isHeadInWall}");
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0f, .75f);
            Gizmos.DrawSphere(transform.position, sphereCheckSize);
        }
    }
}