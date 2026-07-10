using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Locates the Universal Player package root whether it lives in Assets/
    /// (development in uvs-package-creator) or in Packages/ (consumed via registry).
    /// </summary>
    public static class PackagePaths
    {
        public static string Root
        {
            get
            {
                foreach (var guid in AssetDatabase.FindAssets("jeanf.universalplayer t:AssemblyDefinitionAsset"))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith("jeanf.universalplayer.asmdef")) continue;
                    // asmdef sits at <root>/Runtime/scripts/jeanf.universalplayer.asmdef
                    var root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path)));
                    return root?.Replace('\\', '/');
                }

                Assert.Fail("Could not locate jeanf.universalplayer.asmdef in the project. " +
                            "Is the Universal Player package installed (or present under Assets/)? " +
                            "If the asmdef was renamed or moved, update PackagePaths.Root in the test assembly.");
                return null;
            }
        }

        public static string Runtime => $"{Root}/Runtime";
        public static string PlayerPrefab => $"{Runtime}/Prefabs/Player.prefab";
    }
}
