using UnityEngine;
using UnityEditor;

namespace jeanf.universalplayer
{
    [CustomEditor(typeof(PlayerActionManager))]
    public class CreatePlayerActionsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PlayerActionManager playerActionManager = (PlayerActionManager)target;
            if (GUILayout.Button("Create Player Actions"))
            {
                playerActionManager.CreatePlayerActions();  
            }
        }
    }

}
