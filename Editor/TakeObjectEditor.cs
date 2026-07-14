using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer.editor
{
    /// <summary>
    /// Inspector for <see cref="TakeObject"/>, built around one question: "I click the
    /// object and nothing happens — why?"
    ///
    /// Pickup silently requires FOUR things to line up, and the inspector shows none of
    /// them: an assigned Take action, a Layer Mask that actually contains the pickable's
    /// layer, a Collider on the pickable, and a PickableObject on that collider or one of
    /// its parents. This editor cross-checks the mask against the PickableObjects really
    /// in the scene and offers a one-click fix — the layer mismatch is the classic cause
    /// (a tablet left on Default while the mask says Grab is invisible to the raycast).
    /// </summary>
    [CustomEditor(typeof(TakeObject))]
    public class TakeObjectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var takeObject = (TakeObject)target;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Setup check", EditorStyles.boldLabel);

            var takeAction = serializedObject.FindProperty("takeAction");
            var mainCamera = serializedObject.FindProperty("mainCamera");
            var layerMaskProperty = serializedObject.FindProperty("layerMask");
            var maxDistance = serializedObject.FindProperty("maxDistanceCheck").floatValue;
            var mask = layerMaskProperty.intValue;

            var blocked = false;

            if (takeAction.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Take Action is not assigned — no press can ever reach this component, so NOTHING can be picked up. " +
                    "Assign FPS/TakeObject.",
                    MessageType.Error);
                blocked = true;
            }

            if (mainCamera.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Main Camera is not assigned — the grab raycast is cast from it, so pickup is dead.",
                    MessageType.Error);
                blocked = true;
            }

            if (mask == 0)
            {
                EditorGUILayout.HelpBox(
                    "Layer Mask is 'Nothing' — the grab raycast can never hit anything. Set it to the layer(s) your " +
                    "pickable objects live on.",
                    MessageType.Error);
                blocked = true;
            }
            else
            {
                DrawPickableLayerAudit(mask, layerMaskProperty, maxDistance);
            }

            if (serializedObject.FindProperty("objectTakenChannel").objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(
                    "Object Taken Channel is empty. Pickup still works, but nothing is broadcast when an object is " +
                    "taken — a PrimaryItemBehaviour on the item will NOT be promoted to 'drawn' by the grab. " +
                    "(An item whose Carry Slot is Primary is still equipped directly, so the tablet works either way.)",
                    MessageType.Info);
            }

            if (!blocked)
            {
                EditorGUILayout.HelpBox(
                    "Tick 'Is Debug' and click an object in play mode: the console then reports exactly which gate " +
                    "rejected the press (input, UI, layer, range, or a missing PickableObject).",
                    MessageType.None);
            }
        }

        /// <summary>
        /// The check that actually finds the bug: every PickableObject in the open scenes
        /// is compared against the mask, so a pickable the raycast can never see is named
        /// outright instead of just "nothing happens".
        /// </summary>
        private void DrawPickableLayerAudit(int mask, SerializedProperty layerMaskProperty, float maxDistance)
        {
            var pickables = Object.FindObjectsByType<PickableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (pickables.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No PickableObject found in the open scenes — nothing to cross-check the Layer Mask against. " +
                    "(With additive loading the pickables may simply live in another scene.)",
                    MessageType.None);
                return;
            }

            var unreachable = new List<PickableObject>();
            var colliderless = new List<PickableObject>();
            var missedLayers = new HashSet<int>();

            foreach (var pickable in pickables)
            {
                var colliders = pickable.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0)
                {
                    colliderless.Add(pickable);
                    continue;
                }
                // Reachable if ANY of its colliders sits on a layer inside the mask —
                // the raycast hits a collider, and the component is then found on it or a parent.
                var reachable = colliders.Any(c => (mask & (1 << c.gameObject.layer)) != 0);
                if (reachable) continue;

                unreachable.Add(pickable);
                foreach (var c in colliders) missedLayers.Add(c.gameObject.layer);
            }

            if (colliderless.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{colliderless.Count} PickableObject(s) have NO Collider anywhere on them, so a physics raycast can " +
                    $"never hit them: {Names(colliderless)}.\n\n" +
                    "A world-space Canvas is not hittable by a physics raycast — the item needs a real Collider.",
                    MessageType.Warning);
            }

            if (unreachable.Count == 0)
            {
                if (colliderless.Count == 0)
                    EditorGUILayout.HelpBox(
                        $"All {pickables.Length} PickableObject(s) in the open scenes are on layers inside this mask, and " +
                        $"each has a Collider. Remember they must also be within Max Distance Check ({maxDistance:0.##} m).",
                        MessageType.Info);
                return;
            }

            var layerNames = string.Join(", ", missedLayers.Select(l => $"'{LayerMask.LayerToName(l)}'"));
            EditorGUILayout.HelpBox(
                $"{unreachable.Count} PickableObject(s) can NEVER be picked up: their colliders are on layer(s) " +
                $"{layerNames}, which are not in this Layer Mask — the grab raycast passes straight through them.\n\n" +
                $"{Names(unreachable)}\n\n" +
                "The reticle may still highlight them (it uses a different raycast), which is exactly why this looks " +
                "like \"I click it and nothing happens\".",
                MessageType.Error);

            if (GUILayout.Button($"Add {layerNames} to the Layer Mask"))
            {
                var newMask = mask;
                foreach (var layer in missedLayers) newMask |= 1 << layer;
                layerMaskProperty.intValue = newMask;
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space(2);
            if (GUILayout.Button("Select the unreachable object(s)"))
                Selection.objects = unreachable.Select(p => (Object)p.gameObject).ToArray();
        }

        private static string Names(IEnumerable<PickableObject> pickables)
            => string.Join(", ", pickables.Take(6).Select(p => $"'{p.name}'"))
               + (pickables.Count() > 6 ? $" (+{pickables.Count() - 6} more)" : string.Empty);
    }
}
