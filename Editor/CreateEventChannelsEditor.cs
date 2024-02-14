using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace jeanf.vrplayer
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
