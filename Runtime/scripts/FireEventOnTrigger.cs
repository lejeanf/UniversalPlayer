using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;

namespace jeanf.universalplayer
{
    [RequireComponent(typeof(Collider))]
    public class FireEventOnTrigger : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;

        //[SerializeField] private bool useLayerMask = false;
        //[DrawIf("useLayerMask", true, ComparisonType.Equals)]
        [SerializeField] private LayerMask layerMask;
    
        [Space(10)]
        public UnityEvent colliderEnterEvent;
        public UnityEvent colliderExitEvent;
    

        private Collider _collider;

        private void Awake()
        {
            _collider = this.GetComponent<Collider>();
        }

        private void OnTriggerEnter(Collider collider)
        {
            //if (useLayerMask && ((1 << collider.gameObject.layer) & layerMask) == 0) return;
            if (((1 << collider.gameObject.layer) & layerMask) == 0) return;
        
            colliderEnterEvent?.Invoke();
            if(isDebug) Debug.Log($"collision enter with {collider.gameObject.name}");
        }

        private void OnTriggerExit(Collider collider)
        {
            //if (useLayerMask && ((1 << collider.gameObject.layer) & layerMask) == 0) return;
            if (((1 << collider.gameObject.layer) & layerMask) == 0) return;
        
            colliderExitEvent?.Invoke();
            if(isDebug) Debug.Log($"collision exit with {collider.gameObject.name}");
        }
    }
}


