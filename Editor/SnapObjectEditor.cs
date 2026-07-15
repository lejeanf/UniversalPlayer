using UnityEditor;
using UnityEngine;
using jeanf.universalplayer;

namespace jeanf.universalplayer.editor
{
    /// <summary>
    /// Inspector for <see cref="SnapObject"/>. It inherits everything
    /// <see cref="PickableObjectEditor"/> offers (held-pose preview, Rigidbody /
    /// animated-bone checks) and adds one thing: when the object has no Hand Pose it
    /// draws an ORANGE validation banner (the same attention colour the rest of the
    /// package uses) with a specific message. A SnapObject is authored to be carried
    /// with a grip, so a missing pose is a real setup problem — SnapObject.IsValid
    /// (IValidatable) already flags it in the hierarchy and console; this makes the
    /// inspector say why.
    /// </summary>
    [CustomEditor(typeof(SnapObject))]
    public class SnapObjectEditor : PickableObjectEditor
    {
        // Matches jeanf.validationTools.ValidationUi (that assembly isn't referenced by
        // this editor asmdef, so the shared attention colours are mirrored here).
        private static readonly Color Orange = new Color(1f, 0.6f, 0.1f);
        private static readonly Color OrangeWash = new Color(1f, 0.6f, 0.1f, 0.14f);
        private static GUIStyle _title;
        private static GUIStyle _body;

        public override void OnInspectorGUI()
        {
            var snap = target as SnapObject;
            if (snap != null && snap.HandPose == null) DrawMissingPoseBanner();
            base.OnInspectorGUI();
        }

        private static void DrawMissingPoseBanner()
        {
            if (_title == null)
            {
                _title = new GUIStyle(EditorStyles.boldLabel);
                _title.normal.textColor = Orange;
                _body = new GUIStyle(EditorStyles.label) { wordWrap = true };
                _body.normal.textColor = Orange;
            }

            var area = EditorGUILayout.BeginVertical();
            EditorGUI.DrawRect(area, OrangeWash);
            EditorGUI.DrawRect(new Rect(area.x, area.y, 3f, area.height), Orange); // left stripe

            EditorGUILayout.Space(2f);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                EditorGUILayout.LabelField("⚠  SnapObject needs a Hand Pose", _title);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                EditorGUILayout.LabelField(
                    "No Hand Pose assigned — the hand falls back to its default pose instead of gripping this object. " +
                    "Author one in Tools > UniversalPlayer > Pose Editor (assign this object as the held object, and it " +
                    "auto-links back here), or drop a Pose asset into Hand Pose below.",
                    _body);
            }
            EditorGUILayout.Space(2f);
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(2f);
        }
    }
}
