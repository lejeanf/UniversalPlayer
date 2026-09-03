using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// CursorStateController inspector with a one-click guardrail: when the palette is
    /// missing or is the packaged one (immutable in consumer projects, overwritten by
    /// updates), a banner and a button offer to create a project-local copy in Assets/
    /// and assign it right here — scene instance, prefab mode or prefab asset alike.
    /// </summary>
    [CustomEditor(typeof(CursorStateController))]
    public class CursorStateControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var cursor = (CursorStateController)target;
            var palette = cursor.Palette;
            var packaged = CreateLocalCursorPalette.IsPackaged(palette);

            if (palette == null || packaged)
            {
                EditorGUILayout.HelpBox(palette == null
                        ? "No CursorPaletteSO assigned — cursor and interaction ray fall back to code defaults and the project cannot restyle them."
                        : $"The cursor uses the PACKAGED '{palette.name}' — that asset cannot be edited in consumer projects and package updates overwrite it.",
                    MessageType.Warning);

                if (GUILayout.Button("Fix: create a local Cursor Palette in Assets/ and assign it"))
                {
                    var copy = CreateLocalCursorPalette.CreateLocalCopy();
                    if (copy != null) CreateLocalCursorPalette.AssignTo(cursor, copy);
                }
                EditorGUILayout.Space(4f);
            }

            DrawDefaultInspector();
        }
    }
}
