using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace jeanf.vrplayer
{
    public class SendTeleportTarget : MonoBehaviour
    {
        public delegate void TeleportPlayer(Transform teleportTarget, bool isRotateCamera);
        public static event TeleportPlayer teleportPlayer;

        [SerializeField] private bool sendEventOnEnable = false;
        [SerializeField] private bool isRotateCamera = false;

        private void OnEnable()
        {
            if (sendEventOnEnable) Teleport();
        }

        public void Teleport() 
        {
            if (isRotateCamera) MouseLook.ResetCamera?.Invoke();
            teleportPlayer?.Invoke(this.transform, isRotateCamera);
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
