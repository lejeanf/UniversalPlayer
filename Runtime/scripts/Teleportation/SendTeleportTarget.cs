using jeanf.EventSystem;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jeanf.vrplayer
{
    public class SendTeleportTarget : MonoBehaviour
    {
        [SerializeField] private bool sendEventOnEnable = false;
        [SerializeField] private bool alignWithRotation = false;
        [Header("Broadcasting on:")]
        public bool isTeleportPlayer = false;

        [HideInInspector] public TeleportEventChannelSO _teleportObjectEventChannel;
        [HideInInspector] public TeleportEventChannelSO _teleportPLayerEventChannel;
        [HideInInspector] public Transform objectToTeleport;
        
        private void OnEnable()
        {
            if (sendEventOnEnable) Teleport();
        }

        public void Teleport() 
        {
            if (isTeleportPlayer)
            {
                var teleportInformation = new TeleportInformation(objectToTeleport, this.transform, alignWithRotation, isTeleportPlayer);
                _teleportObjectEventChannel.RaiseEvent(teleportInformation);
            }
            else
            {
                var teleportInformation = new TeleportInformation(null, this.transform, alignWithRotation, isTeleportPlayer);
                _teleportPLayerEventChannel.RaiseEvent(teleportInformation);
            }
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
#if UNITY_EDITOR
    [CustomEditor(typeof(SendTeleportTarget))]
    public class RandomScript_Editor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector(); // for other non-HideInInspector fields
 
            SendTeleportTarget script = (SendTeleportTarget)target;
 
            // draw checkbox for the bool
            if (script.isTeleportPlayer) // if bool is true, show other fields
            {
                script._teleportPLayerEventChannel = EditorGUILayout.ObjectField("Teleport Player Channel", script._teleportPLayerEventChannel, typeof(TeleportEventChannelSO), true) as TeleportEventChannelSO;
            }
            else
            {
                script._teleportObjectEventChannel = EditorGUILayout.ObjectField("Teleport Object Channel", script._teleportObjectEventChannel, typeof(TeleportEventChannelSO), true) as TeleportEventChannelSO;
                script.objectToTeleport = EditorGUILayout.ObjectField("Object to teleport", script.objectToTeleport, typeof(Transform), true) as Transform;
            }
        }
    }
#endif
}
