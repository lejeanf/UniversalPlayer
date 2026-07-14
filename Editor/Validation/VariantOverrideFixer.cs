using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Tools/UniversalPlayer/Remove Dead Variant Overrides — strips variant
    /// overrides whose target object no longer exists in the base Player.prefab
    /// (the 'Variant overrides' failure in ValidateSetup). Dead overrides are
    /// already inert — whatever they customized stopped applying when the base
    /// object disappeared — so removing them loses nothing; every removed entry
    /// is logged so the customization can be re-applied on the current base
    /// objects if it is still wanted.
    /// </summary>
    public static class VariantOverrideFixer
    {
        [MenuItem("Tools/UniversalPlayer/Remove Dead Variant Overrides")]
        public static void RemoveDeadOverrides()
        {
            var packageRoot = ProjectSetupChecks.PackageRoot();
            var playerPrefab = packageRoot != null
                ? AssetDatabase.LoadAssetAtPath<GameObject>(ProjectSetupChecks.PlayerPrefabPath())
                : null;
            if (playerPrefab == null)
            {
                Debug.LogError("[UniversalPlayer.Fix] The package Player.prefab could not be located — nothing to fix.");
                return;
            }

            var variants = ProjectSetupChecks.FindPlayerVariants(playerPrefab, packageRoot);
            if (variants.Count == 0)
            {
                Debug.LogWarning("[UniversalPlayer.Fix] No project variant of the package Player.prefab found.");
                return;
            }

            foreach (var variant in variants)
                RemoveDeadOverrides(variant);
        }

        private static void RemoveDeadOverrides(GameObject variant)
        {
            var path = AssetDatabase.GetAssetPath(variant);
            var modifications = PrefabUtility.GetPropertyModifications(variant);
            if (modifications == null)
            {
                Debug.Log($"[UniversalPlayer.Fix] '{path}': no overrides recorded — nothing to do.");
                return;
            }

            // PrefabUtility resolves targets through nested prefabs, so a null
            // target really means the base object is gone (a raw fileID scan of
            // the base file cannot tell — nested objects use computed fileIDs).
            var dead = modifications.Where(m => m.target == null).ToArray();
            if (dead.Length == 0)
            {
                Debug.Log($"[UniversalPlayer.Fix] '{path}': all overrides target objects that still exist — nothing to do.");
                return;
            }

            if (!EditorUtility.DisplayDialog("Remove Dead Variant Overrides",
                    $"'{variant.name}' has {dead.Length} override(s) pointing at objects that no longer exist " +
                    "in the base Player.prefab. They are inert; removing them only cleans the asset.\n\n" +
                    "Every removed entry is logged to the console so you can re-apply the customization it " +
                    "represented on the current base objects.",
                    $"Remove {dead.Length} override(s)", "Cancel"))
                return;

            var report = new StringBuilder();
            report.AppendLine($"[UniversalPlayer.Fix] Removed {dead.Length} dead override(s) from '{path}':");
            foreach (var m in dead)
            {
                var value = m.objectReference != null ? $"ref '{m.objectReference.name}'" : $"'{m.value}'";
                report.AppendLine($"  - {m.propertyPath} = {value}");
            }

            PrefabUtility.SetPropertyModifications(variant, modifications.Where(m => m.target != null).ToArray());
            EditorUtility.SetDirty(variant);
            AssetDatabase.SaveAssetIfDirty(variant);
            Debug.Log(report.ToString());
        }
    }
}
