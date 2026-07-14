using UnityEditor;
using UnityEngine;
using jeanf.universalplayer;

namespace jeanf.universalplayer.editor
{
    /// <summary>
    /// Inspector for <see cref="PickableObject"/>. Its job is to answer, before you ever
    /// hit play, the two questions the runtime can only answer by warning into the
    /// console:
    ///  - "Animated Bone" needs FirstPersonBody enabled with a HUMANOID rig. The body
    ///    ships DISABLED (Player.prefab: Body Enabled = off), so this is the common
    ///    case, and the item would silently fall back to a steady dock.
    ///  - A Rigidbody is required to suspend/restore physics while held.
    /// </summary>
    [CustomEditor(typeof(PickableObject))]
    [CanEditMultipleObjects]
    public class PickableObjectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var pickable = (PickableObject)target;

            if (pickable.GetComponent<Rigidbody>() == null)
            {
                EditorGUILayout.HelpBox(
                    "No Rigidbody. The object can still be picked up, but its physics cannot be suspended while " +
                    "held or restored on release. Add a Rigidbody.",
                    MessageType.Warning);
            }

            if (pickable.Anchor != HeldAnchor.Camera && pickable.AttachMode == HandAttachMode.AnimatedBone)
            {
                DrawAnimatedBoneStatus();
            }

            if (pickable.ReleaseMode == ReleaseTarget.EventDriven)
            {
                EditorGUILayout.HelpBox(
                    "Release Target = Event Driven: this component will NOT place the object. Subscribe to " +
                    "PickableObject.ReleaseRequested and place it yourself (teleport event, inventory, …).",
                    MessageType.Info);
            }
        }

        private static void DrawAnimatedBoneStatus()
        {
            // The body may live on a Player that is not in this scene (additive loading),
            // so a missing body here is "unknown", not "broken" — say so rather than cry wolf.
            var body = Object.FindFirstObjectByType<FirstPersonBody>(FindObjectsInactive.Include);

            if (body == null)
            {
                EditorGUILayout.HelpBox(
                    "Attach Mode = Animated Bone holds this item in the body's REAL hand, which needs a " +
                    "FirstPersonBody (enabled, humanoid rig) on the player.\n\n" +
                    "No FirstPersonBody found in the open scenes — if the Player loads from another scene, check it " +
                    "there. If none is active at runtime, the item falls back to a steady dock in front of the view.",
                    MessageType.Info);
                return;
            }

            if (!body.BodyEnabled)
            {
                EditorGUILayout.HelpBox(
                    "The first-person body is DISABLED, so there is no hand to hold this item in — at runtime it " +
                    "will fall back to a steady dock in front of the view.\n\n" +
                    "Tick 'Body Enabled' on the FirstPersonBody to hold it in the real hand.",
                    MessageType.Warning);

                if (GUILayout.Button("Enable the first-person body"))
                {
                    Undo.RecordObject(body, "Enable first-person body");
                    body.BodyEnabled = true;
                    EditorUtility.SetDirty(body);
                }

                EditorGUILayout.Space(2);
                if (GUILayout.Button("Select the FirstPersonBody")) Selection.activeObject = body.gameObject;
                return;
            }

            if (!body.HasHumanoidHands)
            {
                EditorGUILayout.HelpBox(
                    "The first-person body is enabled but its rig is not Humanoid (or its Animator has not been " +
                    "built yet), so no hand bone can be resolved — the item will fall back to a steady dock.\n\n" +
                    "Import the character with Animation Type = Humanoid.",
                    MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox("The first-person body is enabled with a humanoid rig — this item will be held in the real hand.", MessageType.Info);
        }
    }
}
