using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer
{
    public static class LocalPrefabOverrides
    {
        public static bool IsProjectOwnedPrefab(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/")) return false;
            var packageRoot = ProjectSetupChecks.PackageRoot();
            return packageRoot == null || !assetPath.StartsWith(packageRoot + "/");
        }

        public static string NearestProjectOwnedPrefabPath(GameObject instanceObject)
        {
            if (instanceObject == null || !PrefabUtility.IsPartOfPrefabInstance(instanceObject)) return null;
            var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(instanceObject);
            while (instanceRoot != null)
            {
                var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot);
                if (IsProjectOwnedPrefab(assetPath)) return assetPath;
                var parent = instanceRoot.transform.parent;
                instanceRoot = parent != null ? PrefabUtility.GetNearestPrefabInstanceRoot(parent.gameObject) : null;
            }
            return null;
        }

        public static void ApplyAddedComponent(Component added)
        {
            var prefabPath = NearestProjectOwnedPrefabPath(added.gameObject);
            if (prefabPath == null || !PrefabUtility.IsAddedComponentOverride(added)) return;
            PrefabUtility.ApplyAddedComponent(added, prefabPath, InteractionMode.AutomatedAction);
        }

        public static void ApplyComponent(Component component)
        {
            if (PrefabUtility.IsAddedComponentOverride(component))
            {
                ApplyAddedComponent(component);
                return;
            }
            var prefabPath = NearestProjectOwnedPrefabPath(component.gameObject);
            if (prefabPath == null || !PrefabUtility.IsPartOfPrefabInstance(component)) return;
            PrefabUtility.ApplyObjectOverride(component, prefabPath, InteractionMode.AutomatedAction);
        }

        public static void ApplyPropertyOverride(Object target, string propertyPath)
        {
            EditorUtility.SetDirty(target);
            var gameObject = target as GameObject ?? (target as Component)?.gameObject;
            var prefabPath = NearestProjectOwnedPrefabPath(gameObject);
            if (prefabPath == null) return;
            var property = new SerializedObject(target).FindProperty(propertyPath);
            if (property != null && property.prefabOverride)
                PrefabUtility.ApplyPropertyOverride(property, prefabPath, InteractionMode.AutomatedAction);
        }
    }
}
