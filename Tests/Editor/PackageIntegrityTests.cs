using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Guards against the class of breakage where assets are moved or recreated
    /// without their .meta files: prefabs silently lose script/asset references
    /// and features (like the VR hands) disappear without any error.
    /// </summary>
    public class PackageIntegrityTests
    {
        // Unity's built-in resource pseudo-GUIDs — always valid, never in AssetDatabase.
        private static readonly HashSet<string> BuiltinGuids = new HashSet<string>
        {
            "0000000000000000e000000000000000", // Builtin Extra (Default-Material, UI sprites, ...)
            "0000000000000000f000000000000000", // Default Resources (editor icons, ...)
        };

        private static readonly Regex GuidPattern = new Regex(@"guid:\s*([0-9a-f]{32})", RegexOptions.Compiled);

        private static IEnumerable<string> AllRuntimePrefabPaths()
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { PackagePaths.Runtime })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p);
        }

        [Test]
        public void AllRuntimePrefabs_HaveNoMissingScripts()
        {
            var failures = new List<string>();

            foreach (var prefabPath in AllRuntimePrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    failures.Add($"{prefabPath}: prefab failed to load entirely.");
                    continue;
                }

                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                {
                    var missing = transform.GetComponents<Component>().Count(c => c == null);
                    if (missing > 0)
                        failures.Add($"{prefabPath} → '{HierarchyPath(transform)}': {missing} missing script(s).");
                }
            }

            Assert.That(failures, Is.Empty,
                "Missing scripts found:\n" + string.Join("\n", failures) + "\n\n" +
                "HINT: a missing script means the MonoBehaviour it pointed to no longer exists under " +
                "the same GUID. Usual causes: the script file was moved/recreated without its .meta, " +
                "or it lives in an assembly that no longer compiles. Check 'git log --follow' on the " +
                "script you expect there, and confirm its .meta moved with it.");
        }

        [Test]
        public void AllRuntimeAssets_HaveNoBrokenGuidReferences()
        {
            // .mat included since the pink-hands bug: Hands_Skin/Hands_Nails referenced a
            // diffusion profile whose asset had been recreated without its .meta —
            // invisible in prefab scans, materials break just as silently.
            var assetPaths = AllRuntimePrefabPaths()
                .Concat(AssetDatabase.FindAssets("t:Material", new[] { PackagePaths.Runtime })
                    .Select(AssetDatabase.GUIDToAssetPath))
                .Concat(AssetDatabase.FindAssets("t:ScriptableObject", new[] { PackagePaths.Runtime })
                    .Select(AssetDatabase.GUIDToAssetPath))
                .Where(p => !string.IsNullOrEmpty(p))
                .Distinct()
                .OrderBy(p => p);

            var failures = new List<string>();
            foreach (var assetPath in assetPaths)
            {
                var absolutePath = Path.GetFullPath(assetPath);
                if (!File.Exists(absolutePath)) continue; // e.g. assets inside packages resolved to virtual paths
                var unresolved = GuidPattern.Matches(File.ReadAllText(absolutePath))
                    .Select(m => m.Groups[1].Value)
                    .Distinct()
                    .Where(g => !BuiltinGuids.Contains(g))
                    .Where(g => string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(g)))
                    .ToList();

                foreach (var guid in unresolved)
                    failures.Add($"{assetPath}: references GUID {guid} which resolves to nothing.");
            }

            Assert.That(failures, Is.Empty,
                "Broken asset references found:\n" + string.Join("\n", failures) + "\n\n" +
                "HINT: the asset references another asset that does not exist in this project. Usual causes: " +
                "(a) the asset was moved or recreated WITHOUT its .meta file, so its GUID changed — find it " +
                "and restore/re-assign; (b) the asset sits in an unimported package sample (Samples~) — " +
                "move it into Runtime/ or import the sample; (c) the package providing it is not installed. " +
                "Start by searching git history for the GUID: git log -S <guid> --oneline");
        }

        private static string HierarchyPath(Transform t)
        {
            var path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = $"{t.name}/{path}";
            }
            return path;
        }
    }
}
