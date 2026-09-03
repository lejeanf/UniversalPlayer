using jeanf.validationTools;
using UnityEngine;
using UnityEditor;

namespace jeanf.universalplayer
{
    [CustomEditor(typeof(PlayerActionManager))]
    public class CreatePlayerActionsEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            ValidationUi.DrawIssuesBanner(target as Component); // keep the shared orange "needs setup" banner this custom editor would otherwise replace
            DrawDefaultInspector();

            PlayerActionManager playerActionManager = (PlayerActionManager)target;
            if (GUILayout.Button("Create Player Actions"))
            {
                playerActionManager.CreatePlayerActions();  
            }
        }
    }

}
