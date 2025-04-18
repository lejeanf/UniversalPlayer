using UnityEngine;
using UnityEditor;

namespace jeanf.universalplayer
{
    [CustomEditor(typeof(PlayerInputEventManager))]
    public class CreateEventChannelsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlayerInputEventManager playerInputEventManager = (PlayerInputEventManager)target;
            if (GUILayout.Button("Create Event Channels"))
            {
                playerInputEventManager.CreateEventChannels();
            }
        }
    }
}
