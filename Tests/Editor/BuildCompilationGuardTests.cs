using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Build-safety guard: every script that ends up in a PLAYER assembly must compile
    /// in a build. Editor-only code is only allowed behind #if UNITY_EDITOR or inside
    /// an Editor folder covered by an editor-only asmdef. In the dev repo (packages
    /// under Assets/) this sweeps every fr.jeanf package, so a stray UnityEditor
    /// reference fails here instead of at build time.
    /// </summary>
    public class BuildCompilationGuardTests
    {
        private static readonly Regex UnityEditorReference = new Regex(@"\bUnityEditor\b");

        private static List<string> PackageRoots()
        {
            var root = PackagePaths.Root;
            var parent = Path.GetDirectoryName(root)?.Replace('\\', '/');
            if (parent == "Assets")
            {
                // Dev repo: every package developed here gets the same guarantee.
                return Directory.GetDirectories(parent)
                    .Select(d => d.Replace('\\', '/'))
                    .Where(d => File.Exists($"{d}/package.json"))
                    .ToList();
            }
            return new List<string> { root };
        }

        private sealed class AsmdefInfo
        {
            public string Directory;
            public bool EditorOnly;
        }

        private static List<AsmdefInfo> CollectAsmdefs(string root)
        {
            var asmdefs = new List<AsmdefInfo>();
            foreach (var file in Directory.GetFiles(root, "*.asmdef", SearchOption.AllDirectories))
            {
                var json = File.ReadAllText(file);
                // Editor-only asmdefs list exactly ["Editor"] in includePlatforms.
                var editorOnly = Regex.IsMatch(json, "\"includePlatforms\"\\s*:\\s*\\[\\s*\"Editor\"\\s*\\]");
                asmdefs.Add(new AsmdefInfo
                {
                    Directory = Path.GetDirectoryName(file)!.Replace('\\', '/') + "/",
                    EditorOnly = editorOnly
                });
            }
            return asmdefs;
        }

        private static AsmdefInfo NearestAsmdef(List<AsmdefInfo> asmdefs, string csPath)
        {
            // The owning asmdef is the DEEPEST one whose folder contains the script.
            return asmdefs
                .Where(a => csPath.StartsWith(a.Directory))
                .OrderByDescending(a => a.Directory.Length)
                .FirstOrDefault();
        }

        private static bool IsUnderEditorFolder(string csPath) =>
            csPath.Contains("/Editor/") || csPath.EndsWith("/Editor");

        private static IEnumerable<string> CsFiles(string root) =>
            Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                .Select(f => f.Replace('\\', '/'))
                .Where(f => !f.Contains("/Tests/") && !f.Contains("~/") && !f.Contains("/."));

        [Test]
        public void PlayerScripts_NeverReferenceUnityEditor_OutsideEditorGuards()
        {
            var violations = new List<string>();
            foreach (var root in PackageRoots())
            {
                var asmdefs = CollectAsmdefs(root);
                foreach (var file in CsFiles(root))
                {
                    var asmdef = NearestAsmdef(asmdefs, file);
                    // Editor-only assembly: UnityEditor is fine anywhere in it.
                    if (asmdef is { EditorOnly: true }) continue;
                    // No asmdef + legacy Editor special folder: Unity puts it in an
                    // editor assembly inside a PROJECT (the packaging test below still
                    // flags it, but it does not break builds).
                    if (asmdef == null && IsUnderEditorFolder(file)) continue;

                    foreach (var line in UnguardedUnityEditorLines(file))
                        violations.Add(line);
                }
            }

            Assert.That(violations, Is.Empty,
                "These lines reference UnityEditor from a PLAYER assembly outside #if UNITY_EDITOR — the project compiles " +
                "in the editor but every BUILD fails. Wrap the line (and its using directive) in #if UNITY_EDITOR/#endif, " +
                "or move the script into an Editor folder with an editor-only asmdef:\n" + string.Join("\n", violations));
        }

        [Test]
        public void EditorFolders_AreCoveredByAnEditorOnlyAsmdef()
        {
            var violations = new List<string>();
            foreach (var root in PackageRoots())
            {
                var asmdefs = CollectAsmdefs(root);
                foreach (var file in CsFiles(root).Where(IsUnderEditorFolder))
                {
                    var asmdef = NearestAsmdef(asmdefs, file);
                    if (asmdef == null)
                        violations.Add($"{file} — no asmdef at all (legacy Editor folders work under Assets/ but NOT inside a published package)");
                    else if (!asmdef.EditorOnly && UnguardedUnityEditorLines(file).Any())
                        violations.Add($"{file} — sits in an Editor folder but compiles into the non-editor asmdef at '{asmdef.Directory}'");
                }
            }

            Assert.That(violations, Is.Empty,
                "Editor-folder scripts must belong to an asmdef with includePlatforms [\"Editor\"] (see UniversalPlayer/Editor " +
                "for the pattern), otherwise they ship into player builds:\n" + string.Join("\n", violations));
        }

        /// <summary>
        /// Lines that mention UnityEditor while no enclosing #if region contains
        /// UNITY_EDITOR. Line comments are stripped; a rare hit inside a block comment
        /// or string is a prompt to rephrase it, which keeps this parser simple.
        /// </summary>
        private static IEnumerable<string> UnguardedUnityEditorLines(string file)
        {
            var guardStack = new Stack<bool>();
            var editorDepth = 0;
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("#if"))
                {
                    var guarded = trimmed.Contains("UNITY_EDITOR");
                    guardStack.Push(guarded);
                    if (guarded) editorDepth++;
                }
                else if (trimmed.StartsWith("#elif") || trimmed.StartsWith("#else"))
                {
                    if (guardStack.Count > 0 && guardStack.Pop()) editorDepth--;
                    var guarded = trimmed.Contains("UNITY_EDITOR");
                    guardStack.Push(guarded);
                    if (guarded) editorDepth++;
                }
                else if (trimmed.StartsWith("#endif"))
                {
                    if (guardStack.Count > 0 && guardStack.Pop()) editorDepth--;
                }
                else if (editorDepth == 0)
                {
                    var code = lines[i];
                    var comment = code.IndexOf("//", System.StringComparison.Ordinal);
                    if (comment >= 0) code = code.Substring(0, comment);
                    if (UnityEditorReference.IsMatch(code)) yield return $"{file}:{i + 1}: {lines[i].Trim()}";
                }
            }
        }
    }
}
