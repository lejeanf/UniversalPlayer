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
    public class PickableObjectEditor : UnityEditor.Editor
    {
        private bool _previewing;
        private Vector3 _restorePosition;
        private Quaternion _restoreRotation;
        private Transform _restoreParent;

        private void OnDisable() => StopPreview(); // never strand the object in the preview pose

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var pickable = (PickableObject)target;

            EditorGUILayout.Space(6);
            DrawHeldPosePreview(pickable);
            EditorGUILayout.Space(4);

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

        /// <summary>
        /// Puts the object exactly where it will sit when held, live, without entering
        /// play mode — so Held Local Position / Euler (or a hand pose) can be tuned by
        /// eye in the Scene view. Toggle it back off (or just deselect) and the object
        /// returns to where it was; the move is Undo-able either way.
        /// </summary>
        private void DrawHeldPosePreview(PickableObject pickable)
        {
            if (Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Held-pose preview is an edit-mode tool (in play mode, just pick the object up).", MessageType.None);
                return;
            }

            var anchors = Object.FindFirstObjectByType<PlayerItemAnchors>(FindObjectsInactive.Include);
            if (anchors == null || anchors.CameraTransform == null)
            {
                EditorGUILayout.HelpBox(
                    "Held-pose preview needs a PlayerItemAnchors (and a camera) on the player in an open scene. " +
                    "With additive loading the Player may live in another scene — open it to preview.",
                    MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var label = _previewing ? "Stop preview (restore position)" : "Preview held pose";
                if (GUILayout.Button(label, GUILayout.Height(24)))
                {
                    if (_previewing) StopPreview();
                    else StartPreview(pickable, anchors);
                }

                using (new EditorGUI.DisabledScope(!_previewing))
                {
                    if (GUILayout.Button("Keep", GUILayout.Width(60), GUILayout.Height(24)))
                    {
                        // Accept the previewed spot as the object's real position.
                        _previewing = false;
                        EditorApplication.update -= UpdatePreview;
                    }
                }
            }

            if (_previewing)
            {
                UpdatePreview(); // reflect edits made in this same inspector pass
                EditorGUILayout.HelpBox(
                    "PREVIEWING where this object will sit when held. Tweak Held Local Position / Euler (or the Hand " +
                    "Pose) and watch it move. 'Stop preview' restores its original position; 'Keep' leaves it here.",
                    MessageType.Info);
            }
        }

        private void StartPreview(PickableObject pickable, PlayerItemAnchors anchors)
        {
            if (!anchors.TryGetHeldPose(pickable, out _, out _)) return;

            var t = pickable.transform;
            Undo.RecordObject(t, "Preview held pose");
            _restoreParent = t.parent;
            _restorePosition = t.position;
            _restoreRotation = t.rotation;
            _previewing = true;
            EditorApplication.update += UpdatePreview;
            UpdatePreview();
        }

        private void StopPreview()
        {
            EditorApplication.update -= UpdatePreview;
            if (!_previewing) return;
            _previewing = false;

            if (target == null) return; // the object was deleted while previewing
            var t = ((PickableObject)target).transform;
            t.SetParent(_restoreParent);
            t.SetPositionAndRotation(_restorePosition, _restoreRotation);
        }

        private void UpdatePreview()
        {
            if (!_previewing) return;
            if (target == null) { StopPreview(); return; }

            var pickable = (PickableObject)target;
            var anchors = Object.FindFirstObjectByType<PlayerItemAnchors>(FindObjectsInactive.Include);
            if (anchors == null || !anchors.TryGetHeldPose(pickable, out var position, out var rotation))
            {
                StopPreview();
                return;
            }
            pickable.transform.SetPositionAndRotation(position, rotation);
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
