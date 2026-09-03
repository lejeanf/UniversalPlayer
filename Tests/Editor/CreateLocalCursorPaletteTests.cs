using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace jeanf.universalplayer.tests.editor
{
    /// <summary>
    /// Guards the "own your cursor palette" flow: the packaged palette is recognised as
    /// packaged, a local copy lands in Assets/ and is not packaged, and the prefab-mode
    /// prompt fires only for a project prefab still on the packaged palette — never for
    /// the package's own Player prefab, which consumers cannot edit anyway.
    /// </summary>
    public class CreateLocalCursorPaletteTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<string> _createdAssets = new List<string>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (var path in _createdAssets) AssetDatabase.DeleteAsset(path);
            _createdAssets.Clear();
        }

        [Test]
        public void IsPackaged_DistinguishesPackagedFromLocalPalette()
        {
            Assert.That(CreateLocalCursorPalette.IsPackaged(null), Is.False, "A missing palette is not 'packaged' — the inspector words that case separately.");
            Assert.That(CreateLocalCursorPalette.IsPackaged(PackagedPalette()), Is.True, "The shipped CursorPalette must be recognised as packaged.");

            var local = NewLocalPalette("Assets/CursorPalette_IsPackagedTest.asset");
            Assert.That(CreateLocalCursorPalette.IsPackaged(local), Is.False, "An asset under Assets/ is project-owned, not packaged.");
        }

        [Test]
        public void CreateLocalCopy_LandsInAssetsAndIsNotPackaged()
        {
            var copy = CreateLocalCursorPalette.CreateLocalCopy();
            Assert.That(copy, Is.Not.Null, "CreateLocalCopy returned nothing — is the packaged CursorPalette asset missing?");
            var path = AssetDatabase.GetAssetPath(copy);
            _createdAssets.Add(path);

            Assert.That(path, Does.StartWith("Assets/"), "The local copy must live in the project, not in the package.");
            Assert.That(CreateLocalCursorPalette.IsPackaged(copy), Is.False, "The copy must not itself count as packaged.");
            var packaged = PackagedPalette();
            Assert.That(copy.resting, Is.EqualTo(packaged.resting), "The copy must start from the packaged colours.");
            Assert.That(copy.hover, Is.EqualTo(packaged.hover));
        }

        [Test]
        public void FindCursorNeedingLocalPalette_FlagsProjectPrefabOnPackagedPalette()
        {
            var root = Spawn("PlayerVariant");
            var cursor = root.AddComponent<CursorStateController>();
            SetPalette(cursor, PackagedPalette());

            Assert.That(CreateLocalCursorPalette.FindCursorNeedingLocalPalette(root, "Assets/Player Variant.prefab"), Is.SameAs(cursor),
                "A project prefab still on the packaged palette must be flagged so prefab mode can offer the local copy.");
        }

        [Test]
        public void FindCursorNeedingLocalPalette_IgnoresLocalPaletteAndPackagePrefab()
        {
            var root = Spawn("PlayerVariant");
            var cursor = root.AddComponent<CursorStateController>();

            SetPalette(cursor, NewLocalPalette("Assets/CursorPalette_PromptTest.asset"));
            Assert.That(CreateLocalCursorPalette.FindCursorNeedingLocalPalette(root, "Assets/Player Variant.prefab"), Is.Null,
                "A project-local palette is the recommended setup — no prompt.");

            SetPalette(cursor, PackagedPalette());
            Assert.That(CreateLocalCursorPalette.FindCursorNeedingLocalPalette(root, ProjectSetupChecks.PlayerPrefabPath()), Is.Null,
                "The package's own Player prefab legitimately ships the packaged palette and cannot be edited by consumers — never prompt there.");

            Assert.That(CreateLocalCursorPalette.FindCursorNeedingLocalPalette(Spawn("NoCursor"), "Assets/Other.prefab"), Is.Null,
                "A prefab without a CursorStateController has nothing to fix.");
        }

        [Test]
        public void AssignTo_WritesTheSerializedPaletteField()
        {
            var cursor = Spawn("Player").AddComponent<CursorStateController>();
            var local = NewLocalPalette("Assets/CursorPalette_AssignTest.asset");

            CreateLocalCursorPalette.AssignTo(cursor, local);

            Assert.That(cursor.Palette, Is.SameAs(local), "AssignTo must land on the serialized 'palette' field the runtime reads.");
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private CursorPaletteSO NewLocalPalette(string path)
        {
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var palette = ScriptableObject.CreateInstance<CursorPaletteSO>();
            AssetDatabase.CreateAsset(palette, path);
            _createdAssets.Add(path);
            return palette;
        }

        private static CursorPaletteSO PackagedPalette()
        {
            var packaged = AssetDatabase.FindAssets("t:CursorPaletteSO")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.StartsWith(ProjectSetupChecks.PackageRoot() ?? "<none>"))
                .Select(AssetDatabase.LoadAssetAtPath<CursorPaletteSO>)
                .FirstOrDefault();
            Assert.That(packaged, Is.Not.Null, "The package must ship a default CursorPalette asset.");
            return packaged;
        }

        private static void SetPalette(CursorStateController cursor, CursorPaletteSO palette)
        {
            var serialized = new SerializedObject(cursor);
            serialized.FindProperty("palette").objectReferenceValue = palette;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
