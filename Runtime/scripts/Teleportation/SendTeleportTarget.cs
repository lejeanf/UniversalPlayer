using jeanf.EventSystem;
using UnityEngine;
using UnityEngine.Events;
#if UNITY_EDITOR
using System;
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
        
        [Header("Broadcasting on:")] 
        [SerializeField] private TeleportEventChannelSO _teleportChannel;
        
        [Header("Teleportation parameters:")] 
        public bool isTeleportPlayer = false;
        [DrawIf("isTeleportPlayer", false, ComparisonType.Equals, DisablingType.DontDraw)]
        [SerializeField] public Transform objectToTeleport;
        [SerializeField] public bool isUsingFilter = true;
        [DrawIf("isUsingFilter", true, ComparisonType.Equals, DisablingType.DontDraw)]
        public FilterSO _filter;
        [SerializeField] private bool sendEventOnEnable = false;
        
        public Transform ObjectToTeleport
        {
            get => objectToTeleport;
            set
            {
                if(_isDebug) Debug.Log($"ObjectToTeleport name : {value.gameObject.name}");
                objectToTeleport = value;
            } 
        }

        private void OnEnable()
        {
            if (sendEventOnEnable) Teleport();
        }

        public void Teleport()
        {
            var teleportInformation = new TeleportInformation(objectToTeleport, this.transform, isTeleportPlayer, _filter, isUsingFilter);
            if(_isDebug) Debug.Log($"[teleportInformation ({gameObject.name})] " +
                                   $"objectToTeleport : {teleportInformation.objectToTeleport}, " +
                                   $"targetDestination : {teleportInformation.targetDestination}, " +
                                   $"isTeleportPlayer : {teleportInformation.objectIsPlayer}, " +
                                   $"_filter : {teleportInformation.filter.filters[0]}, " +
                                   $"isUsingFilter : {teleportInformation.isUsingFilter}");
            _teleportChannel.RaiseEvent(teleportInformation);
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

