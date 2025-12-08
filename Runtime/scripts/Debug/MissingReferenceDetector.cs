using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace jeanf.vrplayer
{
    /// <summary>
    /// Detects and logs missing references in MonoBehaviour components using reflection.
    /// Attach this to any GameObject to check its hierarchy for missing references.
    /// </summary>
    public class MissingReferenceDetector : MonoBehaviour
    {
        [Header("Detection Settings")]
        [Tooltip("Check this GameObject and all its children")]
        [SerializeField] private bool checkChildren = true;

        [Tooltip("Log even if no missing references are found")]
        [SerializeField] private bool verboseLogging = true;

        [Tooltip("Run check on Start")]
        [SerializeField] private bool checkOnStart = true;

        private void Start()
        {
            if (checkOnStart)
            {
                CheckForMissingReferences();
            }
        }

        [ContextMenu("Check For Missing References")]
        public void CheckForMissingReferences()
        {
            int missingCount = 0;
            List<string> missingReferencesList = new List<string>();

            Transform[] transforms = checkChildren ? GetComponentsInChildren<Transform>(true) : new Transform[] { transform };

            Debug.Log($"<color=cyan>Starting missing reference check on: <b>{gameObject.name}</b></color>");

            foreach (Transform t in transforms)
            {
                MonoBehaviour[] components = t.GetComponents<MonoBehaviour>();

                foreach (MonoBehaviour component in components)
                {
                    if (component == null)
                    {
                        string msg = $"[MISSING SCRIPT] on GameObject: {t.name}";
                        Debug.LogError(msg, t.gameObject);
                        missingReferencesList.Add(msg);
                        missingCount++;
                        continue;
                    }

                    // Use reflection to check all serialized fields
                    FieldInfo[] fields = component.GetType().GetFields(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                    foreach (FieldInfo field in fields)
                    {
                        // Check if field is serialized
                        if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null)
                            continue;

                        // Check if it's a Unity Object reference type
                        if (!typeof(Object).IsAssignableFrom(field.FieldType))
                            continue;

                        // Get the field value
                        object fieldValue = field.GetValue(component);

                        // Check if it's null
                        if (fieldValue == null || fieldValue.Equals(null))
                        {
                            // Try to determine if this should be assigned (not just optional)
                            // We'll log it and let the user decide
                            string msg = $"[NULL REFERENCE] GameObject: {t.name} | " +
                                       $"Component: {component.GetType().Name} | " +
                                       $"Field: {field.Name} (Type: {field.FieldType.Name})";
                            Debug.LogWarning(msg, t.gameObject);
                            missingReferencesList.Add(msg);
                            missingCount++;
                        }
                        // Special check for destroyed Unity objects
                        else if (fieldValue is Object unityObj && unityObj == null)
                        {
                            string msg = $"[DESTROYED REFERENCE] GameObject: {t.name} | " +
                                       $"Component: {component.GetType().Name} | " +
                                       $"Field: {field.Name}";
                            Debug.LogError(msg, t.gameObject);
                            missingReferencesList.Add(msg);
                            missingCount++;
                        }
                    }
                }
            }

            // Summary
            if (missingCount > 0)
            {
                Debug.LogError($"MISSING/NULL REFERENCES DETECTED: {missingCount} issues found on {gameObject.name}\n" +
                             "Check console for details.", gameObject);
            }
            else if (verboseLogging)
            {
                Debug.Log($"<color=green>No missing references found in {gameObject.name} hierarchy</color>", gameObject);
            }
        }
    }
}
