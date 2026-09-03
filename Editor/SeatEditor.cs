using jeanf.validationTools;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Seat inspector: the default fields (still tinted orange by the shared [Validation] drawer),
    /// plus — when an anchor is unassigned — a per-field "Fix" button and a "Fix all" at the bottom
    /// that wire the empty anchors from child GameObjects matched by name.
    /// </summary>
    [CustomEditor(typeof(Seat))]
    public class SeatEditor : Editor
    {
        private static readonly (string prop, string label, string[] keys)[] Anchors =
        {
            ("sitAnchor", "Sit Anchor", new[] { "sit" }),
            ("exitAnchor", "Exit Anchor", new[] { "exit" }),
            ("handSupportAnchor", "Hand Support Anchor", new[] { "hand", "support" }),
        };

        public override void OnInspectorGUI()
        {
            ValidationUi.DrawIssuesBanner(target as Component); // keep the shared orange "needs setup" banner this custom editor would otherwise replace
            DrawDefaultInspector();

            var root = ((Seat)target).transform;

            var unassigned = 0;
            var fixable = 0;
            foreach (var a in Anchors)
            {
                var p = serializedObject.FindProperty(a.prop);
                if (p == null || p.objectReferenceValue != null) continue;
                unassigned++;
                if (FindChild(root, a.keys) != null) fixable++;
            }

            if (unassigned == 0) return;

            EditorGUILayout.Space();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Unassigned anchors", EditorStyles.boldLabel);

                foreach (var a in Anchors)
                {
                    var p = serializedObject.FindProperty(a.prop);
                    if (p == null || p.objectReferenceValue != null) continue;
                    var child = FindChild(root, a.keys);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(child != null ? $"{a.label}  →  {child.name}" : $"{a.label}  —  no matching child");
                        using (new EditorGUI.DisabledScope(child == null))
                            if (GUILayout.Button("Fix", GUILayout.Width(60f)))
                                Assign(p, child);
                    }
                }

                using (new EditorGUI.DisabledScope(fixable == 0))
                    if (GUILayout.Button($"Fix all ({fixable})"))
                    {
                        foreach (var a in Anchors)
                        {
                            var p = serializedObject.FindProperty(a.prop);
                            if (p == null || p.objectReferenceValue != null) continue;
                            var child = FindChild(root, a.keys);
                            if (child != null) p.objectReferenceValue = child;
                        }
                        serializedObject.ApplyModifiedProperties();
                    }
            }
        }

        private void Assign(SerializedProperty prop, Transform child)
        {
            prop.objectReferenceValue = child;
            serializedObject.ApplyModifiedProperties();
        }

        // Anchors are usually child GameObjects named SitAnchor / ExitAnchor / HandSupportAnchor.
        private static Transform FindChild(Transform root, string[] keys)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                var n = t.name.ToLowerInvariant();
                foreach (var k in keys)
                    if (n.Contains(k)) return t;
            }
            return null;
        }
    }
}
