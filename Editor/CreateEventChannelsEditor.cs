using jeanf.validationTools;
using UnityEngine;
using UnityEditor;

namespace jeanf.universalplayer
{
    [CustomEditor(typeof(PlayerInputEventManager))]
    public class CreateEventChannelsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ValidationUi.DrawIssuesBanner(target as Component); // keep the shared orange "needs setup" banner this custom editor would otherwise replace
            DrawDefaultInspector();

            PlayerInputEventManager playerInputEventManager = (PlayerInputEventManager)target;
            if (GUILayout.Button("Create Event Channels"))
            {
                playerInputEventManager.CreateEventChannels();
            }
        }
    }
}
