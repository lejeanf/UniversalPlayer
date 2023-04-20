using jeanf.EventSystem;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jeanf.vrplayer
{
    public class SendTeleportTarget : MonoBehaviour, IDebugBehaviour
    {
        public bool isDebug
        { 
            get => _isDebug;
            set => _isDebug = value; 
        }
        [SerializeField] private bool _isDebug = false;

        [SerializeField] private bool sendEventOnEnable = false;
        [SerializeField] private bool alignWithRotation = false;
        public bool isTeleportPlayer = false;
        
        [Header("Broadcasting on (player):")] 
        [SerializeField] private TeleportEventChannelSO _teleportPLayerEventChannel;
        [Header("Broadcasting on (object):")] 
        [SerializeField] private TeleportEventChannelSO _teleportObjectEventChannel;
        [SerializeField] private Transform objectToTeleport;

        public Transform ObjectToTeleport
        {
            get => objectToTeleport;
            set => objectToTeleport = value;
        }

        private void OnEnable()
        {
            if (sendEventOnEnable) Teleport();
        }

        public void Teleport()
        {
            var teleportInformation = new TeleportInformation(objectToTeleport, this.transform, alignWithRotation, isTeleportPlayer);
            if (isTeleportPlayer) _teleportPLayerEventChannel.RaiseEvent(teleportInformation);
            else  _teleportObjectEventChannel.RaiseEvent(teleportInformation);
            
            if(_isDebug) Debug.Log($"sending teleport information");
        }

        #if UNITY_EDITOR
        [DrawGizmo(GizmoType.Pickable)]
        private void OnDrawGizmos()
        {
            if (!this.transform) return;

            var thisTransform = transform.position;
            Gizmos.color = new Color(.2f, .6f, .2f, .5f);
            Gizmos.DrawSphere(thisTransform, .25f);
            Gizmos.color = new Color(.2f, .6f, .2f, 1f);
            Gizmos.DrawWireSphere(thisTransform, .5f);

            // Blue Arrow for teleport direction
            var a = transform.position + transform.rotation * new Vector3(0, 0, .5f); // tip of the arrow
            var b = transform.position + transform.rotation * new Vector3(-.05f, 0, .45f); // bottom left of the arrow
            var c = transform.position + transform.rotation * new Vector3(.05f, 0, .45f); // bottom right of the arrow
            Gizmos.color = new Color(0, 0, 1, 1f); // using blue color (same as the axis)
            Gizmos.DrawLine(thisTransform, a);
            Gizmos.DrawLine(c, b);
            Gizmos.DrawLine(c, a);
            Gizmos.DrawLine(b, a);
        }
        #endif
    }
}

